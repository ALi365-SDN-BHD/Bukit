namespace Bukit.Cli.Commands.SeoInsights;

internal static class SeoInsightsRuleEvaluator
{
    internal const string SnippetMismatch = "seo.insights.snippet_mismatch";
    internal const string LandingQuality = "seo.insights.landing_quality";
    internal const string Discoverability = "seo.insights.discoverability";
    internal const string PositionOpportunity = "seo.insights.position_opportunity";

    internal static IReadOnlyList<SeoInsightsFinding> Evaluate(
        SeoInsightsRoute route,
        SeoInsightsRuleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(profile);

        var findings = new List<SeoInsightsFinding>(4);
        AddSnippetMismatch(findings, route, profile);
        AddLandingQuality(findings, route, profile);
        AddDiscoverability(findings, route, profile);
        AddPositionOpportunity(findings, route, profile);

        return findings
            .OrderBy(finding => PriorityRank(finding.Priority))
            .ThenBy(finding => finding.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddSnippetMismatch(
        ICollection<SeoInsightsFinding> findings,
        SeoInsightsRoute route,
        SeoInsightsRuleProfile profile)
    {
        var metrics = route.Metrics;
        var thresholds = profile.Thresholds;
        if (metrics.Impressions is not { } impressions ||
            metrics.Ctr is not { } ctr ||
            metrics.Sessions is not { } sessions ||
            metrics.EngagementRate is not { } engagementRate ||
            impressions < thresholds.MinimumSearchImpressions ||
            ctr >= thresholds.LowCtr ||
            sessions < thresholds.MinimumAnalyticsSessions ||
            engagementRate < thresholds.HighEngagementRate)
        {
            return;
        }

        findings.Add(new SeoInsightsFinding(
            SnippetMismatch,
            profile.Priorities.SnippetMismatch,
            route.RouteKey,
            [
                Evidence("impressions", impressions, ">=", thresholds.MinimumSearchImpressions),
                Evidence("ctr", ctr, "<", thresholds.LowCtr),
                Evidence("sessions", sessions, ">=", thresholds.MinimumAnalyticsSessions),
                Evidence("engagementRate", engagementRate, ">=", thresholds.HighEngagementRate)
            ],
            "Search presentation may not align with the intent of impressions reaching this route.",
            "Review the title and description against the observed queries before changing content."));
    }

    private static void AddLandingQuality(
        ICollection<SeoInsightsFinding> findings,
        SeoInsightsRoute route,
        SeoInsightsRuleProfile profile)
    {
        var metrics = route.Metrics;
        var thresholds = profile.Thresholds;
        if (metrics.Sessions is not { } sessions ||
            metrics.EngagementRate is not { } engagementRate ||
            sessions < thresholds.MinimumAnalyticsSessions ||
            engagementRate >= thresholds.LowEngagementRate)
        {
            return;
        }

        findings.Add(new SeoInsightsFinding(
            LandingQuality,
            profile.Priorities.LandingQuality,
            route.RouteKey,
            [
                Evidence("sessions", sessions, ">=", thresholds.MinimumAnalyticsSessions),
                Evidence("engagementRate", engagementRate, "<", thresholds.LowEngagementRate)
            ],
            "The landing experience may not align with the needs of organic visitors reaching this route.",
            "Review the landing experience against the observed organic engagement before changing content."));
    }

    private static void AddDiscoverability(
        ICollection<SeoInsightsFinding> findings,
        SeoInsightsRoute route,
        SeoInsightsRuleProfile profile)
    {
        var metrics = route.Metrics;
        var thresholds = profile.Thresholds;
        if (metrics.Impressions is not { } impressions ||
            metrics.Sessions is not { } sessions ||
            metrics.EngagementRate is not { } engagementRate ||
            impressions > thresholds.MaximumLowImpressions ||
            sessions < thresholds.MinimumAnalyticsSessions ||
            engagementRate < thresholds.HighEngagementRate)
        {
            return;
        }

        findings.Add(new SeoInsightsFinding(
            Discoverability,
            profile.Priorities.Discoverability,
            route.RouteKey,
            [
                Evidence("impressions", impressions, "<=", thresholds.MaximumLowImpressions),
                Evidence("sessions", sessions, ">=", thresholds.MinimumAnalyticsSessions),
                Evidence("engagementRate", engagementRate, ">=", thresholds.HighEngagementRate)
            ],
            "This route may provide useful visits despite limited search visibility.",
            "Review indexing signals and relevant query coverage before changing content."));
    }

    private static void AddPositionOpportunity(
        ICollection<SeoInsightsFinding> findings,
        SeoInsightsRoute route,
        SeoInsightsRuleProfile profile)
    {
        var metrics = route.Metrics;
        var thresholds = profile.Thresholds;
        if (metrics.Impressions is not { } impressions ||
            metrics.AveragePosition is not { } averagePosition ||
            impressions < thresholds.MinimumSearchImpressions ||
            averagePosition < thresholds.OpportunityPositionMinimum ||
            averagePosition > thresholds.OpportunityPositionMaximum)
        {
            return;
        }

        findings.Add(new SeoInsightsFinding(
            PositionOpportunity,
            profile.Priorities.PositionOpportunity,
            route.RouteKey,
            [
                Evidence("impressions", impressions, ">=", thresholds.MinimumSearchImpressions),
                Evidence("averagePosition", averagePosition, ">=", thresholds.OpportunityPositionMinimum),
                Evidence("averagePosition", averagePosition, "<=", thresholds.OpportunityPositionMaximum)
            ],
            "This route may have an opportunity to gain visibility within the configured search-position range.",
            "Review the observed queries and competing results before changing content."));
    }

    private static SeoInsightsEvidence Evidence(string metric, long actual, string comparison, long threshold)
        => new(metric, actual, comparison, threshold);

    private static SeoInsightsEvidence Evidence(string metric, double actual, string comparison, double threshold)
        => new(metric, actual, comparison, threshold);

    private static int PriorityRank(string priority)
        => priority switch
        {
            "P0" => 0,
            "P1" => 1,
            "P2" => 2,
            _ => 3
        };
}
