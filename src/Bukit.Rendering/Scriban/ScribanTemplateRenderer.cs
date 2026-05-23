using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using Bukit.Config;
using Bukit.Content;
using Bukit.Routing;
using Bukit.Shared;
using Bukit.Theme;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Bukit.Rendering.Scriban;

public sealed class ScribanTemplateRenderer
{
    private const int MaxLayoutDepth = 10;
    private readonly string _layoutsDir;
    private readonly FileTemplateLoader _templateLoader;
    private readonly IReadOnlyDictionary<string, string>? _shortcodes;
    private readonly IReadOnlyDictionary<string, ComponentDefinition>? _components;
    private readonly ConcurrentDictionary<string, CachedTemplate> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ThemeComponentRegistry? _themeRegistry;
    private readonly SectionSchemaValidator? _schemaValidator;
    private readonly SectionDataResolverAccessor? _dataResolver;
    private readonly string _componentValidation;
    private readonly IReadOnlyList<(ContentItem Item, RouteInfo? Route)>? _allPages;

    public ScribanTemplateRenderer(string layoutsDir, string? parentLayoutsDir = null, IReadOnlyDictionary<string, string>? shortcodes = null, IReadOnlyDictionary<string, ComponentDefinition>? components = null, string? userLayoutsDir = null)
        : this(layoutsDir, parentLayoutsDir, shortcodes, components, userLayoutsDir, null, null, null, "off", null)
    {
    }

    public ScribanTemplateRenderer(string layoutsDir, string? parentLayoutsDir, IReadOnlyDictionary<string, string>? shortcodes, IReadOnlyDictionary<string, ComponentDefinition>? components, string? userLayoutsDir, ThemeComponentRegistry? themeRegistry, SectionSchemaValidator? schemaValidator, SectionDataResolverAccessor? dataResolver, string componentValidation, IReadOnlyList<(ContentItem, RouteInfo?)>? allPages = null)
    {
        _layoutsDir = layoutsDir;
        _templateLoader = new FileTemplateLoader(_layoutsDir, parentLayoutsDir, userLayoutsDir);
        _shortcodes = shortcodes;
        _components = components;
        _themeRegistry = themeRegistry;
        _schemaValidator = schemaValidator;
        _dataResolver = dataResolver;
        _componentValidation = componentValidation;
        _allPages = allPages;
    }

    public string RenderPage(string templateRelativePath, PageModel model)
    {
        var globals = ScribanModelBinder.ToScriptObject(model);
        return Render(templateRelativePath, globals, 0);
    }

    public string RenderList(string templateRelativePath, ListPageModel model)
    {
        var globals = ScribanModelBinder.ToScriptObject(model);
        return Render(templateRelativePath, globals, 0);
    }

    private string Render(string templateRelativePath, ScriptObject globals, int depth)
    {
        if (depth >= MaxLayoutDepth)
        {
            throw new RenderException($"Layout nesting depth exceeded maximum of {MaxLayoutDepth}. Possible circular layout reference in '{templateRelativePath}'.");
        }

        var cached = GetCachedTemplate(templateRelativePath);
        if (cached.LayoutTemplateRelativePath is not null)
        {
            var body = RenderTemplate(cached.Template, templateRelativePath, globals);
            globals.SetValue("content", body, readOnly: true);
            return Render(cached.LayoutTemplateRelativePath, globals, depth + 1);
        }

        return RenderTemplate(cached.Template, templateRelativePath, globals);
    }

    private string RenderTemplate(Template template, string templateRelativePath, ScriptObject globals)
    {
        var context = new TemplateContext
        {
            TemplateLoader = _templateLoader,
            EnableRelaxedMemberAccess = true,
            EnableRelaxedTargetAccess = true,
            EnableNullIndexer = true
        };

        if (_shortcodes is { Count: > 0 })
        {
            var shortcodeObj = new ScriptObject();
            var capturedShortcodes = _shortcodes;
            shortcodeObj.SetValue("shortcode", new Func<string, string, string, string, string, string>((name, a1, a2, a3, a4) =>
            {
                var args = new List<string>();
                if (!string.IsNullOrEmpty(a1)) args.Add(a1);
                if (!string.IsNullOrEmpty(a2)) args.Add(a2);
                if (!string.IsNullOrEmpty(a3)) args.Add(a3);
                if (!string.IsNullOrEmpty(a4)) args.Add(a4);
                return ShortcodeProcessor.RenderShortcode(name, capturedShortcodes, args.ToArray());
            }), readOnly: true);
            context.PushGlobal(shortcodeObj);
        }

        if (_components is { Count: > 0 })
        {
            ComponentFunctions.Components = _components;
            ComponentFunctions.TemplateLoader = _templateLoader;
            ComponentFunctions.ParentGlobals = globals;
            var componentObj = new ScriptObject();
            componentObj.SetValue("render", new Func<string, string, string, string, string>(ComponentFunctions.Render), readOnly: true);
            context.PushGlobal(new ScriptObject { ["comp"] = componentObj });
        }

        if (_themeRegistry is not null)
        {
            var sectionHelpers = new SectionRenderHelper(
                _themeRegistry, _schemaValidator, _componentValidation,
                _templateLoader, globals, _allPages);
            var sectionObj = new ScriptObject();
            sectionObj.SetValue("render_section", new RenderSectionFunction(sectionHelpers), readOnly: true);
            context.PushGlobal(sectionObj);

            if (_themeRegistry.Components.Count > 0)
            {
                ComponentFunctions.ThemeComponents = _themeRegistry.Components;
                ComponentFunctions.ThemeTemplateLoader = _templateLoader;
                ComponentFunctions.ThemeParentGlobals = globals;
                ComponentFunctions.ThemeRegistryRoot = Path.Combine(_themeRegistry.ThemeRoot, "layouts");
                var compObj = new ScriptObject();
                compObj.SetValue("render", new Func<string, object, string>(ComponentFunctions.RenderComponent), readOnly: true);
                context.PushGlobal(new ScriptObject { ["comp"] = compObj });
            }
        }

        var imageObj = new ScriptObject();
        imageObj.SetValue("srcset", new Func<string, string, string>(ImageHelper.BuildSrcset), readOnly: true);
        imageObj.SetValue("img", new Func<string, string, string, string, string>(ImageHelper.BuildImgTag), readOnly: true);
        context.PushGlobal(new ScriptObject { ["image"] = imageObj });

        context.PushGlobal(globals);

        try
        {
            var result = template.Render(context);
            if (_shortcodes is { Count: > 0 })
            {
                result = ShortcodeProcessor.RenderShortcodes(result, _shortcodes);
            }
            return result;
        }
        catch (Exception ex)
        {
            throw new RenderException($"Render failed: {templateRelativePath}", ex);
        }
    }

    private CachedTemplate GetCachedTemplate(string templateRelativePath)
    {
        var templatePath = ResolveTemplatePath(templateRelativePath);
        var fileInfo = new FileInfo(templatePath);
        if (!fileInfo.Exists)
        {
            throw new RenderException($"Template not found: {templateRelativePath}");
        }

        var signature = new FileSignature(fileInfo.LastWriteTimeUtc, fileInfo.Length);
        if (_cache.TryGetValue(templatePath, out var existing) && existing.Signature.Equals(signature))
        {
            return existing;
        }

        var templateText = File.ReadAllText(templatePath);
        CachedTemplate parsed;
        if (ScribanLayoutDirectiveParser.TryExtractLayoutDirective(templateText, out var layoutTemplateRelativePath, out var bodyTemplateText))
        {
            var bodyTemplate = ParseTemplateOrThrow(bodyTemplateText, templatePath, templateRelativePath);
            parsed = new CachedTemplate(signature, bodyTemplate, layoutTemplateRelativePath);
        }
        else
        {
            var template = ParseTemplateOrThrow(templateText, templatePath, templateRelativePath);
            parsed = new CachedTemplate(signature, template, null);
        }

        _cache[templatePath] = parsed;
        return parsed;
    }

    private string ResolveTemplatePath(string templateRelativePath)
    {
        try
        {
            return _templateLoader.GetPath(new TemplateContext(), default, templateRelativePath);
        }
        catch (Exception ex)
        {
            throw new RenderException($"Template path is invalid: {templateRelativePath}", ex);
        }
    }

    private static Template ParseTemplateOrThrow(string text, string templatePath, string templateRelativePath)
    {
        var template = Template.Parse(text, templatePath);
        if (template.HasErrors)
        {
            throw new RenderException($"Template parse error: {templateRelativePath}\n{template.Messages}");
        }

        return template;
    }

    private readonly record struct FileSignature(DateTime LastWriteTimeUtc, long Length);

    private sealed record CachedTemplate(FileSignature Signature, Template Template, string? LayoutTemplateRelativePath);
}

internal sealed class SectionRenderHelper
{
    private readonly ThemeComponentRegistry _themeRegistry;
    private readonly SectionSchemaValidator? _schemaValidator;
    private readonly string _componentValidation;
    private readonly FileTemplateLoader _templateLoader;
    private readonly ScriptObject _parentGlobals;
    private readonly IReadOnlyList<(ContentItem Item, RouteInfo? Route)>? _allPages;

    internal ScriptObject ParentGlobals => _parentGlobals;

    public SectionRenderHelper(
        ThemeComponentRegistry themeRegistry,
        SectionSchemaValidator? schemaValidator,
        string componentValidation,
        FileTemplateLoader templateLoader,
        ScriptObject parentGlobals,
        IReadOnlyList<(ContentItem, RouteInfo?)>? allPages)
    {
        _themeRegistry = themeRegistry;
        _schemaValidator = schemaValidator;
        _componentValidation = componentValidation;
        _templateLoader = templateLoader;
        _parentGlobals = parentGlobals;
        _allPages = allPages;
    }

    public string render_section(string jsonInput)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(jsonInput)) return "<!-- render_section: empty input -->";

            jsonInput = jsonInput.Trim();
            if (!jsonInput.StartsWith('[') && !jsonInput.StartsWith('{'))
                return "<!-- render_section: input is not valid JSON -->";

            var parsed = PageComposer.ParseSections(jsonInput);
            if (parsed.Count == 0) return "<!-- render_section: no sections parsed -->";

            var composed = PageComposer.Compose(parsed, _themeRegistry.Sections);

            var sb = new StringBuilder();
            foreach (var sectionDef in composed)
            {
                sb.Append(RenderOneSection(sectionDef));
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"<!-- render_section error: {ex.Message} -->";
        }
    }

    public string RenderScriptObjectSection(ScriptObject so, ScriptObject parentGlobals)
    {
        try
        {
            var sectionType = so.ContainsKey("type") ? so["type"]?.ToString() : null;
            if (string.IsNullOrEmpty(sectionType)) return "<!-- render_section: missing type -->";

            var sectionDef = _themeRegistry.ResolveSection(sectionType);
            if (sectionDef is null) return $"<!-- section not found: {sectionType} -->";

            var variant = so.ContainsKey("variant") ? so["variant"]?.ToString() : null;
            var templatePath = _themeRegistry.ResolveSectionTemplate(sectionType, variant);
            if (templatePath is null || !File.Exists(templatePath)) return $"<!-- section template not found: {sectionType} -->";

            var props = ExtractPropsFromScriptObject(so);

            if (_schemaValidator is not null)
            {
                var validationMode = _componentValidation switch
                {
                    "strict" => ValidationMode.Strict,
                    "warn" => ValidationMode.Warn,
                    _ => ValidationMode.Off
                };
                if (validationMode != ValidationMode.Off)
                {
                    _schemaValidator.Validate(sectionType, sectionDef, props);
                }
            }

            var sectionDefForRender = new PageSectionDefinition
            {
                Type = sectionType,
                Variant = variant,
                Props = props ?? new Dictionary<string, object?>()
            };

            if (so.ContainsKey("limit") && int.TryParse(so["limit"]?.ToString(), out var l))
                sectionDefForRender.Limit = l;
            if (so.ContainsKey("sort") && so["sort"] is string s)
                sectionDefForRender.Sort = s;

            return RenderOneSectionBase(sectionDefForRender, templatePath, sectionDef, parentGlobals);
        }
        catch (Exception ex)
        {
            return $"<!-- render_section error: {ex.Message} -->";
        }
    }

    private string RenderOneSection(PageSectionDefinition sectionDef)
    {
        var sectionType = sectionDef.Type;
        if (string.IsNullOrEmpty(sectionType)) return "<!-- render_section: missing type -->";

        var themeSection = _themeRegistry.ResolveSection(sectionType);
        if (themeSection is null) return $"<!-- section not found: {sectionType} -->";

        var templatePath = _themeRegistry.ResolveSectionTemplate(sectionType, sectionDef.Variant);
        if (templatePath is null || !File.Exists(templatePath)) return $"<!-- section template not found: {sectionType} -->";

        return RenderOneSectionBase(sectionDef, templatePath, themeSection, _parentGlobals);
    }

    private string RenderOneSectionBase(PageSectionDefinition sectionDef, string templatePath, ThemeSectionDefinition themeSection, ScriptObject parentGlobals)
    {
        var sectionType = sectionDef.Type;
        var props = sectionDef.Props;

        if (_schemaValidator is not null)
        {
            var validationMode = _componentValidation switch
            {
                "strict" => ValidationMode.Strict,
                "warn" => ValidationMode.Warn,
                _ => ValidationMode.Off
            };
            if (validationMode != ValidationMode.Off)
            {
                _schemaValidator.Validate(sectionType, themeSection, props);
            }
        }

        var sectionTemplateText = File.ReadAllText(templatePath);
        var sectionTemplate = Template.Parse(sectionTemplateText, templatePath);
        if (sectionTemplate.HasErrors) return $"<!-- section template error: {sectionTemplate.Messages} -->";

        var sectionContext = new TemplateContext
        {
            TemplateLoader = _templateLoader,
            EnableRelaxedMemberAccess = true,
            EnableRelaxedTargetAccess = true,
            EnableNullIndexer = true
        };

        var so = new ScriptObject();
        so.SetValue("type", sectionDef.Type, readOnly: true);
        if (sectionDef.Variant is not null) so.SetValue("variant", sectionDef.Variant, readOnly: true);
        if (props is not null)
        {
            var propsObj = new ScriptObject();
            foreach (var kv in props)
            {
                if (kv.Value is not null) propsObj.SetValue(kv.Key, ConvertToScribanValue(kv.Value), readOnly: true);
            }
            so.SetValue("props", propsObj, readOnly: true);
        }
        if (sectionDef.Limit is not null) so.SetValue("limit", sectionDef.Limit, readOnly: true);
        if (sectionDef.Sort is not null) so.SetValue("sort", sectionDef.Sort, readOnly: true);

        var sectionGlobals = new ScriptObject();
        sectionGlobals.SetValue("section", so, readOnly: true);

        if (!string.IsNullOrWhiteSpace(sectionDef.Source) && _allPages is not null)
        {
            var resolved = SectionDataResolver.Resolve(sectionDef, _allPages);
            if (resolved.Count > 0)
            {
                var itemsArray = new ScriptArray();
                foreach (var (item, url) in resolved)
                {
                    itemsArray.Add(ContentItemToScriptObject(item, url));
                }
                sectionGlobals.SetValue("items", itemsArray, readOnly: true);
            }
        }

        sectionContext.PushGlobal(sectionGlobals);

        if (_themeRegistry.Components.Count > 0)
        {
            ComponentFunctions.ThemeComponents = _themeRegistry.Components;
            ComponentFunctions.ThemeTemplateLoader = _templateLoader;
            ComponentFunctions.ThemeParentGlobals = parentGlobals;
            ComponentFunctions.ThemeRegistryRoot = Path.Combine(_themeRegistry.ThemeRoot, "layouts");
            sectionGlobals.SetValue("render_component", new RenderComponentFunction(), readOnly: true);
        }

        sectionContext.PushGlobal(parentGlobals);

        return sectionTemplate.Render(sectionContext);
    }

    private static ScriptObject ContentItemToScriptObject(ContentItem item, string? url)
    {
        var obj = new ScriptObject();
        obj.SetValue("title", item.Title, readOnly: true);
        obj.SetValue("url", url ?? "", readOnly: true);
        obj.SetValue("slug", item.Slug, readOnly: true);
        obj.SetValue("publish_date", item.PublishAt.DateTime, readOnly: true);
        obj.SetValue("publish_date_formatted", item.PublishAt.ToString("yyyy-MM-dd"), readOnly: true);

        if (item.Meta.TryGetValue("summary", out var s) && s is string summary)
        {
            obj.SetValue("summary", summary, readOnly: true);
        }

        if (item.Fields is not null)
        {
            var fieldsObj = new ScriptObject();
            foreach (var kv in item.Fields)
            {
                if (kv.Value.Value is not null) fieldsObj.SetValue(kv.Key, kv.Value.Value, readOnly: true);
            }
            obj.SetValue("fields", fieldsObj, readOnly: true);
        }

        return obj;
    }

    private static object ConvertToScribanValue(object value)
    {
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? "",
                JsonValueKind.Number => je.GetInt64(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                _ => je.ToString()
            };
        }
        return value;
    }

    private static Dictionary<string, object?>? ExtractPropsFromScriptObject(ScriptObject so)
    {
        if (!so.ContainsKey("props") || so["props"] is not ScriptObject propsObj) return null;

        var dict = new Dictionary<string, object?>();
        foreach (var key in propsObj.GetMembers())
        {
            dict[key] = propsObj[key] as object;
        }
        return dict.Count > 0 ? dict : null;
    }
}

internal sealed class RenderSectionFunction : IScriptCustomFunction
{
    private readonly SectionRenderHelper _helper;

    public RenderSectionFunction(SectionRenderHelper helper)
    {
        _helper = helper;
    }

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var firstArg = arguments.Count > 0 ? arguments[0] : null;

        if (firstArg is ScriptObject so)
        {
            return _helper.RenderScriptObjectSection(so, _helper.ParentGlobals);
        }

        var json = firstArg?.ToString() ?? "";
        return _helper.render_section(json);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return new ValueTask<object?>(Invoke(context, callerContext, arguments, blockStatement));
    }

    public int RequiredParameterCount => 1;
    public int ParameterCount => 1;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(string), "json");
}

internal sealed class RenderComponentFunction : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var name = arguments.Count > 0 ? arguments[0]?.ToString() ?? "" : "";
        var data = arguments.Count > 1 ? arguments[1] : null;

        if (data is ScriptObject so)
        {
            return RenderComponentWithObject(name, so);
        }

        return $"<!-- component: data is {(data?.GetType().FullName ?? "null")} not ScriptObject -->";
    }

    private static string RenderComponentWithObject(string name, ScriptObject data)
    {
        if (ComponentFunctions.ThemeComponents is null || !ComponentFunctions.ThemeComponents.TryGetValue(name, out var def))
            return $"<!-- component not found: {name} -->";

        var templatePath = !string.IsNullOrEmpty(ComponentFunctions.ThemeRegistryRoot)
            ? Path.Combine(ComponentFunctions.ThemeRegistryRoot, def.Template)
            : def.Template;

        if (!File.Exists(templatePath))
            return $"<!-- component template not found: {def.Template} -->";

        var templateText = File.ReadAllText(templatePath);
        var compTemplate = Template.Parse(templateText);
        if (compTemplate.HasErrors) return $"<!-- component error: {compTemplate.Messages} -->";

        var compContext = new TemplateContext
        {
            TemplateLoader = ComponentFunctions.ThemeTemplateLoader,
            EnableRelaxedMemberAccess = true,
            EnableRelaxedTargetAccess = true,
            EnableNullIndexer = true
        };

        if (ComponentFunctions.ThemeParentGlobals is not null)
            compContext.PushGlobal(ComponentFunctions.ThemeParentGlobals);

        compContext.PushGlobal(data);

        return compTemplate.Render(compContext);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return new ValueTask<object?>(Invoke(context, callerContext, arguments, blockStatement));
    }

    public int RequiredParameterCount => 1;
    public int ParameterCount => 2;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(string), index == 0 ? "name" : "data");
}

public sealed class SectionDataResolverAccessor
{
    internal IReadOnlyList<ContentItem>? AllItems { get; set; }
    internal ThemeComponentRegistry? Registry { get; set; }

    public IReadOnlyList<ContentItem>? ResolveData(PageSectionDefinition sectionDef)
    {
        return null;
    }
}

internal static class ImageHelper
{
    internal static string BuildSrcset(string imagePath, string sizes = "480,768,1200")
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var sizeList = sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var size in sizeList)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"{imagePath}?w={size} {size}w");
        }
        return sb.ToString();
    }

    internal static string BuildImgTag(string src, string alt = "", string sizes = "480,768,1200", string className = "")
    {
        if (string.IsNullOrWhiteSpace(src))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var classAttr = string.IsNullOrWhiteSpace(className) ? "" : $" class=\"{className}\"";
        var sizesAttr = $"(max-width: 480px) 480px, (max-width: 768px) 768px, 1200px";

        sb.Append($"<img src=\"{src}\"");
        sb.Append($" srcset=\"{BuildSrcset(src, sizes)}\"");
        sb.Append($" sizes=\"{sizesAttr}\"");
        if (!string.IsNullOrWhiteSpace(alt))
        {
            sb.Append($" alt=\"{alt}\"");
        }
        if (!string.IsNullOrWhiteSpace(className))
        {
            sb.Append($" class=\"{className}\"");
        }
        sb.Append(" loading=\"lazy\" decoding=\"async\" />");
        return sb.ToString();
    }
}
