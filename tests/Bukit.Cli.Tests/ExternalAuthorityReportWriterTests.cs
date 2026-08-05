using System.Text.Json;
using Bukit.Cli.Commands.SeoAuthorityInsights;
using Bukit.Cli.Commands.SeoInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ExternalAuthorityReportWriterTests
{
    private const string QuestionKey = "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string RouteKeyA = "route:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RouteKeyB = "route:sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string RouteKeyDup1 = "route:sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string RouteKeyDup2 = "route:sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private static readonly DateTimeOffset GeneratedAt = DateTimeOffset.Parse("2026-08-05T00:00:00Z");

    [Fact]
    public void Assemble_LifecycleStatuses_CountOnlyActiveSourcesAndPreserveEvidence()
    {
        var report = ExternalAuthorityReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/forum.json", Dataset("provider-a",
                    Row("https://source.example/a", "forum", "active", ["https://example.com/a/"]),
                    Row("https://source.example/b", "forum", "deleted", ["https://example.com/a/"]),
                    Row("https://source.example/c", "news", "unavailable", ["https://example.com/b/"])))
            ],
            Options(),
            GeneratedAt);

        Assert.Equal(3, report.Overall.Sources);
        Assert.Equal(1, report.Overall.ActiveSources);
        Assert.Equal(1, report.Overall.ActiveCitedRoutes);

        Assert.Equal(3, report.Sources.Count);
        Assert.Equal("deleted", report.Sources[1].Status);
        Assert.Equal(RouteKeyA, Assert.Single(report.Sources[1].CitedRouteKeys));
        Assert.Equal(DateTimeOffset.Parse("2026-08-03T00:00:00Z"), report.Sources[1].ObservedAt);

        Assert.Equal(3, report.Statuses.Count);
        Assert.Equal("active", report.Statuses[0].Status);
        Assert.Equal(1, report.Statuses[0].Sources);
        Assert.Equal("deleted", report.Statuses[1].Status);
        Assert.Equal("unavailable", report.Statuses[2].Status);

        var route = Assert.Single(report.Routes);
        Assert.Equal(RouteKeyA, route.RouteKey);
        Assert.Equal("https://example.com/a/", route.Canonical);
        Assert.Equal(1, route.ActiveSources);
    }

    [Fact]
    public void Assemble_MultipleActiveSourcesCitingOneRoute_CountsEachSourceOnce()
    {
        var report = ExternalAuthorityReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/first.json", Dataset("provider-a",
                    Row("https://source.example/a", "forum", "active", ["https://example.com/a/"]))),
                ("observations/second.json", Dataset("provider-b",
                    Row("https://source.example/b", "news", "active", ["https://example.com/a/"])))
            ],
            Options(),
            GeneratedAt);

        var route = Assert.Single(report.Routes);
        Assert.Equal(RouteKeyA, route.RouteKey);
        Assert.Equal(2, route.ActiveSources);
        Assert.Equal(1, report.Overall.ActiveCitedRoutes);
    }

    [Fact]
    public void Assemble_OneSourceCitingMultipleRoutes_RecordsAllRouteKeys()
    {
        var report = ExternalAuthorityReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/multi.json", Dataset("provider-a",
                    Row("https://source.example/a", "repository", "active",
                        ["https://example.com/b/", "https://example.com/a/"])))
            ],
            Options(),
            GeneratedAt);

        Assert.Equal([RouteKeyA, RouteKeyB], report.Sources[0].CitedRouteKeys);
        Assert.Equal(2, report.Routes.Count);
        Assert.Equal(RouteKeyA, report.Routes[0].RouteKey);
        Assert.Equal(RouteKeyB, report.Routes[1].RouteKey);
        Assert.Equal(2, report.Overall.ActiveCitedRoutes);
    }

    [Fact]
    public void Assemble_DuplicateNormalizedUrls_AreNotDoubleCounted()
    {
        var report = ExternalAuthorityReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/utm.json", Dataset("provider-a",
                    Row("https://source.example/a", "forum", "active",
                        ["https://example.com/a/", "https://example.com/a/?utm_source=x"])))
            ],
            Options(),
            GeneratedAt);

        var route = Assert.Single(report.Routes);
        Assert.Equal(1, route.ActiveSources);
        Assert.Equal(1, report.JoinQuality.SourceRows);
        Assert.Equal(1, report.JoinQuality.MatchedRows);
    }

    [Fact]
    public void Assemble_UnmatchedAmbiguousAndInvalidUrls_ArePreserved()
    {
        var report = ExternalAuthorityReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/gaps.json", Dataset("provider-a",
                    Row("https://source.example/a", "forum", "active",
                        ["https://example.com/a/", "https://example.com/unknown/", "https://example.com/dup/"]),
                    Row("https://source.example/b", "forum", "active",
                        ["https://user:pass@example.com/a/"])))
            ],
            Options(),
            GeneratedAt);

        Assert.Equal(2, report.UnmatchedCitedUrls.Count);
        Assert.Equal("https://example.com/unknown/", report.UnmatchedCitedUrls[0].Url);
        Assert.Null(report.UnmatchedCitedUrls[0].ErrorCode);
        Assert.Equal("https://user:pass@example.com/a/", report.UnmatchedCitedUrls[1].Url);
        Assert.Equal("credentials_not_allowed", report.UnmatchedCitedUrls[1].ErrorCode);

        var ambiguous = Assert.Single(report.AmbiguousCitedUrls);
        Assert.Equal("https://example.com/dup/", ambiguous.Url);
        Assert.Equal([RouteKeyDup1, RouteKeyDup2], ambiguous.CandidateRouteKeys);

        Assert.Equal(4, report.JoinQuality.SourceRows);
        Assert.Equal(1, report.JoinQuality.MatchedRows);
        Assert.Equal(2, report.JoinQuality.UnmatchedRows);
        Assert.Equal(1, report.JoinQuality.AmbiguousRows);
    }

    [Fact]
    public void Assemble_ExternalHostCitedUrl_IsNotUnmatched()
    {
        var report = ExternalAuthorityReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/external.json", Dataset("provider-a",
                    Row("https://source.example/a", "forum", "active",
                        ["https://example.com/a/", "https://third-party.example.org/source/"])))
            ],
            Options(),
            GeneratedAt);

        Assert.Empty(report.UnmatchedCitedUrls);
        Assert.Empty(report.AmbiguousCitedUrls);
        Assert.Equal(1, report.JoinQuality.SourceRows);
        Assert.Equal(1, report.JoinQuality.MatchedRows);
    }

    [Fact]
    public void Assemble_ProviderAndSourceTypeTotals_AreSeparate()
    {
        var report = ExternalAuthorityReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/mixed.json", Dataset("provider-a",
                    Row("https://source.example/a", "official", "active", ["https://example.com/a/"]),
                    Row("https://source.example/b", "forum", "deleted", ["https://example.com/a/"]))),
                 ("observations/second.json", Dataset("provider-b",
                    Row("https://source.example/c", "forum", "active", ["https://example.com/b/"])))
            ],
            Options(),
            GeneratedAt);

        Assert.Equal(2, report.Providers.Count);
        Assert.Equal("provider-a", report.Providers[0].Provider);
        Assert.Equal(2, report.Providers[0].Sources);
        Assert.Equal(1, report.Providers[0].ActiveSources);
        Assert.Equal("provider-b", report.Providers[1].Provider);
        Assert.Equal(1, report.Providers[1].Sources);
        Assert.Equal(1, report.Providers[1].ActiveSources);

        Assert.Equal(2, report.SourceTypes.Count);
        Assert.Equal("forum", report.SourceTypes[0].SourceType);
        Assert.Equal(2, report.SourceTypes[0].Sources);
        Assert.Equal(1, report.SourceTypes[0].ActiveSources);
        Assert.Equal("official", report.SourceTypes[1].SourceType);
        Assert.Equal(1, report.SourceTypes[1].Sources);
        Assert.Equal(1, report.SourceTypes[1].ActiveSources);
    }

    [Fact]
    public void Assemble_NoDatasets_ThrowsStableCode()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => ExternalAuthorityReportWriter.Assemble(RouteMapPath(), [], Options(), GeneratedAt));

        Assert.StartsWith("external_authority_insights.observations_required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ProducesByteStableReportForFixedInputs()
    {
        var inputs = () => ExternalAuthorityReportWriter.Assemble(
            RouteMapPath(),
            [
                ("observations/stable.json", Dataset("provider-a",
                    Row("https://source.example/b", "news", "active", ["https://example.com/b/"]),
                    Row("https://source.example/a", "forum", "active", ["https://example.com/a/"])))
            ],
            Options(),
            GeneratedAt);

        var first = Serialize(inputs());
        var second = Serialize(inputs());

        Assert.Equal(first, second);
        Assert.Contains("external-authority-report.v1.json", first, StringComparison.Ordinal);
    }

    private static string Serialize(ExternalAuthorityReport report)
        => JsonSerializer.Serialize(report, SeoAuthorityInsightsJsonContext.Default.ExternalAuthorityReport);

    private static SeoObservationUrlOptions Options()
        => new(
            "example.com",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "utm_source" });

    private static string RouteMapPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-authority-route-map-{Guid.NewGuid():N}.json");
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

    private static ExternalAuthorityObservationRow Row(
        string sourceUrl,
        string sourceType,
        string status,
        string[] citedUrls)
    {
        var observedAt = status switch
        {
            "deleted" => "2026-08-03T00:00:00Z",
            "unavailable" => "2026-08-04T00:00:00Z",
            _ => "2026-08-05T00:00:00Z"
        };
        return new ExternalAuthorityObservationRow(
            sourceUrl,
            sourceType,
            DateTimeOffset.Parse(observedAt),
            status,
            QuestionKey,
            null,
            null,
            "context:sha256:" + new string('0', 63) + sourceUrl.Length % 10,
            citedUrls);
    }

    private static ExternalAuthorityObservationDataset Dataset(
        string provider,
        params ExternalAuthorityObservationRow[] rows)
        => new(
            "https://bukit.dev/schemas/external-authority-observation.v1.json",
            "1.0",
            provider,
            DateTimeOffset.Parse("2026-08-05T00:00:00Z"),
            "api",
            rows);
}
