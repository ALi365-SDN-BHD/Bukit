namespace Bukit.Rendering;

public interface ITemplateRenderer
{
    string RenderPage(string templateRelativePath, PageModel model);
    string RenderList(string templateRelativePath, ListPageModel model);
}
