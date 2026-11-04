namespace Bukit.Engine.PublishAuditRules;

internal static class SeoCompatibilityAuditRules
{
    /// <summary>
    /// Whether the sitemap writers would list this canonical. Without <c>site.url</c> there is
    /// no sitemap scope, so every canonical counts as in scope.
    /// </summary>
    internal static bool IsInSitemapScope(string? siteUrl, string baseUrl, string? canonical)
        => string.IsNullOrWhiteSpace(siteUrl) || new SitemapLocFilter(siteUrl, baseUrl).IsInScope(canonical);

    internal static void Analyze(
        PublishDocument document,
        bool sitemapIncluded,
        bool sitemapExpected,
        bool searchIncluded,
        bool searchExpected,
        bool rssIncluded,
        bool rssExpected,
        bool atomFeedIncluded,
        bool atomFeedExpected,
        bool jsonFeedIncluded,
        bool jsonFeedExpected,
        bool llmsIncluded,
        bool llmsExpected,
        bool llmsFullIncluded,
        bool llmsFullExpected,
        bool manifestIncluded,
        string? robotsText,
        List<PublishAuditIssue> issues)
    {
        if (!document.Indexable || !PublishDocumentAuditScope.IsContentBacked(document))
        {
            return;
        }

        if (sitemapExpected && !sitemapIncluded)
        {
            issues.Add(Warning("publish.sitemap_missing_route", document.RouteUrl, "Indexable published content is missing from sitemap output."));
        }

        if (searchExpected && !searchIncluded)
        {
            issues.Add(Warning("publish.search_missing_route", document.RouteUrl, "Indexable published content is missing from search index output."));
        }

        if (rssExpected && !rssIncluded)
        {
            issues.Add(Warning("publish.rss_missing_route", document.RouteUrl, "RSS-enabled published content is missing from RSS output."));
        }

        if (atomFeedExpected && !atomFeedIncluded)
        {
            issues.Add(Warning("publish.atom_feed_missing_route", document.RouteUrl, "Atom-enabled published content is missing from Atom output."));
        }

        if (jsonFeedExpected && !jsonFeedIncluded)
        {
            issues.Add(Warning("publish.json_feed_missing_route", document.RouteUrl, "JSON Feed-enabled published content is missing from JSON Feed output."));
        }

        if (llmsExpected && !llmsIncluded)
        {
            issues.Add(Warning("publish.llms_missing_route", document.RouteUrl, "llms.txt-enabled published content is missing from llms.txt output."));
        }

        if (llmsFullExpected && !llmsFullIncluded)
        {
            issues.Add(Warning("publish.llms_full_missing_route", document.RouteUrl, "llms-full.txt-enabled published content is missing from llms-full.txt output."));
        }

        if (!manifestIncluded)
        {
            issues.Add(Warning("publish.manifest_missing_route", document.RouteUrl, "Published content is missing from agent manifest output."));
        }

        if (BlocksAiCrawler(robotsText, document.RouteUrl))
        {
            issues.Add(Warning("publish.ai_crawler_policy_conflict", document.RouteUrl, "AI search crawler policy blocks an indexable published route, so it cannot be cited in AI answers."));
        }
    }

    internal static void AnalyzeRobotsTxt(string? robotsText, IReadOnlyList<SeoAuditRoute> routes, List<SeoAuditIssue> issues)
    {
        if (robotsText is null)
        {
            return;
        }

        if (!robotsText.Contains("Sitemap:", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new("warning", "seo.robots_txt_sitemap_missing", null, "robots.txt does not declare a Sitemap URL."));
        }

        var rules = new RobotsTxtRules(robotsText);
        foreach (var route in routes.Where(x => x.Indexable && rules.IsBlocked("*", x.Url)))
        {
            issues.Add(new("error", "seo.robots_txt_blocks_indexable", route.Url, "robots.txt default crawler policy blocks this indexable route."));
        }
    }

    // Only AI search and user-fetch crawlers decide whether a page can be cited in AI answers.
    // Blocking training crawlers (for example aiBotMode: block-training) is a deliberate choice, not a conflict.
    private static bool BlocksAiCrawler(string? robotsText, string routeUrl)
    {
        var rules = new RobotsTxtRules(robotsText);
        return AiCrawlerCatalog.VisibilityUserAgents.Any(agent => rules.IsBlocked(agent, routeUrl));
    }

    private static PublishAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);
}
