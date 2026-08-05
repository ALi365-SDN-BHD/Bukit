using Bukit.Config;

namespace Bukit.Engine.Incremental;

internal sealed class SiteIdentityAndModesContributor : IRenderDependencyContributor
{
    public string Name => "site-identity-and-modes";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var site = context.Config.Site;
        writer.AppendLabeledCanonicalValue("site.name", context.SiteModel.Name);
        writer.AppendLabeledCanonicalValue("site.title", site.Title);
        writer.AppendLabeledCanonicalValue("site.description", site.Description);
        writer.AppendLabeledCanonicalValue("site.baseUrl", site.BaseUrl);
        writer.AppendLabeledCanonicalValue("site.language", site.Language);
        writer.AppendLabeledCanonicalValue("site.url", site.Url);
        writer.AppendLabeledCanonicalValue("site.buildYear", context.SiteModel.BuildYear);
        writer.AppendLabeledCanonicalValue(
            "site.languages",
            site.Languages?.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
        writer.AppendLabeledCanonicalValue("site.defaultLanguage", site.DefaultLanguage);
        writer.AppendLabeledCanonicalValue("site.sitemapMode", site.SitemapMode);
        writer.AppendLabeledCanonicalValue("site.feedMode", SiteModeResolver.ResolveFeedMode(site));
        writer.AppendLabeledCanonicalValue("site.searchMode", SiteModeResolver.ResolveSearchMode(site));
    }
}
