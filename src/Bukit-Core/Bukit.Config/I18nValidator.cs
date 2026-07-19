using Bukit.Shared;
using System.Globalization;
using System.Net;
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

        if (site.Search.MaxContentLength <= 0)
        {
            throw new ConfigException(
                "site.search.maxContentLength must be positive.",
                DiagnosticCode.ConfigInvalidValue);
        }

        ValidateSearchRoute(site.Search.Route);

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

        SeoTitleTemplateValidator.Validate(site.Seo);

        var geoAiBotMode = (site.Seo.Geo.AiBotMode ?? "allow").Trim().ToLowerInvariant();
        if (geoAiBotMode is not ("allow" or "block" or "selective"))
        {
            throw new ConfigException("site.seo.geo.aiBotMode must be allow|block|selective.");
        }

        ValidateAnalytics(site.Analytics);

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

    private static void ValidateAnalytics(AnalyticsConfig analytics)
    {
        var providerKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < analytics.Providers.Count; index++)
        {
            var provider = analytics.Providers[index];
            var path = $"site.analytics.providers[{index}]";
            var key = provider.Type switch
            {
                "google-analytics" => ValidateGoogleAnalytics(provider, path),
                "google-tag-manager" => ValidateGoogleTagManager(provider, path),
                "plausible" => ValidatePlausible(provider, path),
                "umami" => ValidateUmami(provider, path),
                _ => throw new ConfigException(
                    $"{path}.type must be google-analytics|google-tag-manager|plausible|umami.",
                    DiagnosticCode.ConfigInvalidValue)
            };

            if (!providerKeys.Add(key))
            {
                throw new ConfigException(
                    $"{path} has a duplicate provider key.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private static string ValidateGoogleAnalytics(AnalyticsProviderConfig provider, string path)
    {
        RequireOnlyProviderFields(provider, path, measurementId: true);
        if (string.IsNullOrEmpty(provider.MeasurementId))
        {
            throw new ConfigException($"{path}.measurementId is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (!Regex.IsMatch(provider.MeasurementId, "^G-[A-Z0-9]+$", RegexOptions.CultureInvariant))
        {
            throw new ConfigException($"{path}.measurementId must match ^G-[A-Z0-9]+$.", DiagnosticCode.ConfigInvalidValue);
        }

        return $"google-analytics:{provider.MeasurementId}";
    }

    private static string ValidateGoogleTagManager(AnalyticsProviderConfig provider, string path)
    {
        RequireOnlyProviderFields(provider, path, containerId: true);
        if (string.IsNullOrEmpty(provider.ContainerId))
        {
            throw new ConfigException($"{path}.containerId is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (!Regex.IsMatch(provider.ContainerId, "^GTM-[A-Z0-9]+$", RegexOptions.CultureInvariant))
        {
            throw new ConfigException($"{path}.containerId must match ^GTM-[A-Z0-9]+$.", DiagnosticCode.ConfigInvalidValue);
        }

        return $"google-tag-manager:{provider.ContainerId}";
    }

    private static string ValidatePlausible(AnalyticsProviderConfig provider, string path)
    {
        RequireOnlyProviderFields(provider, path, domain: true, scriptUrl: true);
        if (string.IsNullOrEmpty(provider.Domain))
        {
            throw new ConfigException($"{path}.domain is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var asciiDomain = NormalizeDnsDomain(provider.Domain, $"{path}.domain");
        if (provider.ScriptUrl is not null)
        {
            ValidateScriptUrl(provider.ScriptUrl, $"{path}.scriptUrl");
        }

        return $"plausible:{asciiDomain}";
    }

    private static string ValidateUmami(AnalyticsProviderConfig provider, string path)
    {
        RequireOnlyProviderFields(provider, path, websiteId: true, scriptUrl: true);
        if (string.IsNullOrEmpty(provider.WebsiteId))
        {
            throw new ConfigException($"{path}.websiteId is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        if (!Guid.TryParseExact(provider.WebsiteId, "D", out var websiteId))
        {
            throw new ConfigException($"{path}.websiteId must be a UUID.", DiagnosticCode.ConfigInvalidValue);
        }

        if (provider.ScriptUrl is null)
        {
            throw new ConfigException($"{path}.scriptUrl is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        ValidateScriptUrl(provider.ScriptUrl, $"{path}.scriptUrl");
        return $"umami:{websiteId:D}";
    }

    private static void RequireOnlyProviderFields(
        AnalyticsProviderConfig provider,
        string path,
        bool measurementId = false,
        bool containerId = false,
        bool domain = false,
        bool websiteId = false,
        bool scriptUrl = false)
    {
        RejectProviderField(provider.MeasurementId, measurementId, $"{path}.measurementId");
        RejectProviderField(provider.ContainerId, containerId, $"{path}.containerId");
        RejectProviderField(provider.Domain, domain, $"{path}.domain");
        RejectProviderField(provider.WebsiteId, websiteId, $"{path}.websiteId");
        RejectProviderField(provider.ScriptUrl, scriptUrl, $"{path}.scriptUrl");
    }

    private static void RejectProviderField(string? value, bool allowed, string path)
    {
        if (!allowed && value is not null)
        {
            throw new ConfigException($"{path} is not allowed for this provider type.", DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static string NormalizeDnsDomain(string? domain, string path)
    {
        if (string.IsNullOrWhiteSpace(domain) || domain != domain.Trim() ||
            domain.IndexOfAny([':', '/', '\\', '?', '#', '@']) >= 0 ||
            IPAddress.TryParse(domain, out _))
        {
            throw new ConfigException($"{path} must be a DNS host name.", DiagnosticCode.ConfigInvalidValue);
        }

        string asciiDomain;
        try
        {
            asciiDomain = new IdnMapping().GetAscii(domain).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            throw new ConfigException($"{path} must be a DNS host name.", DiagnosticCode.ConfigInvalidValue);
        }

        if (asciiDomain.Length > 253 ||
            Uri.CheckHostName(asciiDomain) != UriHostNameType.Dns ||
            asciiDomain.StartsWith(".", StringComparison.Ordinal) ||
            asciiDomain.EndsWith(".", StringComparison.Ordinal) ||
            asciiDomain.Split('.').Any(label =>
                label.Length is < 1 or > 63 ||
                label.StartsWith("-", StringComparison.Ordinal) ||
                label.EndsWith("-", StringComparison.Ordinal)))
        {
            throw new ConfigException($"{path} must be a DNS host name.", DiagnosticCode.ConfigInvalidValue);
        }

        return asciiDomain;
    }

    private static void ValidateScriptUrl(string scriptUrl, string path)
    {
        if (scriptUrl != scriptUrl.Trim() ||
            !Uri.TryCreate(scriptUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !uri.IsDefaultPort ||
            !uri.AbsolutePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigException($"{path} must be an absolute HTTPS .js URL without credentials or a fragment.", DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static void ValidateSearchRoute(string? route)
    {
        if (route is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(route))
        {
            throw new ConfigException("site.search.route must be a non-empty internal URL path when set.", DiagnosticCode.ConfigInvalidValue);
        }

        if (route.Any(char.IsControl))
        {
            throw new ConfigException("site.search.route must not contain control characters.", DiagnosticCode.ConfigInvalidValue);
        }

        var value = route.Trim();
        if (!value.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ConfigException("site.search.route must start with '/'.", DiagnosticCode.ConfigInvalidValue);
        }

        if (value.Contains("//", StringComparison.Ordinal) || value.Contains("://", StringComparison.Ordinal))
        {
            throw new ConfigException("site.search.route must be an internal URL path.", DiagnosticCode.ConfigInvalidValue);
        }

        if (value.Contains('\\'))
        {
            throw new ConfigException("site.search.route must not contain backslashes.", DiagnosticCode.ConfigInvalidValue);
        }

        if (value.Contains('?') || value.Contains('#'))
        {
            throw new ConfigException("site.search.route must not contain query strings or fragments.", DiagnosticCode.ConfigInvalidValue);
        }

        if (value.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new ConfigException("site.search.route must not contain path traversal segments.", DiagnosticCode.ConfigInvalidValue);
        }
    }
}
