using Bukit.Rendering;
using Bukit.Rendering.Scriban;

namespace Bukit.Engine;

internal sealed class ScribanTemplateRendererAdapter : ITemplateRenderer
{
    private readonly ScribanTemplateRenderer _inner;

    internal ScribanTemplateRendererAdapter(string layoutsDir)
    {
        _inner = new ScribanTemplateRenderer(layoutsDir);
    }

    public string RenderPage(string templateRelativePath, PageModel model) => _inner.RenderPage(templateRelativePath, model);
    public string RenderList(string templateRelativePath, ListPageModel model) => _inner.RenderList(templateRelativePath, model);
}
