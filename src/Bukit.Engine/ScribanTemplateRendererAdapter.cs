using Bukit.Config;
using Bukit.Rendering;
using Bukit.Rendering.Scriban;

namespace Bukit.Engine;

internal sealed class ScribanTemplateRendererAdapter : ITemplateRenderer
{
    private readonly ScribanTemplateRenderer _inner;

    internal ScribanTemplateRendererAdapter(string layoutsDir, string? parentLayoutsDir = null, IReadOnlyDictionary<string, string>? shortcodes = null, IReadOnlyDictionary<string, ComponentDefinition>? components = null, string? userLayoutsDir = null)
    {
        _inner = new ScribanTemplateRenderer(layoutsDir, parentLayoutsDir, shortcodes, components, userLayoutsDir);
    }

    public string RenderPage(string templateRelativePath, PageModel model) => _inner.RenderPage(templateRelativePath, model);
    public string RenderList(string templateRelativePath, ListPageModel model) => _inner.RenderList(templateRelativePath, model);
}
