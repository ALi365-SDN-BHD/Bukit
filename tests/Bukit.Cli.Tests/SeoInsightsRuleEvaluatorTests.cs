using System.Reflection;
using System.Text.Json;
using Bukit.Cli.Commands.SeoInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoInsightsRuleEvaluatorTests
{
    [Fact]
    public void Evaluate_EmitsExactlyFourConfiguredFindingsInPriorityThenCodeOrder()
    {
        var profile = Profile(
            thresholds: new SeoInsightsThresholds(100, 100, 10, 0.2, 0.8, 0.4, 4, 12),
            priorities: new SeoInsightsPriorities("P2", "P0", "P1", "P0"));
        var route = Route(new SeoObservationMetrics(100, 10, 4, 0.1, 10, 5, 0, 0.5, 0));

        var findings = SeoInsightsRuleEvaluator.Evaluate(route, profile);

        Assert.Equal(
            [
                "P0|seo.insights.landing_quality",
                "P0|seo.insights.position_opportunity",
                "P1|seo.insights.discoverability",
                "P2|seo.insights.snippet_mismatch"
            ],
            findings.Select(finding => $"{finding.Priority}|{finding.Code}"));
        Assert.Equal(4, findings.Select(finding => finding.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(findings, finding => Assert.Equal("route:alpha", finding.RouteKey));
    }

    [Fact]
    public void Evaluate_SnippetMismatchUsesInclusiveCountsAndEngagementButStrictCtr()
    {
        var profile = Profile();
        var boundary = Route(new SeoObservationMetrics(100, 19, null, 0.199, 10, 7, null, 0.7, null));

        var finding = Assert.Single(SeoInsightsRuleEvaluator.Evaluate(boundary, profile));

        Assert.Equal("seo.insights.snippet_mismatch", finding.Code);
        Assert.Equal("P0", finding.Priority);
        Assert.Equal(
            [
                new SeoInsightsEvidence("impressions", 100, ">=", 100),
                new SeoInsightsEvidence("ctr", 0.199, "<", 0.2),
                new SeoInsightsEvidence("sessions", 10, ">=", 10),
                new SeoInsightsEvidence("engagementRate", 0.7, ">=", 0.7)
            ],
            finding.Evidence);
        Assert.Equal("Search presentation may not align with the intent of impressions reaching this route.", finding.Hypothesis);
        Assert.Equal("Review the title and description against the observed queries before changing content.", finding.SuggestedAction);

        Assert.Empty(SeoInsightsRuleEvaluator.Evaluate(boundary with
        {
            Metrics = boundary.Metrics with { Ctr = 0.2 }
        }, profile));
    }

    [Fact]
    public void Evaluate_LandingQualityUsesInclusiveSessionsButStrictEngagement()
    {
        var profile = Profile();
        var boundary = Route(new SeoObservationMetrics(null, null, null, null, 10, 2, null, 0.299, null));

        var finding = Assert.Single(SeoInsightsRuleEvaluator.Evaluate(boundary, profile));

        Assert.Equal("seo.insights.landing_quality", finding.Code);
        Assert.Equal(
            [
                new SeoInsightsEvidence("sessions", 10, ">=", 10),
                new SeoInsightsEvidence("engagementRate", 0.299, "<", 0.3)
            ],
            finding.Evidence);
        Assert.Empty(SeoInsightsRuleEvaluator.Evaluate(boundary with
        {
            Metrics = boundary.Metrics with { EngagementRate = 0.3 }
        }, profile));
    }

    [Fact]
    public void Evaluate_DiscoverabilityUsesInclusiveLowImpressionsSessionsAndEngagement()
    {
        var profile = Profile();
        var boundary = Route(new SeoObservationMetrics(20, 1, null, 0.05, 10, 7, null, 0.7, null));

        var finding = Assert.Single(SeoInsightsRuleEvaluator.Evaluate(boundary, profile));

        Assert.Equal("seo.insights.discoverability", finding.Code);
        Assert.Equal(
            [
                new SeoInsightsEvidence("impressions", 20, "<=", 20),
                new SeoInsightsEvidence("sessions", 10, ">=", 10),
                new SeoInsightsEvidence("engagementRate", 0.7, ">=", 0.7)
            ],
            finding.Evidence);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(12)]
    public void Evaluate_PositionOpportunityUsesInclusiveConfiguredInterval(double position)
    {
        var profile = Profile();
        var route = Route(new SeoObservationMetrics(100, 10, position, 0.1, null, null, null, null, null));

        var finding = Assert.Single(SeoInsightsRuleEvaluator.Evaluate(route, profile));

        Assert.Equal("seo.insights.position_opportunity", finding.Code);
        Assert.Equal(
            [
                new SeoInsightsEvidence("impressions", 100, ">=", 100),
                new SeoInsightsEvidence("averagePosition", position, ">=", 4),
                new SeoInsightsEvidence("averagePosition", position, "<=", 12)
            ],
            finding.Evidence);
    }

    [Fact]
    public void Evaluate_PositionOpportunitySuppressesValuesOutsideConfiguredInterval()
    {
        var profile = Profile();

        Assert.Empty(SeoInsightsRuleEvaluator.Evaluate(
            Route(new SeoObservationMetrics(100, 10, 3.999, 0.1, null, null, null, null, null)), profile));
        Assert.Empty(SeoInsightsRuleEvaluator.Evaluate(
            Route(new SeoObservationMetrics(100, 10, 12.001, 0.1, null, null, null, null, null)), profile));
    }

    [Fact]
    public void Evaluate_SuppressesEachRuleWhenAnyRequiredMetricIsMissing()
    {
        var profile = Profile(
            thresholds: new SeoInsightsThresholds(100, 100, 10, 0.2, 0.8, 0.4, 4, 12));
        var complete = new SeoObservationMetrics(100, 10, 4, 0.1, 10, 5, 0, 0.5, 0);
        var cases = new (string Code, SeoObservationMetrics Metrics)[]
        {
            ("seo.insights.snippet_mismatch", complete with { Ctr = null }),
            ("seo.insights.landing_quality", complete with { Sessions = null }),
            ("seo.insights.discoverability", complete with { EngagementRate = null }),
            ("seo.insights.position_opportunity", complete with { AveragePosition = null })
        };

        foreach (var (code, metrics) in cases)
        {
            Assert.DoesNotContain(SeoInsightsRuleEvaluator.Evaluate(Route(metrics), profile), finding => finding.Code == code);
        }
    }

    [Fact]
    public void Evaluate_UsesFixedCautiousMessagesWithoutRootCauseOrAutomaticEdits()
    {
        var profile = Profile(
            thresholds: new SeoInsightsThresholds(100, 100, 10, 0.2, 0.8, 0.4, 4, 12));
        var findings = SeoInsightsRuleEvaluator.Evaluate(
            Route(new SeoObservationMetrics(100, 10, 4, 0.1, 10, 5, 0, 0.5, 0)), profile);

        Assert.All(findings, finding =>
        {
            var text = $"{finding.Hypothesis} {finding.SuggestedAction}";
            Assert.DoesNotContain("caused", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("proved", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("automatically", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("may", finding.Hypothesis, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("Review ", finding.SuggestedAction, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Assemble_WithProfileAddsFindingsWhileCompatibilityOverloadAddsEmptyArrays()
    {
        var matcher = CreateMatcher();
        var datasets = Datasets();
        var withProfile = SeoInsightsReportWriter.Assemble(matcher, datasets, Profile());
        var withoutProfile = SeoInsightsReportWriter.Assemble(matcher, datasets);

        Assert.Equal("seo.insights.snippet_mismatch", Assert.Single(Assert.Single(withProfile.Routes).Findings).Code);
        Assert.Empty(Assert.Single(withoutProfile.Routes).Findings);

        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(withProfile, SeoInsightsJsonContext.Default.SeoInsightsReport));
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("routes")[0].GetProperty("findings").ValueKind);
    }

    private static SeoInsightsRuleProfile Profile(
        SeoInsightsThresholds? thresholds = null,
        SeoInsightsPriorities? priorities = null)
        => new(
            "https://bukit.dev/schemas/seo-insights-rules.v1.json",
            "1.0",
            "example.com",
            [],
            [],
            thresholds ?? new SeoInsightsThresholds(100, 20, 10, 0.2, 0.3, 0.7, 4, 12),
            priorities ?? new SeoInsightsPriorities("P0", "P1", "P2", "P1"));

    private static SeoInsightsRoute Route(SeoObservationMetrics metrics)
        => new("route:alpha", "content:alpha", "/alpha/", "/alpha/", metrics, []);

    private static IReadOnlyList<SeoObservationDataset> Datasets()
    {
        var window = new SeoObservationWindow(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), "UTC");
        return
        [
            new SeoObservationDataset(
                "https://bukit.dev/schemas/seo-observation.v1.json",
                "1.0",
                "google-search-console",
                "google-organic",
                DateTimeOffset.Parse("2026-08-03T01:00:00Z"),
                window,
                [new SeoObservationRow("https://example.com/alpha/", 100, 10, 2, null, null, null)]),
            new SeoObservationDataset(
                "https://bukit.dev/schemas/seo-observation.v1.json",
                "1.0",
                "google-analytics-4",
                "google-organic",
                DateTimeOffset.Parse("2026-08-03T02:00:00Z"),
                window,
                [new SeoObservationRow("https://example.com/alpha/", null, null, null, 10, 7, 1)])
        ];
    }

    private static SeoObservationRouteMatcher CreateMatcher()
    {
        var engineAssembly = Assembly.Load("Bukit.Engine");
        var entryType = engineAssembly.GetType("Bukit.Engine.SeoRouteMapEntry", throwOnError: true)!;
        var entryConstructor = entryType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 10);
        var entries = Array.CreateInstance(entryType, 1);
        entries.SetValue(entryConstructor.Invoke(
            ["route:alpha", "content:alpha", "/alpha/", "/alpha/", null, null, null, true, null, null]), 0);

        var mapType = engineAssembly.GetType("Bukit.Engine.SeoRouteMap", throwOnError: true)!;
        var mapConstructor = mapType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 6);
        var routeMap = mapConstructor.Invoke(
            ["https://bukit.dev/schemas/seo-route-map.v1.json", "1.0", DateTimeOffset.Parse("2026-08-03T00:00:00Z"), "https://example.com", "/", entries]);
        var matcherConstructor = typeof(SeoObservationRouteMatcher).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (SeoObservationRouteMatcher)matcherConstructor.Invoke(
            [routeMap, new SeoObservationUrlOptions("example.com", new HashSet<string>(), new HashSet<string>())]);
    }
}
