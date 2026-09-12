namespace Bukit.Engine.PublishAuditRules;

internal static class SeoCompatibilityAuditRules
{
    internal static void Analyze(
        PublishDocument document,
        bool sitemapIncluded,
        bool searchIncluded,
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

        if (!sitemapIncluded)
        {
            issues.Add(Warning("publish.sitemap_missing_route", document.RouteUrl, "Indexable published content is missing from sitemap output."));
        }

        if (!searchIncluded)
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
            issues.Add(Warning("publish.ai_crawler_policy_conflict", document.RouteUrl, "AI crawler policy blocks an indexable published route."));
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

    private static readonly string[] AiAgents =
    [
        "GPTBot", "ChatGPT-User", "OAI-SearchBot", "ClaudeBot", "Claude-Web",
        "anthropic-ai", "PerplexityBot", "Google-Extended", "CCBot", "Bytespider",
        "Amazonbot", "Cohere-AI", "Diffbot", "FacebookBot"
    ];

    private static bool BlocksAiCrawler(string? robotsText, string routeUrl)
    {
        var rules = new RobotsTxtRules(robotsText);
        return AiAgents.Any(agent => rules.IsBlocked(agent, routeUrl));
    }

    private static PublishAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);
}
