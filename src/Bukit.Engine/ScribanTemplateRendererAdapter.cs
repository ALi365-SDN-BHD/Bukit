using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Rendering.Scriban;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Theme;

namespace Bukit.Engine;

internal sealed class ScribanTemplateRendererAdapter : ITemplateRenderer
{
    private readonly ScribanTemplateRenderer _inner;

    internal ScribanTemplateRendererAdapter(string layoutsDir, string? parentLayoutsDir = null, IReadOnlyDictionary<string, string>? shortcodes = null, IReadOnlyDictionary<string, ComponentDefinition>? components = null, string? userLayoutsDir = null)
    {
        _inner = new ScribanTemplateRenderer(layoutsDir, parentLayoutsDir, shortcodes, components, userLayoutsDir);
    }

    internal ScribanTemplateRendererAdapter(string layoutsDir, string? parentLayoutsDir, IReadOnlyDictionary<string, string>? shortcodes, IReadOnlyDictionary<string, ComponentDefinition>? components, string? userLayoutsDir, ThemeComponentRegistry? registry, SectionSchemaValidator? validator, SectionDataResolverAccessor? dataResolver, string componentValidation, IReadOnlyList<(ContentDocument, RouteInfo?)>? allPages = null, IReadOnlyDictionary<string, ISectionPlugin>? sectionPlugins = null)
    {
        _inner = new ScribanTemplateRenderer(layoutsDir, parentLayoutsDir, shortcodes, components, userLayoutsDir, registry, validator, dataResolver, componentValidation, allPages, sectionPlugins);
    }

    public string RenderPage(string templateRelativePath, PageModel model) => _inner.RenderPage(templateRelativePath, model);
    public string RenderList(string templateRelativePath, ListPageModel model) => _inner.RenderList(templateRelativePath, model);
}
