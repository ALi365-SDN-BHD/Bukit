using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Routing;
using Bukit.Shared;
using Bukit.Theme;
using System.Collections.Concurrent;
using System.Net;
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
    private readonly IReadOnlyDictionary<string, ISectionPlugin>? _sectionPlugins;
    private readonly ConcurrentDictionary<string, CachedSectionTemplate> _sectionTemplateCache = new(StringComparer.OrdinalIgnoreCase);

    public ScribanTemplateRenderer(string layoutsDir, string? parentLayoutsDir = null, IReadOnlyDictionary<string, string>? shortcodes = null, IReadOnlyDictionary<string, ComponentDefinition>? components = null, string? userLayoutsDir = null)
        : this(layoutsDir, parentLayoutsDir, shortcodes, components, userLayoutsDir, null, null, null, "off", null, null)
    {
    }

    public ScribanTemplateRenderer(string layoutsDir, string? parentLayoutsDir, IReadOnlyDictionary<string, string>? shortcodes, IReadOnlyDictionary<string, ComponentDefinition>? components, string? userLayoutsDir, ThemeComponentRegistry? themeRegistry, SectionSchemaValidator? schemaValidator, SectionDataResolverAccessor? dataResolver, string componentValidation, IReadOnlyList<(ContentItem, RouteInfo?)>? allPages = null, IReadOnlyDictionary<string, ISectionPlugin>? sectionPlugins = null)
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
        _sectionPlugins = sectionPlugins;
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
            var componentObj = new ScriptObject();
            componentObj.SetValue("render", new ComponentRenderFunction(_components, _templateLoader, globals, _componentValidation), readOnly: true);
            context.PushGlobal(new ScriptObject { ["comp"] = componentObj });
        }

        if (_themeRegistry is not null)
        {
            var sectionHelpers = new SectionRenderHelper(
                _themeRegistry, _schemaValidator, _componentValidation,
                _templateLoader, globals, _allPages, _sectionPlugins,
                TryGetCachedSectionTemplate);
            var sectionObj = new ScriptObject();
            sectionObj.SetValue("render_section", new RenderSectionFunction(sectionHelpers), readOnly: true);
            context.PushGlobal(sectionObj);

            if (_themeRegistry.Components.Count > 0)
            {
                var compObj = new ScriptObject();
                compObj.SetValue("render", new ThemeComponentRenderFunction(
                    _themeRegistry.Components,
                    _templateLoader,
                    globals,
                    Path.Combine(_themeRegistry.ThemeRoot, "layouts"),
                    _componentValidation,
                    TryGetCachedSectionTemplate), readOnly: true);
                context.PushGlobal(new ScriptObject { ["comp"] = compObj });
            }
        }

        var imageObj = new ScriptObject();
        imageObj.SetValue("srcset", new ImageSrcsetFunction(), readOnly: true);
        imageObj.SetValue("img", new ImageImgFunction(), readOnly: true);
        context.PushGlobal(new ScriptObject { ["image"] = imageObj });

        var utilObj = new ScriptObject();
        utilObj.SetValue("format_date", new Func<string, string, string>((input, format) =>
            ComponentUtilityFunctions.FormatDate(input, format)), readOnly: true);
        utilObj.SetValue("truncate", new Func<string, string, string>((input, maxLen) =>
            int.TryParse(maxLen, out var n) ? ComponentUtilityFunctions.Truncate(input, n) : ComponentUtilityFunctions.Truncate(input)), readOnly: true);
        utilObj.SetValue("titleize", new Func<string, string>(ComponentUtilityFunctions.Titleize), readOnly: true);
        utilObj.SetValue("slugify", new Func<string, string>(ComponentUtilityFunctions.Slugify), readOnly: true);
        context.PushGlobal(new ScriptObject { ["util"] = utilObj });

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
            if (TryFindRenderException(ex, out var renderException))
            {
                throw renderException;
            }

            throw new RenderException($"Render failed: {templateRelativePath}", ex);
        }
    }

    private static bool TryFindRenderException(Exception exception, out RenderException renderException)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is RenderException found)
            {
                renderException = found;
                return true;
            }
        }

        renderException = null!;
        return false;
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

    private readonly record struct SectionFileSignature(DateTime LastWriteTimeUtc, long Length);

    private sealed record CachedSectionTemplate(SectionFileSignature Signature, Template Template);

    private bool TryGetCachedSectionTemplate(string templatePath, out Template template)
    {
        var fileInfo = new FileInfo(templatePath);
        if (!fileInfo.Exists)
        {
            template = null!;
            return false;
        }

        var signature = new SectionFileSignature(fileInfo.LastWriteTimeUtc, fileInfo.Length);
        if (_sectionTemplateCache.TryGetValue(templatePath, out var cached) && cached.Signature.Equals(signature))
        {
            template = cached.Template;
            return true;
        }

        var templateText = File.ReadAllText(templatePath);
        template = Template.Parse(templateText, templatePath);
        if (!template.HasErrors)
        {
            _sectionTemplateCache[templatePath] = new CachedSectionTemplate(signature, template);
        }

        return true;
    }
}

internal sealed class SectionRenderHelper
{
    private readonly ThemeComponentRegistry _themeRegistry;
    private readonly SectionSchemaValidator? _schemaValidator;
    private readonly string _componentValidation;
    private readonly FileTemplateLoader _templateLoader;
    private readonly ScriptObject _parentGlobals;
    private readonly IReadOnlyList<(ContentItem Item, RouteInfo? Route)>? _allPages;
    private readonly IReadOnlyDictionary<string, ISectionPlugin>? _sectionPlugins;
    private readonly GetCachedSectionTemplate _getCachedTemplate;

    internal ScriptObject ParentGlobals => _parentGlobals;

    internal delegate bool GetCachedSectionTemplate(string templatePath, out Template template);

    public SectionRenderHelper(
        ThemeComponentRegistry themeRegistry,
        SectionSchemaValidator? schemaValidator,
        string componentValidation,
        FileTemplateLoader templateLoader,
        ScriptObject parentGlobals,
        IReadOnlyList<(ContentItem, RouteInfo?)>? allPages,
        IReadOnlyDictionary<string, ISectionPlugin>? sectionPlugins = null,
        GetCachedSectionTemplate? getCachedTemplate = null)
    {
        _themeRegistry = themeRegistry;
        _schemaValidator = schemaValidator;
        _componentValidation = componentValidation;
        _templateLoader = templateLoader;
        _parentGlobals = parentGlobals;
        _allPages = allPages;
        _sectionPlugins = sectionPlugins;
        _getCachedTemplate = getCachedTemplate ?? DefaultGetCachedTemplate;
    }

    private static bool DefaultGetCachedTemplate(string templatePath, out Template template)
    {
        if (!File.Exists(templatePath))
        {
            template = null!;
            return false;
        }

        var templateText = File.ReadAllText(templatePath);
        template = Template.Parse(templateText, templatePath);
        return true;
    }

    public string render_section(string jsonInput)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(jsonInput)) return Diagnostic("theme.render_section.empty", "render_section: empty input");

            jsonInput = jsonInput.Trim();
            if (!jsonInput.StartsWith('[') && !jsonInput.StartsWith('{'))
                return Diagnostic("theme.render_section.invalid_json", "render_section: input is not valid JSON");

            var parsed = PageComposer.ParseSections(jsonInput);
            if (parsed.Count == 0) return Diagnostic("theme.render_section.empty", "render_section: no sections parsed");

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
            if (ex is RenderException)
            {
                throw;
            }

            return Diagnostic("theme.render_section.failed", $"render_section error: {ex.Message}");
        }
    }

    public string RenderScriptObjectSection(ScriptObject so, ScriptObject parentGlobals)
    {
        try
        {
            var sectionType = so.ContainsKey("type") ? so["type"]?.ToString() : null;
            if (string.IsNullOrEmpty(sectionType)) return Diagnostic("theme.render_section.missing_type", "render_section: missing type");

            var sectionDef = _themeRegistry.ResolveSection(sectionType);
            if (sectionDef is null) return Diagnostic("theme.section.not_found", $"section not found: {sectionType}");

            var variant = so.ContainsKey("variant") ? so["variant"]?.ToString() : null;
            var templatePath = _themeRegistry.ResolveSectionTemplate(sectionType, variant);
            if (templatePath is null || !File.Exists(templatePath)) return Diagnostic("theme.section.template_not_found", $"section template not found: {sectionType}");

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
            if (ex is RenderException)
            {
                throw;
            }

            return Diagnostic("theme.render_section.failed", $"render_section error: {ex.Message}");
        }
    }

    private string RenderOneSection(PageSectionDefinition sectionDef)
    {
        var sectionType = sectionDef.Type;
        if (string.IsNullOrEmpty(sectionType)) return Diagnostic("theme.render_section.missing_type", "render_section: missing type");

        var themeSection = _themeRegistry.ResolveSection(sectionType);
        if (themeSection is null) return Diagnostic("theme.section.not_found", $"section not found: {sectionType}");

        var templatePath = _themeRegistry.ResolveSectionTemplate(sectionType, sectionDef.Variant);
        if (templatePath is null || !File.Exists(templatePath)) return Diagnostic("theme.section.template_not_found", $"section template not found: {sectionType}");

        return RenderOneSectionBase(sectionDef, templatePath, themeSection, _parentGlobals);
    }

    private string RenderOneSectionBase(PageSectionDefinition sectionDef, string templatePath, ThemeSectionDefinition themeSection, ScriptObject parentGlobals)
    {
        var sectionType = sectionDef.Type;
        var props = sectionDef.Props;

        if (_sectionPlugins is not null && themeSection.Plugin is not null &&
            _sectionPlugins.TryGetValue(themeSection.Plugin, out var plugin) &&
            plugin.SupportedHook == SectionHook.BeforeRender)
        {
            var ctx = new SectionContext
            {
                SectionType = sectionType,
                Variant = sectionDef.Variant,
                Props = props is not null ? new Dictionary<string, object?>(props) : null
            };
            try
            {
                var task = plugin.ExecuteAsync(ctx);
                if (!task.IsCompleted) task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return Diagnostic("theme.section.plugin_failed", $"section plugin error [{plugin.GetType().Name}]: {ex.Message}");
            }
            if (ctx.Props is not null) props = ctx.Props;
        }

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

        if (!_getCachedTemplate(templatePath, out var sectionTemplate))
        {
            return Diagnostic("theme.section.template_not_found", $"section template not found: {sectionType}");
        }

        if (sectionTemplate.HasErrors)
        {
            return Diagnostic("theme.section.template_parse_failed", $"section template error: {sectionType}: {sectionTemplate.Messages}");
        }

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
            sectionGlobals.SetValue(
                "render_component",
                new RenderComponentFunction(
                    _themeRegistry.Components,
                    _templateLoader,
                    parentGlobals,
                    Path.Combine(_themeRegistry.ThemeRoot, "layouts"),
                    _componentValidation,
                    _getCachedTemplate),
                readOnly: true);
        }

        sectionContext.PushGlobal(parentGlobals);

        var html = sectionTemplate.Render(sectionContext);

        if (_sectionPlugins is not null && themeSection.Plugin is not null &&
            _sectionPlugins.TryGetValue(themeSection.Plugin, out var afterPlugin) &&
            afterPlugin.SupportedHook == SectionHook.AfterRender)
        {
            var afterCtx = new SectionContext
            {
                SectionType = sectionType,
                Variant = sectionDef.Variant,
                Props = props,
                RenderedHtml = html
            };
            try
            {
                var task = afterPlugin.ExecuteAsync(afterCtx);
                if (!task.IsCompleted) task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                html = Diagnostic("theme.section.plugin_failed", $"section plugin error [{afterPlugin.GetType().Name}]: {ex.Message}") + "\n" + html;
            }
            if (afterCtx.RenderedHtml is not null) html = afterCtx.RenderedHtml;
        }

        return html;
    }

    private string Diagnostic(string code, string message)
    {
        var diagnostic = $"code={code} {message}";
        if (string.Equals(_componentValidation, "strict", StringComparison.OrdinalIgnoreCase))
        {
            throw new RenderException(diagnostic);
        }

        return $"<!-- {diagnostic} -->";
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
                JsonValueKind.Number when je.TryGetInt64(out var l) => l,
                JsonValueKind.Number => je.GetDouble(),
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
    private readonly ThemeComponentRenderFunction _renderer;

    public RenderComponentFunction(
        IReadOnlyDictionary<string, ThemeComponentDefinition> components,
        FileTemplateLoader templateLoader,
        ScriptObject parentGlobals,
        string registryRoot,
        string componentValidation,
        SectionRenderHelper.GetCachedSectionTemplate? getCachedTemplate = null)
    {
        _renderer = new ThemeComponentRenderFunction(components, templateLoader, parentGlobals, registryRoot, componentValidation, getCachedTemplate);
    }

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var name = arguments.Count > 0 ? arguments[0]?.ToString() ?? "" : "";
        var data = arguments.Count > 1 ? arguments[1] : null;

        if (data is ScriptObject so)
        {
            return _renderer.Render(name, so);
        }

        return $"<!-- component: data is {(data?.GetType().FullName ?? "null")} not ScriptObject -->";
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
        if (!IsSafeImageSource(imagePath))
        {
            return string.Empty;
        }

        var encodedImagePath = WebUtility.HtmlEncode(imagePath.Trim());
        var sb = new StringBuilder();
        var sizeList = sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var size in sizeList)
        {
            if (!int.TryParse(size, out var width) || width <= 0)
            {
                continue;
            }

            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"{encodedImagePath}?w={width} {width}w");
        }
        return sb.ToString();
    }

    internal static string BuildImgTag(string src, string alt = "", string sizes = "480,768,1200", string className = "")
    {
        if (!IsSafeImageSource(src))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var safeSrc = WebUtility.HtmlEncode(src.Trim());
        var safeAlt = WebUtility.HtmlEncode(alt ?? string.Empty);
        var safeClass = WebUtility.HtmlEncode(className ?? string.Empty);
        var sizesAttr = $"(max-width: 480px) 480px, (max-width: 768px) 768px, 1200px";

        sb.Append($"<img src=\"{safeSrc}\"");
        sb.Append($" srcset=\"{BuildSrcset(src, sizes)}\"");
        sb.Append($" sizes=\"{sizesAttr}\"");
        if (!string.IsNullOrWhiteSpace(alt))
        {
            sb.Append($" alt=\"{safeAlt}\"");
        }
        if (!string.IsNullOrWhiteSpace(className))
        {
            sb.Append($" class=\"{safeClass}\"");
        }
        sb.Append(" loading=\"lazy\" decoding=\"async\" />");
        return sb.ToString();
    }

    private static bool IsSafeImageSource(string? src)
    {
        if (string.IsNullOrWhiteSpace(src))
        {
            return false;
        }

        var value = src.Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

internal sealed class ImageSrcsetFunction : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var src = arguments.Count > 0 ? arguments[0]?.ToString() ?? string.Empty : string.Empty;
        var sizes = arguments.Count > 1 ? arguments[1]?.ToString() ?? "480,768,1200" : "480,768,1200";
        return ImageHelper.BuildSrcset(src, sizes);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
        => new(Invoke(context, callerContext, arguments, blockStatement));

    public int RequiredParameterCount => 1;
    public int ParameterCount => 2;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(string), index == 0 ? "src" : "sizes");
}

internal sealed class ImageImgFunction : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var src = arguments.Count > 0 ? arguments[0]?.ToString() ?? string.Empty : string.Empty;
        var alt = arguments.Count > 1 ? arguments[1]?.ToString() ?? string.Empty : string.Empty;
        var sizes = arguments.Count > 2 ? arguments[2]?.ToString() ?? "480,768,1200" : "480,768,1200";
        var className = arguments.Count > 3 ? arguments[3]?.ToString() ?? string.Empty : string.Empty;
        return ImageHelper.BuildImgTag(src, alt, sizes, className);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
        => new(Invoke(context, callerContext, arguments, blockStatement));

    public int RequiredParameterCount => 1;
    public int ParameterCount => 4;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index)
        => new(typeof(string), index switch
        {
            0 => "src",
            1 => "alt",
            2 => "sizes",
            _ => "className"
        });
}
