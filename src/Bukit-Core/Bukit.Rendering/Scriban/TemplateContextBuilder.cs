using Scriban;
using Scriban.Runtime;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Rendering.Scriban;

internal sealed class TemplateContextBuilder
{
    private readonly FileTemplateLoader _templateLoader;
    private readonly IReadOnlyDictionary<string, string>? _shortcodes;
    private readonly IReadOnlyDictionary<string, ComponentDefinition>? _components;
    private readonly ThemeComponentRegistry? _themeRegistry;
    private readonly SectionSchemaValidator? _schemaValidator;
    private readonly string _componentValidation;
    private readonly IReadOnlyList<(ContentDocument Document, RouteInfo? Route)>? _allPages;
    private readonly IReadOnlyDictionary<string, ISectionPlugin>? _sectionPlugins;
    private readonly SectionRenderHelper.GetCachedSectionTemplate _getCachedSectionTemplate;
    private readonly IReadOnlyList<ITemplateContextContributor> _contributors;

    public TemplateContextBuilder(
        FileTemplateLoader templateLoader,
        IReadOnlyDictionary<string, string>? shortcodes,
        IReadOnlyDictionary<string, ComponentDefinition>? components,
        ThemeComponentRegistry? themeRegistry,
        SectionSchemaValidator? schemaValidator,
        string componentValidation,
        IReadOnlyList<(ContentDocument Document, RouteInfo? Route)>? allPages,
        IReadOnlyDictionary<string, ISectionPlugin>? sectionPlugins,
        SectionRenderHelper.GetCachedSectionTemplate getCachedSectionTemplate,
        IReadOnlyList<ITemplateContextContributor>? contributors = null)
    {
        _templateLoader = templateLoader;
        _shortcodes = shortcodes;
        _components = components;
        _themeRegistry = themeRegistry;
        _schemaValidator = schemaValidator;
        _componentValidation = componentValidation;
        _allPages = allPages;
        _sectionPlugins = sectionPlugins;
        _getCachedSectionTemplate = getCachedSectionTemplate;
        _contributors = contributors ?? Array.Empty<ITemplateContextContributor>();
    }

    public TemplateContext BuildContext(ScriptObject globals)
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
                _getCachedSectionTemplate);
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
                    _getCachedSectionTemplate), readOnly: true);
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

        // Run all registered contributors before pushing model globals.
        // This lets Core internals inject custom template functions/objects without
        // modifying this builder.
        foreach (var contributor in _contributors)
        {
            contributor.Contribute(context, globals);
        }

        context.PushGlobal(globals);

        return context;
    }
}
