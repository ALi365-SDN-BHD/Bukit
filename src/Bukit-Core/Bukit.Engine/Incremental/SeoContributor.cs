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
        // Inject mode adds Markdown/JSON alternate links to content pages only while GEO is enabled.
        writer.AppendLabeledCanonicalValue("seo.geo.enabled", seo.Geo.Enabled);

        // These fields change the injected head or JSON-LD even when the page body and template
        // are unchanged. Keep them in the hash so incremental builds do not keep stale metadata.
        writer.AppendLabeledCanonicalValue("seo.schema.webPage", seo.Schema.WebPage);
        writer.AppendLabeledCanonicalValue("seo.schema.collectionPage", seo.Schema.CollectionPage);
        writer.AppendLabeledCanonicalValue("seo.schema.searchAction", seo.Schema.SearchAction);
        writer.AppendLabeledCanonicalValue("site.search.route", context.Config.Site.Search.Route);

        var organization = seo.Organization;
        writer.AppendLabeledCanonicalValue("seo.organization.type", organization?.Type);
        writer.AppendLabeledCanonicalValue("seo.organization.name", organization?.Name);
        writer.AppendLabeledCanonicalValue("seo.organization.url", organization?.Url);
        writer.AppendLabeledCanonicalValue("seo.organization.logo", organization?.Logo);
        writer.AppendLabeledCanonicalValue("seo.organization.sameAs", organization?.SameAs);
    }
}
