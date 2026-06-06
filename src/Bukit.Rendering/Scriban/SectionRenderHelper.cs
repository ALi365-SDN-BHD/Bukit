using Scriban;
using Scriban.Runtime;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Theme;
using System.Text;
using System.Text.Json;

namespace Bukit.Rendering.Scriban;

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

        var summary = ContentFieldReader.GetSummary(item.Fields);
        if (!string.IsNullOrWhiteSpace(summary))
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
