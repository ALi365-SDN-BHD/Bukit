using System.Xml.Linq;
using Bukit.Config;
using Bukit.Engine.PublishAuditRules;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class SeoAuditReportWriter
{
    private static void AnalyzeDuplicates(IReadOnlyList<SeoAuditRoute> routes, List<SeoAuditIssue> issues)
    {
        foreach (var group in routes.Where(x => !string.IsNullOrWhiteSpace(x.Title))
                     .GroupBy(x => x.Title!, StringComparer.OrdinalIgnoreCase)
                     .Where(x => HasNonAlternateDuplicate(x.ToArray())))
        {
            foreach (var route in group)
            {
                issues.Add(Warning("seo.title_duplicate", route.Url, $"SEO title is duplicated by {group.Count()} routes."));
            }
        }

        foreach (var group in routes.Where(x => !string.IsNullOrWhiteSpace(x.Description))
                     .GroupBy(x => x.Description!, StringComparer.OrdinalIgnoreCase)
                     .Where(x => HasNonAlternateDuplicate(x.ToArray())))
        {
            foreach (var route in group)
            {
                issues.Add(Warning("seo.description_duplicate", route.Url, $"SEO description is duplicated by {group.Count()} routes."));
            }
        }
    }

    private static bool HasNonAlternateDuplicate(IReadOnlyList<SeoAuditRoute> routes)
    {
        if (routes.Count < 2)
        {
            return false;
        }

        for (var i = 0; i < routes.Count; i++)
        {
            for (var j = i + 1; j < routes.Count; j++)
            {
                if (!AreHreflangAlternates(routes[i], routes[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AreHreflangAlternates(SeoAuditRoute left, SeoAuditRoute right)
    {
        return left.Alternates.Any(x => string.Equals(x.Href, right.Canonical, StringComparison.OrdinalIgnoreCase)) &&
               right.Alternates.Any(x => string.Equals(x.Href, left.Canonical, StringComparison.OrdinalIgnoreCase));
    }

    private static void AnalyzeCanonicalTargets(IReadOnlyList<SeoAuditRoute> routes, List<SeoAuditIssue> issues)
    {
        var noindexCanonicals = routes.Where(x => !x.Indexable).Select(x => x.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var route in routes.Where(x => x.Indexable && noindexCanonicals.Contains(x.Canonical)))
        {
            issues.Add(Error("seo.canonical_points_to_noindex", route.Url, $"Canonical points to a noindex route: {route.Canonical}."));
        }
    }

    private static void AnalyzeHreflang(
        IReadOnlyList<SeoAuditRoute> routes,
        IReadOnlyDictionary<string, (SeoIndexEntry Entry, SeoModel Model)> modelByCanonical,
        List<SeoAuditIssue> issues,
        bool requireTargets)
    {
        foreach (var route in routes.Where(x => x.Alternates.Count > 0))
        {
            if (!route.Alternates.Any(x => string.Equals(x.Href, route.Canonical, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Warning("seo.hreflang_self_missing", route.Url, "hreflang alternates must include the current page canonical URL."));
            }

            if (!route.Alternates.Any(x => string.Equals(x.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Warning("seo.hreflang_x_default_missing", route.Url, "hreflang alternates are missing x-default."));
            }

            foreach (var alternate in route.Alternates)
            {
                if (!IsValidHreflang(alternate.Hreflang))
                {
                    issues.Add(Warning("seo.hreflang_invalid_locale", route.Url, $"Invalid hreflang value: {alternate.Hreflang}."));
                }

                if (string.Equals(alternate.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!modelByCanonical.TryGetValue(alternate.Href, out var target))
                {
                    if (requireTargets)
                    {
                        issues.Add(Warning("seo.hreflang_target_missing", route.Url, $"hreflang target is not part of the generated URL inventory: {alternate.Href}."));
                    }

                    continue;
                }

                var hasReturnLink = target.Model.Alternates.Any(x =>
                    string.Equals(x.Href, route.Canonical, StringComparison.OrdinalIgnoreCase));
                if (!hasReturnLink)
                {
                    issues.Add(Warning("seo.hreflang_return_missing", route.Url, $"hreflang target does not link back to {route.Canonical}."));
                }
            }
        }
    }

    private static void AnalyzeRobotsTxt(string? robotsText, IReadOnlyList<SeoAuditRoute> routes, List<SeoAuditIssue> issues)
    {
        if (robotsText is null)
        {
            return;
        }

        if (!ContainsInvariant(robotsText, "Sitemap:"))
        {
            issues.Add(Warning("seo.robots_txt_sitemap_missing", null, "robots.txt does not declare a Sitemap URL."));
        }

        if (!robotsText.Split('\n').Any(x => x.Trim().Equals("Disallow: /", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        foreach (var route in routes.Where(x => x.Indexable))
        {
            issues.Add(Error("seo.robots_txt_blocks_indexable", route.Url, "robots.txt disallows all crawling while route is indexable."));
        }
    }

    private static void AnalyzeSitemapXml(string? sitemapText, List<SeoAuditIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(sitemapText))
        {
            return;
        }

        try
        {
            var doc = XDocument.Parse(sitemapText, LoadOptions.None);
            var rootName = doc.Root?.Name.LocalName;
            if (rootName is not ("urlset" or "sitemapindex"))
            {
                issues.Add(Error("seo.sitemap_xml_invalid_root", null, $"sitemap.xml root must be urlset or sitemapindex, got {rootName ?? "<none>"}."));
            }
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            issues.Add(Error("seo.sitemap_xml_invalid", null, $"sitemap.xml is not valid XML: {ex.Message}"));
        }
    }

    private static bool IsValidHreflang(string value)
    {
        if (string.Equals(value, "x-default", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 3 ||
            parts[0].Length is < 2 or > 3 ||
            !parts[0].All(char.IsLetter))
        {
            return false;
        }

        if (parts.Length == 1)
        {
            return true;
        }

        var second = parts[1];
        var secondLooksLikeScript = second.Length == 4 && second.All(char.IsLetter);
        var secondLooksLikeRegion = second.Length == 2 && second.All(char.IsLetter);
        if (!secondLooksLikeScript && !secondLooksLikeRegion)
        {
            return false;
        }

        if (parts.Length == 2)
        {
            return true;
        }

        var third = parts[2];
        return secondLooksLikeScript && third.Length == 2 && third.All(char.IsLetter);
    }

    private static bool IsRssContent(AppConfig config, SeoIndexEntry entry)
        => !string.IsNullOrWhiteSpace(entry.ContentType) &&
           config.Site.Collections is not null &&
           config.Site.Collections.TryGetValue(entry.ContentType, out var collection) &&
           collection.Output?.Rss == true;

    private static bool IsJsonFeedContent(AppConfig config, SeoIndexEntry entry)
        => entry.Indexable &&
           config.Site.Feed.Formats.Any(format => string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)) &&
           !string.IsNullOrWhiteSpace(entry.ContentType) &&
           !string.Equals(entry.ContentType, "list", StringComparison.OrdinalIgnoreCase);

    private static void AnalyzePublishDocument(PublishDocument document, List<SeoAuditIssue> issues)
    {
        TrustAuditRules.Analyze(document, issues);
        RepresentationAuditRules.Analyze(document, issues);
    }

    private static void AnalyzePublishDocumentDuplicates(IReadOnlyList<PublishDocument> documents, List<SeoAuditIssue> issues)
    {
        foreach (var group in documents
                     .Where(x => x.Indexable && !string.IsNullOrWhiteSpace(x.ContentRecord?.Presentation.Body))
                     .GroupBy(x => NormalizeBodyForComparison(x.ContentRecord!.Presentation.Body!), StringComparer.OrdinalIgnoreCase)
                     .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1))
        {
            foreach (var document in group)
            {
                issues.Add(Warning("publish.content_duplicate", document.RouteUrl, $"Published content body is duplicated by {group.Count()} routes."));
            }
        }

        foreach (var document in documents.Where(x => x.Indexable))
        {
            var summary = document.Summary ?? document.Description;
            if (string.IsNullOrWhiteSpace(summary) ||
                summary.Length < 24 ||
                string.Equals(summary, document.Title, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Warning("publish.unique_value_missing", document.RouteUrl, "Published content summary is too thin to communicate unique value to machine consumers."));
            }
        }
    }

    private static string NormalizeBodyForComparison(string value)
        => string.Join(' ', value
            .Replace("<", " < ", StringComparison.Ordinal)
            .Replace(">", " > ", StringComparison.Ordinal)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    internal static bool IsAbsoluteHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool HasFragment(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return !string.IsNullOrWhiteSpace(absolute.Fragment);
        }

        return value.Contains('#', StringComparison.Ordinal);
    }

    private static int SeverityRank(string severity)
        => string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    private static bool IsMachineReadabilityIssue(string code)
        => code is "publish.semantic_main_missing"
            or "publish.semantic_article_missing"
            or "publish.semantic_header_missing"
            or "publish.semantic_nav_missing"
            or "publish.semantic_footer_missing"
            or "publish.image_alt_missing"
            or "publish.figure_caption_missing"
            or "publish.heading_h1_missing"
            or "publish.heading_level_skip"
            or "publish.time_missing"
            or "publish.initial_html_unreadable"
            or "publish.jsonld_title_mismatch"
            or "publish.jsonld_description_mismatch"
            or "publish.jsonld_author_mismatch"
            or "publish.jsonld_date_mismatch"
            or "publish.summary_missing"
            or "publish.entity_summary_missing"
            or "publish.sitemap_missing_route"
            or "publish.search_missing_route"
            or "publish.rss_missing_route"
            or "publish.json_feed_missing_route"
            or "publish.manifest_missing_route"
            or "publish.content_duplicate"
            or "publish.unique_value_missing"
            or "publish.ai_crawler_policy_conflict";

    private static bool IsTrustIssue(string code)
        => code is "publish.author_missing"
            or "publish.source_missing"
            or "publish.source_references_missing"
            or "publish.review_status_missing"
            or "publish.updated_at_missing"
            or "publish.entity_missing";

    private static string? ReadOptional(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static bool ContainsInvariant(string? haystack, string needle)
        => haystack?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static SeoAuditIssue Error(string code, string? route, string message) => new("error", code, route, message);

    private static SeoAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);

    private static string BuildMergedKey(string language, string key) => language + "/" + key;

    private static string CombineBaseUrl(string baseUrl, string routeUrl)
    {
        var b = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        var r = routeUrl.StartsWith('/') ? routeUrl : "/" + routeUrl;
        return string.IsNullOrWhiteSpace(b) ? r : b + r;
    }
}
