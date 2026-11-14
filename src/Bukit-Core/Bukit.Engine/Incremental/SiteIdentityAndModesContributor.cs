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
        // A single-language site hashes its language through site.language only, so writing
        // languages: [en] for a site that had language: en (or adding defaultLanguage: en) keeps
        // the hash, and upgrading such a site does not render every page again.
        // The cleaned list (trimmed, without blanks or duplicates) is what the build uses.
        var languages = I18nLanguagePolicy.GetLanguages(site);
        var multilingual = languages.Count > 0;
        writer.AppendLabeledCanonicalValue(
            "site.languages",
            multilingual ? languages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList() : null);
        writer.AppendLabeledCanonicalValue("site.defaultLanguage", multilingual ? site.DefaultLanguage : null);
        // Only a multilingual site that moves its default language into a subdirectory hashes
        // the setting; the default (root) layout and single-language sites leave the hash unchanged.
        if (site.DefaultLanguageInSubdir && multilingual)
        {
            writer.AppendLabeledCanonicalValue("site.defaultLanguageInSubdir", true);
        }

        if (!site.DefaultLanguageRedirect)
        {
            writer.AppendLabeledCanonicalValue("site.defaultLanguageRedirect", false);
        }

        writer.AppendLabeledCanonicalValue("site.sitemapMode", site.SitemapMode);
        writer.AppendLabeledCanonicalValue("site.feedMode", SiteModeResolver.ResolveFeedMode(site));
        writer.AppendLabeledCanonicalValue("site.searchMode", SiteModeResolver.ResolveSearchMode(site));
    }
}
