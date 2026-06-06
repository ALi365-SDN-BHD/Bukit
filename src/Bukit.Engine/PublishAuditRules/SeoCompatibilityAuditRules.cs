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
        List<SeoAuditIssue> issues)
    {
        if (!document.Indexable)
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

    private static bool BlocksAiCrawler(string? robotsText, string routeUrl)
    {
        if (string.IsNullOrWhiteSpace(robotsText))
        {
            return false;
        }

        var currentAgents = new List<string>();
        var relevantRules = new List<(bool Allow, string Path)>();
        var groupHasRules = false;
        foreach (var rawLine in robotsText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
            {
                if (groupHasRules)
                {
                    currentAgents.Clear();
                    groupHasRules = false;
                }

                var agent = line["User-agent:".Length..].Trim();
                currentAgents.Add(agent);
                continue;
            }

            if (StartsWithDirective(line, "Allow:", out var allowPath))
            {
                groupHasRules = true;
                if (currentAgents.Any(IsAiRelevantAgent))
                {
                    var normalized = NormalizeRulePath(allowPath);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        relevantRules.Add((true, normalized));
                    }
                }

                continue;
            }

            if (StartsWithDirective(line, "Disallow:", out var disallowPath))
            {
                groupHasRules = true;
                if (currentAgents.Any(IsAiRelevantAgent))
                {
                    var normalized = NormalizeRulePath(disallowPath);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        relevantRules.Add((false, normalized));
                    }
                }
            }
        }

        var routePath = NormalizeRoutePath(routeUrl);
        var matchingRules = relevantRules
            .Where(rule => routePath.StartsWith(rule.Path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(rule => rule.Path.Length)
            .ThenByDescending(rule => rule.Allow)
            .ToArray();
        return matchingRules.Length > 0 && !matchingRules[0].Allow;
    }

    private static bool StartsWithDirective(string line, string directive, out string value)
    {
        if (line.StartsWith(directive, StringComparison.OrdinalIgnoreCase))
        {
            value = line[directive.Length..].Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool IsAiRelevantAgent(string agent)
        => agent == "*" ||
           agent.Equals("GPTBot", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("ChatGPT-User", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("OAI-SearchBot", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("ClaudeBot", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("Claude-Web", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("anthropic-ai", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("PerplexityBot", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("Google-Extended", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("CCBot", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("Bytespider", StringComparison.OrdinalIgnoreCase) ||
           agent.Equals("Amazonbot", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeRoutePath(string routeUrl)
        => string.IsNullOrWhiteSpace(routeUrl) ? "/" : routeUrl.StartsWith('/') ? routeUrl : "/" + routeUrl;

    private static string NormalizeRulePath(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.StartsWith('/') ? value : "/" + value;

    private static SeoAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);
}
