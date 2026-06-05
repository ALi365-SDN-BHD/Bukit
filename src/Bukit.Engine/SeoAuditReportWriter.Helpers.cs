using System.Xml.Linq;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class SeoAuditReportWriter
{
    private static void AnalyzeSemanticHtml(SeoIndexEntry entry, PublishDocument document, string html, List<SeoAuditIssue> issues)
    {
        if (!html.Contains("<main", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("publish.semantic_main_missing", entry.Route.Url, "HTML output is missing a <main> landmark for primary page content."));
        }

        if (!string.Equals(entry.ContentType, "list", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("<article", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("publish.semantic_article_missing", entry.Route.Url, "HTML output is missing an <article> wrapper for page content."));
        }

        var missingAltCount = ImgTagRegex.Matches(html)
            .Select(match => match.Value)
            .Count(tag => !AltAttributeRegex.IsMatch(tag));
        if (missingAltCount > 0)
        {
            issues.Add(Warning("publish.image_alt_missing", entry.Route.Url, $"HTML output contains {missingAltCount} image element(s) without an alt attribute."));
        }

        var headingLevels = HeadingTagRegex.Matches(html)
            .Select(match => int.Parse(match.Groups["level"].Value))
            .ToArray();
        if (!headingLevels.Contains(1))
        {
            issues.Add(Warning("publish.heading_h1_missing", entry.Route.Url, "HTML output is missing an <h1> for the primary page heading."));
        }

        for (var i = 1; i < headingLevels.Length; i++)
        {
            if (headingLevels[i] - headingLevels[i - 1] > 1)
            {
                issues.Add(Warning("publish.heading_level_skip", entry.Route.Url, $"Heading structure skips from h{headingLevels[i - 1]} to h{headingLevels[i]}."));
                break;
            }
        }

        if (RequiresVisibleTime(document) && !TimeDatetimeRegex.IsMatch(html))
        {
            issues.Add(Warning("publish.time_missing", entry.Route.Url, "Dated content is missing a visible <time datetime=\"...\"> element."));
        }

        if (ContainsScriptShellWithoutReadableContent(html))
        {
            issues.Add(Warning("publish.initial_html_unreadable", entry.Route.Url, "Initial HTML does not expose enough readable main content without executing JavaScript."));
        }
    }

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

    private static void AnalyzePublishDocument(PublishDocument document, List<SeoAuditIssue> issues)
    {
        if (!document.Indexable)
        {
            return;
        }

        if (!string.Equals(document.ContentType, "list", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(document.Author))
            {
                issues.Add(Warning("publish.author_missing", document.RouteUrl, "Published content is missing author metadata."));
            }

            if (string.IsNullOrWhiteSpace(document.Source))
            {
                issues.Add(Warning("publish.source_missing", document.RouteUrl, "Published content is missing source/provenance metadata."));
            }

            if (string.IsNullOrWhiteSpace(document.ReviewStatus))
            {
                issues.Add(Warning("publish.review_status_missing", document.RouteUrl, "Published content is missing review status metadata."));
            }

            if (document.EntityNames.Count == 0)
            {
                issues.Add(Warning("publish.entity_missing", document.RouteUrl, "Published content does not declare any entities."));
            }
        }

        if (!document.RepresentationKinds.Contains("html", StringComparer.OrdinalIgnoreCase) ||
            !document.RepresentationKinds.Contains("json", StringComparer.OrdinalIgnoreCase) ||
            !document.RepresentationKinds.Contains("markdown", StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(Error("publish.representation_missing", document.RouteUrl, "Published content is missing one or more required representations (html/json/markdown)."));
        }
    }

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

    private static bool RequiresVisibleTime(PublishDocument document)
    {
        if (string.Equals(document.ContentType, "list", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lifecycle = document.ContentRecord?.Lifecycle;
        return lifecycle is not null && (lifecycle.PublishedAt != default || lifecycle.UpdatedAt is not null);
    }

    private static bool ContainsScriptShellWithoutReadableContent(string html)
    {
        if (!html.Contains("<script", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var primaryContent = MainOrArticleRegex.Matches(html)
            .Select(match => match.Groups["content"].Value)
            .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content));
        if (string.IsNullOrWhiteSpace(primaryContent))
        {
            return false;
        }

        var withoutScripts = StripScriptStyleRegex.Replace(primaryContent, " ");
        var text = CollapseWhitespaceRegex.Replace(StripTagRegex.Replace(withoutScripts, " "), " ").Trim();
        return text.Length < 24;
    }

    private static int SeverityRank(string severity)
        => string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    private static bool IsMachineReadabilityIssue(string code)
        => code is "publish.semantic_main_missing"
            or "publish.semantic_article_missing"
            or "publish.image_alt_missing"
            or "publish.heading_h1_missing"
            or "publish.heading_level_skip"
            or "publish.time_missing"
            or "publish.initial_html_unreadable";

    private static bool IsTrustIssue(string code)
        => code is "publish.author_missing"
            or "publish.source_missing"
            or "publish.review_status_missing"
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
