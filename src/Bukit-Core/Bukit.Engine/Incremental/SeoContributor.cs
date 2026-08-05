namespace Bukit.Engine.Incremental;

internal sealed class SeoContributor : IRenderDependencyContributor
{
    public string Name => "seo";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var seo = context.Config.Site.Seo;
        writer.AppendLabeledCanonicalValue("seo.enabled", seo.Enabled);
        writer.AppendLabeledCanonicalValue("seo.renderMode", seo.RenderMode);
        writer.AppendLabeledCanonicalValue("seo.homeTitleTemplate", seo.HomeTitleTemplate);
        writer.AppendLabeledCanonicalValue("seo.pageTitleTemplate", seo.PageTitleTemplate);
        writer.AppendLabeledCanonicalValue("seo.titleSeparator", seo.TitleSeparator);
        writer.AppendLabeledCanonicalValue("seo.defaultImage", seo.DefaultImage);
        writer.AppendLabeledCanonicalValue("seo.twitterSite", seo.TwitterSite);
    }
}
