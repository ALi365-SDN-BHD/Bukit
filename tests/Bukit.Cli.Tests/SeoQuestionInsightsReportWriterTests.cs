using System.Text.Json;
using Bukit.Cli.Commands.SeoInsights;
using Bukit.Cli.Commands.SeoQuestionInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoQuestionInsightsReportWriterTests
{
    private const string QuestionKey = "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OtherQuestionKey = "question:sha256:1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string TopicKey = "topic:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string RouteKeyA = "route:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RouteKeyB = "route:sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string RouteKeyMissing = "route:sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string RouteKeyDup1 = "route:sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string RouteKeyDup2 = "route:sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private static readonly DateTimeOffset GeneratedAt = DateTimeOffset.Parse("2026-08-05T00:00:00Z");

    [Fact]
    public void Assemble_MatchedTargetAndObservations_AggregateDescriptiveMetrics()
    {
        var report = SeoQuestionInsightsAssembler.Assemble(
            RouteMapPath(),
            TargetMap([(QuestionKey, [RouteKeyA])]),
            [
                ("observations/gsc-questions.json", Dataset(
                    (QuestionKey, "https://example.com/a/", 10, 2, 4.0),
                    (QuestionKey, "https://example.com/a/", 30, 4, 2.0)))
            ],
            Options(),
            GeneratedAt);

        var question = Assert.Single(report.Questions);
        Assert.Equal(QuestionKey, question.QuestionKey);
        Assert.Equal(40, question.TotalImpressions);
        Assert.Equal(6, question.TotalClicks);
        var coverage = Assert.Single(question.Routes);
        Assert.Equal(RouteKeyA, coverage.RouteKey);
        Assert.Equal("https://example.com/a/", coverage.Canonical);
        Assert.Equal(40, coverage.Impressions);
        Assert.Equal(6, coverage.Clicks);
        Assert.NotNull(coverage.Ctr);
        Assert.Equal(0.15, coverage.Ctr!.Value, 10);
        Assert.Equal((4.0 * 10 + 2.0 * 30) / 40, coverage.AveragePosition, 10);
    }

    [Fact]
    public void Assemble_MissingTargetRouteKey_IsPreservedAsUnmatched()
    {
        var report = SeoQuestionInsightsAssembler.Assemble(
            RouteMapPath(),
            TargetMap([(QuestionKey, [RouteKeyA, RouteKeyMissing])]),
            [("observations/gsc-questions.json", Dataset())],
            Options(),
            GeneratedAt);

        var unmatched = Assert.Single(report.UnmatchedTargets);
        Assert.Equal(QuestionKey, unmatched.QuestionKey);
        Assert.Equal(RouteKeyMissing, unmatched.RouteKey);
        Assert.Equal("route_key_not_found", unmatched.ErrorCode);
        Assert.Equal(2, report.JoinQuality.Targets.SourceRows);
        Assert.Equal(1, report.JoinQuality.Targets.MatchedRows);
        Assert.Equal(1, report.JoinQuality.Targets.UnmatchedRows);
    }

    [Fact]
    public void Assemble_UnmatchedAndAmbiguousObservations_ArePreserved()
    {
        var report = SeoQuestionInsightsAssembler.Assemble(
            RouteMapPath(),
            TargetMap([(QuestionKey, [RouteKeyA])]),
            [
                ("observations/gsc-questions.json", Dataset(
                    (QuestionKey, "https://unknown.example/x/", 5, 1, 3.0),
                    (QuestionKey, "https://example.com/dup/", 5, 1, 3.0)))
            ],
            Options(),
            GeneratedAt);

        var unmatched = Assert.Single(report.UnmatchedObservations);
        Assert.Equal("https://unknown.example/x/", unmatched.Url);

        var ambiguous = Assert.Single(report.AmbiguousObservations);
        Assert.Equal("https://example.com/dup/", ambiguous.Url);
        Assert.Equal([RouteKeyDup1, RouteKeyDup2], ambiguous.CandidateRouteKeys);

        Assert.Equal(2, report.JoinQuality.Observations.SourceRows);
        Assert.Equal(0, report.JoinQuality.Observations.MatchedRows);
        Assert.Equal(1, report.JoinQuality.Observations.UnmatchedRows);
        Assert.Equal(1, report.JoinQuality.Observations.AmbiguousRows);
    }

    [Fact]
    public void Assemble_WindowMismatchAcrossDatasets_IsRejected()
    {
        var first = Dataset((QuestionKey, "https://example.com/a/", 1, 0, 1.0));
        var second = Dataset((QuestionKey, "https://example.com/b/", 1, 0, 1.0)) with
        {
            Window = new SeoObservationWindow(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 2), "UTC")
        };

        var exception = Assert.Throws<InvalidDataException>(() => SeoQuestionInsightsAssembler.Assemble(
            RouteMapPath(),
            TargetMap([(QuestionKey, [RouteKeyA])]),
            [("one.json", first), ("two.json", second)],
            Options(),
            GeneratedAt));

        Assert.StartsWith("question_insights.window_mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_DuplicateDatasetsAreCountedRatherThanDeduplicated()
    {
        var dataset = Dataset((QuestionKey, "https://example.com/a/", 10, 2, 4.0));

        var report = SeoQuestionInsightsAssembler.Assemble(
            RouteMapPath(),
            TargetMap([(QuestionKey, [RouteKeyA])]),
            [("one.json", dataset), ("two.json", dataset)],
            Options(),
            GeneratedAt);

        var question = Assert.Single(report.Questions);
        Assert.Equal(20, question.TotalImpressions);
        Assert.Equal(2, report.JoinQuality.Observations.SourceRows);
        Assert.Equal(2, report.JoinQuality.Observations.MatchedRows);
        Assert.Equal(2, report.Sources.Count);
    }

    [Fact]
    public void Assemble_JoinQualityTotalsCoverTargetsAndObservations()
    {
        var report = SeoQuestionInsightsAssembler.Assemble(
            RouteMapPath(),
            TargetMap([(QuestionKey, [RouteKeyA, RouteKeyMissing]), (OtherQuestionKey, [RouteKeyB])]),
            [("observations/gsc-questions.json", Dataset(
                (QuestionKey, "https://example.com/a/", 10, 2, 4.0),
                (OtherQuestionKey, "https://unknown.example/x/", 5, 1, 3.0)))],
            Options(),
            GeneratedAt);

        Assert.Equal(3, report.JoinQuality.Targets.SourceRows);
        Assert.Equal(2, report.JoinQuality.Targets.MatchedRows);
        Assert.Equal(2, report.JoinQuality.Observations.SourceRows);
        Assert.Equal(1, report.JoinQuality.Observations.MatchedRows);
        Assert.Equal(5, report.JoinQuality.Overall.SourceRows);
        Assert.Equal(3, report.JoinQuality.Overall.MatchedRows);
    }

    [Fact]
    public void Write_ProducesByteStableReportForFixedInputs()
    {
        var inputs = () => SeoQuestionInsightsAssembler.Assemble(
            RouteMapPath(),
            TargetMap([(QuestionKey, [RouteKeyA]), (OtherQuestionKey, [RouteKeyB])]),
            [("observations/gsc-questions.json", Dataset(
                (OtherQuestionKey, "https://example.com/b/", 7, 1, 2.5),
                (QuestionKey, "https://example.com/a/", 10, 2, 4.0)))],
            Options(),
            GeneratedAt);

        var first = Serialize(inputs());
        var second = Serialize(inputs());

        Assert.Equal(first, second);
        Assert.Contains("seo-question-insights-report.v1.json", first, StringComparison.Ordinal);
    }

    private static string Serialize(SeoQuestionInsightsReport report)
        => JsonSerializer.Serialize(report, SeoQuestionInsightsJsonContext.Default.SeoQuestionInsightsReport);

    private static SeoObservationUrlOptions Options()
        => new(
            "example.com",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static string RouteMapPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-question-route-map-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, $$"""
            {
              "schema": "https://bukit.dev/schemas/seo-route-map.v1.json",
              "schemaVersion": "1.0",
              "generatedAt": "2026-08-03T00:00:00Z",
              "siteUrl": "https://example.com",
              "baseUrl": "/",
              "routes": [
                {{RouteEntry(RouteKeyA, "/a/", "https://example.com/a/")}},
                {{RouteEntry(RouteKeyB, "/b/", "https://example.com/b/")}},
                {{RouteEntry(RouteKeyDup1, "/dup-1/", "https://example.com/dup/")}},
                {{RouteEntry(RouteKeyDup2, "/dup-2/", "https://example.com/dup/")}}
              ]
            }
            """);
        return path;
    }

    private static string RouteEntry(string routeKey, string route, string canonical)
        => $$$"""
            {
              "routeKey": "{{{routeKey}}}",
              "contentKey": null,
              "route": "{{{route}}}",
              "canonical": "{{{canonical}}}",
              "language": null,
              "contentType": "article",
              "collection": "post",
              "indexable": true,
              "publishedAt": "2026-08-01T00:00:00Z",
              "updatedAt": null
            }
            """;

    private static SeoQuestionTargetMap TargetMap(params (string QuestionKey, string[] RouteKeys)[] questions)
        => new(
            "https://bukit.dev/schemas/seo-question-target-map.v1.json",
            "1.0",
            DateTimeOffset.Parse("2026-08-04T00:00:00Z"),
            questions
                .Select(question => new SeoQuestionTarget(
                    question.QuestionKey,
                    TopicKey,
                    "informational",
                    "zh-CN",
                    "P1",
                    question.RouteKeys))
                .ToArray());

    private static SearchQuestionObservationDataset Dataset(params (string QuestionKey, string Url, long Impressions, long Clicks, double Position)[] rows)
        => new(
            "https://bukit.dev/schemas/search-question-observation.v1.json",
            "1.0",
            "google-search-console",
            "google-organic",
            DateTimeOffset.Parse("2026-08-03T00:00:00Z"),
            "api",
            new SeoObservationWindow(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), "UTC"),
            rows
                .Select(row => new SearchQuestionObservationRow(
                    row.QuestionKey,
                    TopicKey,
                    row.Url,
                    "zh-CN",
                    "desktop",
                    row.Impressions,
                    row.Clicks,
                    row.Position))
                .ToArray());
}
