namespace Bukit.Engine.Incremental;

internal sealed class SeoContributor : IRenderDependencyContributor
{
    public string Name => "seo";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var seo = context.Config.Site.Seo;
        writer.AppendUtf8(seo.Enabled.ToString());
        writer.AppendNewline();
        writer.AppendUtf8(seo.RenderMode);
        writer.AppendNewline();
        writer.AppendUtf8(seo.HomeTitleTemplate);
        writer.AppendNewline();
        writer.AppendUtf8(seo.PageTitleTemplate);
        writer.AppendNewline();
        writer.AppendUtf8(seo.TitleSeparator);
        writer.AppendNewline();
        writer.AppendUtf8(seo.DefaultImage);
        writer.AppendNewline();
        writer.AppendUtf8(seo.TwitterSite);
        writer.AppendNewline();
    }
}
