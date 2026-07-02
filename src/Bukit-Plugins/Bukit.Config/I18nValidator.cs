using Bukit.Shared;
using System.Text.RegularExpressions;

namespace Bukit.Config;

internal static class I18nValidator
{
    internal static void ValidateSite(SiteConfig site)
    {
        if (string.IsNullOrWhiteSpace(site.Name))
        {
            throw new ConfigException("site.name is required.");
        }

        if (string.IsNullOrWhiteSpace(site.Title))
        {
            throw new ConfigException("site.title is required.");
        }

        if (!string.IsNullOrWhiteSpace(site.Url) &&
            !(site.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
              site.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConfigException("site.url must start with http:// or https:// when set.");
        }

        if (site.AutoSummaryMaxLength <= 0 || site.AutoSummaryMaxLength > 5000)
        {
            throw new ConfigException("site.autoSummaryMaxLength must be between 1 and 5000.");
        }

        if (string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            throw new ConfigException("site.baseUrl is required.");
        }

        if (!site.BaseUrl.StartsWith('/'))
        {
            throw new ConfigException("site.baseUrl must start with '/'.");
        }

        var outputPathEncoding = (site.OutputPathEncoding ?? "none").Trim().ToLowerInvariant();
        if (outputPathEncoding is not ("none" or "slug" or "urlencode" or "sanitize"))
        {
            throw new ConfigException("site.outputPathEncoding must be none|slug|urlencode|sanitize.");
        }

        if (site.Languages is { Count: > 0 } languages)
        {
            var cleaned = languages.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            if (cleaned.Count == 0)
            {
                throw new ConfigException("site.languages must contain at least one language.");
            }

            var dup = cleaned.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (dup is not null)
            {
                throw new ConfigException($"site.languages has duplicate language: {dup.Key}");
            }

            var defaultLang = string.IsNullOrWhiteSpace(site.DefaultLanguage) ? cleaned[0] : site.DefaultLanguage.Trim();
            if (!cleaned.Contains(defaultLang, StringComparer.OrdinalIgnoreCase))
            {
                throw new ConfigException("site.defaultLanguage must be included in site.languages.");
            }
        }

        var sitemapMode = (site.SitemapMode ?? "split").Trim().ToLowerInvariant();
        if (sitemapMode is not ("split" or "merged" or "index"))
        {
            throw new ConfigException("site.sitemapMode must be split|merged|index.");
        }

        var rssMode = SiteModeResolver.ResolveFeedMode(site);
        if (rssMode is not ("split" or "merged"))
        {
            throw new ConfigException("site.feed configuration produced an invalid feed mode; expected split|merged.");
        }

        var searchMode = SiteModeResolver.ResolveSearchMode(site);
        if (searchMode is not ("split" or "merged" or "index"))
        {
            throw new ConfigException("site.search.mode must be split|merged|index.");
        }

        var seoRenderMode = (site.Seo.RenderMode ?? "inject").Trim().ToLowerInvariant();
        if (seoRenderMode is not ("theme" or "inject" or "off"))
        {
            throw new ConfigException("site.seo.renderMode must be theme|inject|off.");
        }

        var seoDiagnostics = (site.Seo.Diagnostics ?? "warn").Trim().ToLowerInvariant();
        if (seoDiagnostics is not ("off" or "warn" or "strict"))
        {
            throw new ConfigException("site.seo.diagnostics must be off|warn|strict.");
        }

        var geoAiBotMode = (site.Seo.Geo.AiBotMode ?? "allow").Trim().ToLowerInvariant();
        if (geoAiBotMode is not ("allow" or "block" or "selective"))
        {
            throw new ConfigException("site.seo.geo.aiBotMode must be allow|block|selective.");
        }

        if (!string.IsNullOrWhiteSpace(site.Analytics.GoogleAnalyticsId) &&
            !Regex.IsMatch(site.Analytics.GoogleAnalyticsId.Trim(), "^G-[A-Z0-9]+$", RegexOptions.CultureInvariant))
        {
            throw new ConfigException("site.analytics.googleAnalyticsId must be a GA4 id starting with G-.");
        }

        var pluginFailMode = (site.PluginFailMode ?? "strict").Trim().ToLowerInvariant();
        if (pluginFailMode is not ("strict" or "warn"))
        {
            throw new ConfigException("site.pluginFailMode must be strict|warn.");
        }

        var deriveConflictPolicy = (site.DeriveConflictPolicy ?? "fail").Trim().ToLowerInvariant();
        if (deriveConflictPolicy is not ("fail" or "warn" or "last-wins"))
        {
            throw new ConfigException("site.deriveConflictPolicy must be fail|warn|last-wins.");
        }

        if (site.Plugins is not null)
        {
            foreach (var kv in site.Plugins)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                {
                    throw new ConfigException("site.plugins keys must be non-empty strings.");
                }
            }
        }
    }
}
