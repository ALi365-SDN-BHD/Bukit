using System.Text.Json;
using Bukit.Cli.Commands.SeoGenerativeInsights;
using Bukit.Cli.Commands.SeoInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class GenerativeCitationReportWriterTests
{
    private const string QuestionKey = "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OtherQuestionKey = "question:sha256:1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string AnswerHash = "answer:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string RouteKeyA = "route:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RouteKeyB = "route:sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string RouteKeyDup1 = "route:sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string RouteKeyDup2 = "route:sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private static readonly DateTimeOffset GeneratedAt = DateTimeOffset.Parse("2026-08-05T00:00:00Z");

    [Fact]
    public void Assemble_MultiEngineMultiRun_AggregatesRatesAndDeduplicatesRouteCitationsPerRun()
    {
        var report = GenerativeCitationReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/engine-one.json", Dataset(
                    "engine-one",
                    "v1",
                    (QuestionKey, true, true, ["https://example.com/a/", "https://example.com/a/?utm_source=x"]),
                    (QuestionKey, false, false, []))),
                ("observations/engine-two.json", Dataset(
                    "engine-two",
                    "v1",
                    (OtherQuestionKey, true, true, ["https://example.com/b/"])))
            ],
            Options(),
            GeneratedAt);

        Assert.Equal(3, report.Overall.Runs);
        Assert.Equal(2, report.Overall.BrandMentions);
        Assert.Equal(2.0 / 3, report.Overall.BrandMentionRate!.Value, 10);
        Assert.Equal(2, report.Overall.SiteCitations);
        Assert.Equal(2.0 / 3, report.Overall.SiteCitationRate!.Value, 10);

        Assert.Equal(2, report.Engines.Count);
        Assert.Equal("engine-one", report.Engines[0].Engine);
        Assert.Equal(2, report.Engines[0].Runs);
        Assert.Equal(1, report.Engines[0].BrandMentions);
        Assert.Equal(0.5, report.Engines[0].BrandMentionRate!.Value, 10);
        Assert.Equal("engine-two", report.Engines[1].Engine);
        Assert.Equal(1, report.Engines[1].Runs);

        Assert.Equal(2, report.Questions.Count);
        var first = report.Questions[0];
        Assert.Equal(QuestionKey, first.QuestionKey);
        Assert.Equal(2, first.Runs);
        Assert.Equal(1, first.BrandMentions);
        Assert.Equal(0.5, first.BrandMentionRate!.Value, 10);
        Assert.Equal(1, first.SiteCitations);
        var route = Assert.Single(first.Routes);
        Assert.Equal(RouteKeyA, route.RouteKey);
        Assert.Equal("https://example.com/a/", route.Canonical);
        Assert.Equal(1, route.CitationRuns);

        var second = report.Questions[1];
        Assert.Equal(OtherQuestionKey, second.QuestionKey);
        Assert.Equal(RouteKeyB, Assert.Single(second.Routes).RouteKey);

        Assert.Equal(2, report.JoinQuality.SourceRows);
        Assert.Equal(2, report.JoinQuality.MatchedRows);
        Assert.Equal(0, report.JoinQuality.UnmatchedRows);
        Assert.Equal(0, report.JoinQuality.AmbiguousRows);
    }

    [Fact]
    public void Assemble_ExternalCitedUrlIsEvidenceAndNeverUnmatched()
    {
        var report = GenerativeCitationReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/runs.json", Dataset(
                    "engine-one",
                    "v1",
                    (QuestionKey, true, true, ["https://example.com/a/", "https://third-party.example.org/source/"]),
                    (QuestionKey, true, true, ["https://example.com/a/", "https://third-party.example.org/source/"])))
            ],
            Options(),
            GeneratedAt);

        var external = Assert.Single(report.ExternalCitedUrls);
        Assert.Equal("https://third-party.example.org/source/", external.Url);
        Assert.Equal(2, external.CitationRuns);
        Assert.Empty(report.UnmatchedCitedUrls);
        Assert.Empty(report.AmbiguousCitedUrls);
        Assert.Equal(1, report.JoinQuality.SourceRows);
        Assert.Equal(1, report.JoinQuality.MatchedRows);
    }

    [Fact]
    public void Assemble_UnmatchedAndAmbiguousCitedUrlsArePreserved()
    {
        var report = GenerativeCitationReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/runs.json", Dataset(
                    "engine-one",
                    "v1",
                    (QuestionKey, true, true,
                        ["https://example.com/a/", "https://example.com/unknown/", "https://example.com/dup/"])))
            ],
            Options(),
            GeneratedAt);

        var unmatched = Assert.Single(report.UnmatchedCitedUrls);
        Assert.Equal("https://example.com/unknown/", unmatched.Url);

        var ambiguous = Assert.Single(report.AmbiguousCitedUrls);
        Assert.Equal("https://example.com/dup/", ambiguous.Url);
        Assert.Equal([RouteKeyDup1, RouteKeyDup2], ambiguous.CandidateRouteKeys);

        Assert.Equal(3, report.JoinQuality.SourceRows);
        Assert.Equal(1, report.JoinQuality.MatchedRows);
        Assert.Equal(1, report.JoinQuality.UnmatchedRows);
        Assert.Equal(1, report.JoinQuality.AmbiguousRows);
    }

    [Fact]
    public void Assemble_ContradictoryPromptSetVersionsRemainSeparateSources()
    {
        var report = GenerativeCitationReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/first.json", Dataset(
                    "engine-one",
                    "v1",
                    (QuestionKey, true, true, ["https://example.com/a/"]))),
                ("observations/second.json", Dataset(
                    "engine-one",
                    "v2",
                    (QuestionKey, false, true, ["https://example.com/a/"])))
            ],
            Options(),
            GeneratedAt);

        Assert.Equal(2, report.Sources.Count);
        Assert.Equal("v1", report.Sources[0].PromptSetVersion);
        Assert.Equal("v2", report.Sources[1].PromptSetVersion);
        var engine = Assert.Single(report.Engines);
        Assert.Equal("engine-one", engine.Engine);
        Assert.Equal(2, engine.Runs);
    }

    [Fact]
    public void Assemble_EmptyDatasetReportsZeroRunsWithNullRates()
    {
        var report = GenerativeCitationReportWriter.Assemble(
            RouteMapPath(),
            [("observations/runs.json", Dataset("engine-one", "v1"))],
            Options(),
            GeneratedAt);

        Assert.Equal(0, report.Overall.Runs);
        Assert.Null(report.Overall.BrandMentionRate);
        Assert.Null(report.Overall.SiteCitationRate);
        Assert.Empty(report.Engines);
        Assert.Empty(report.Questions);
    }

    [Fact]
    public void Write_ProducesByteStableReportForFixedInputs()
    {
        var inputs = () => GenerativeCitationReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/runs.json", Dataset(
                    "engine-one",
                    "v1",
                    (OtherQuestionKey, true, true, ["https://example.com/b/"]),
                    (QuestionKey, true, true, ["https://example.com/a/"])))
            ],
            Options(),
            GeneratedAt);

        var first = Serialize(inputs());
        var second = Serialize(inputs());

        Assert.Equal(first, second);
        Assert.Contains("generative-citation-report.v1.json", first, StringComparison.Ordinal);
    }

    private static string Serialize(GenerativeCitationReport report)
        => JsonSerializer.Serialize(report, SeoGenerativeInsightsJsonContext.Default.GenerativeCitationReport);

    private static SeoObservationUrlOptions Options()
        => new(
            "example.com",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "utm_source" });

    private static string RouteMapPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-generative-route-map-{Guid.NewGuid():N}.json");
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

    private static GenerativeAnswerObservationDataset Dataset(
        string engine,
        string promptSetVersion,
        params (string QuestionKey, bool BrandMentioned, bool SiteCited, string[] CitedUrls)[] rows)
        => new(
            "https://bukit.dev/schemas/generative-answer-observation.v1.json",
            "1.0",
            engine,
            promptSetVersion,
            "zh-CN",
            DateTimeOffset.Parse("2026-08-04T00:00:00Z"),
            "api",
            rows
                .Select((row, index) => new GenerativeAnswerObservationRow(
                    row.QuestionKey,
                    0,
                    index,
                    row.BrandMentioned,
                    row.SiteCited,
                    row.CitedUrls,
                    row.SiteCited ? 1 : null,
                    AnswerHash))
                .ToArray());
}
