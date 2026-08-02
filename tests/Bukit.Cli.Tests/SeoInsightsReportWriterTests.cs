using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Cli.Commands.SeoInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoInsightsReportWriterTests
{
    [Fact]
    public void Assemble_CountsEveryRowOnceAndAggregatesMatchedMetrics()
    {
        var report = SeoInsightsReportWriter.Assemble(CreateMatcher(), Datasets());

        Assert.Equal(new SeoJoinCounts(7, 4, 2, 1), report.JoinQuality.Overall);
        Assert.Equal(
            [
                new SeoProviderJoinQuality("google-analytics-4", new SeoJoinCounts(3, 2, 1, 0)),
                new SeoProviderJoinQuality("google-search-console", new SeoJoinCounts(4, 2, 1, 1))
            ],
            report.JoinQuality.Providers);

        var article = Assert.Single(report.Routes, route => route.RouteKey == "route:article");
        Assert.Equal(40, article.Metrics.Impressions);
        Assert.Equal(6, article.Metrics.Clicks);
        Assert.Equal(3.5, article.Metrics.AveragePosition!.Value, 12);
        Assert.Equal(0.15, article.Metrics.Ctr!.Value, 12);
        Assert.Equal(10, article.Metrics.Sessions);
        Assert.Equal(5, article.Metrics.EngagedSessions);
        Assert.Equal(1, article.Metrics.KeyEvents);
        Assert.Equal(0.5, article.Metrics.EngagementRate!.Value, 12);
        Assert.Equal(0.1, article.Metrics.KeyEventRate!.Value, 12);

        var zero = Assert.Single(report.Routes, route => route.RouteKey == "route:zero");
        Assert.Null(zero.Metrics.Ctr);
        Assert.Null(zero.Metrics.EngagementRate);
        Assert.Null(zero.Metrics.KeyEventRate);
        Assert.Null(zero.Metrics.AveragePosition);
        Assert.All(DoubleValues(report), value => Assert.True(double.IsFinite(value)));
    }

    [Fact]
    public void Assemble_PreservesAndSortsCompleteJoinEvidence()
    {
        var report = SeoInsightsReportWriter.Assemble(CreateMatcher(), Datasets());

        Assert.Equal(
            ["google-analytics-4|https://example.com/missing-a/|https://example.com/missing-a/", "google-search-console|https://example.com/missing-z/|https://example.com/missing-z/"],
            report.Unmatched.Select(EvidenceKey));
        Assert.Equal(9, report.Unmatched[0].Metrics.Sessions);
        Assert.Equal(4, report.Unmatched[0].Metrics.EngagedSessions);
        Assert.Equal(2, report.Unmatched[0].Metrics.KeyEvents);
        var ambiguous = Assert.Single(report.Ambiguous);
        Assert.Equal("google-search-console", ambiguous.Provider);
        Assert.Equal("https://example.com/shared/", ambiguous.OriginalUrl);
        Assert.Equal(5, ambiguous.Metrics.Impressions);
        Assert.Equal(["route:shared-a", "route:shared-b"], ambiguous.Candidates.Select(candidate => candidate.RouteKey));
        Assert.Equal(["/shared-a/", "/shared-b/"], ambiguous.Candidates.Select(candidate => candidate.Route));
    }

    [Fact]
    public void Assemble_UsesLatestCollectionTimeExactWindowAndDeterministicRouteOrder()
    {
        var report = SeoInsightsReportWriter.Assemble(CreateMatcher(), Datasets());

        Assert.Equal(DateTimeOffset.Parse("2026-08-03T02:00:00Z"), report.GeneratedAt);
        Assert.Equal(new SeoObservationWindow(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), "Asia/Kuala_Lumpur"), report.Window);
        Assert.Equal(["route:article", "route:zero"], report.Routes.Select(route => route.RouteKey));
        Assert.Equal(["google-analytics-4", "google-search-console"], report.Sources.Select(source => source.Provider));
    }

    [Fact]
    public void Assemble_MismatchedWindowsAreRejected()
    {
        var datasets = Datasets().ToArray();
        datasets[1] = datasets[1] with
        {
            Window = datasets[1].Window with { EndDate = new DateOnly(2026, 8, 3) }
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => SeoInsightsReportWriter.Assemble(CreateMatcher(), datasets));

        Assert.StartsWith("report.window_mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_IntegerOverflowIsRejectedRatherThanWrapped()
    {
        var datasets = Datasets().ToArray();
        var gsc = datasets.Single(dataset => dataset.Provider == "google-search-console");
        datasets[Array.IndexOf(datasets, gsc)] = gsc with
        {
            Rows =
            [
                new SeoObservationRow("https://example.com/article/", long.MaxValue, 0, 1, null, null, null),
                new SeoObservationRow("https://example.com/article/?utm_source=x", 1, 0, 1, null, null, null)
            ]
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => SeoInsightsReportWriter.Assemble(CreateMatcher(), datasets));

        Assert.StartsWith("report.numeric_overflow", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_UsesExactTopLevelContractAndDeterministicUtf8Json()
    {
        var report = SeoInsightsReportWriter.Assemble(CreateMatcher(), Datasets());
        var output = Path.Combine(Path.GetTempPath(), $"bukit-seo-{Guid.NewGuid():N}");
        try
        {
            SeoInsightsReportWriter.Write(output, report);
            var path = Path.Combine(output, ".bukit", "seo-insights-report.json");
            var first = File.ReadAllText(path);
            SeoInsightsReportWriter.Write(output, report);
            var second = File.ReadAllText(path);

            Assert.Equal(first, second);
            Assert.EndsWith(Environment.NewLine, first, StringComparison.Ordinal);
            using var json = JsonDocument.Parse(first);
            Assert.Equal(
                ["schema", "schemaVersion", "generatedAt", "window", "sources", "joinQuality", "routes", "unmatched", "ambiguous"],
                json.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.False(first.Contains("NaN", StringComparison.Ordinal));
            Assert.False(first.Contains("Infinity", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public void JsonContext_SourceGeneratesEveryNestedInputAndOutputType()
    {
        var generatedTypes = typeof(SeoInsightsJsonContext)
            .CustomAttributes
            .Where(attribute => attribute.AttributeType == typeof(JsonSerializableAttribute))
            .Select(attribute => (Type)attribute.ConstructorArguments[0].Value!)
            .ToHashSet();

        Assert.Subset(
            new HashSet<Type>
            {
                typeof(SeoObservationWindow), typeof(SeoObservationRow), typeof(SeoObservationDataset),
                typeof(SeoObservationRouteCandidate), typeof(SeoObservationMetrics), typeof(SeoInsightsSource),
                typeof(SeoJoinCounts), typeof(SeoProviderJoinQuality), typeof(SeoJoinQuality),
                typeof(SeoInsightsRoute), typeof(SeoUnmatchedObservation), typeof(SeoAmbiguousObservation),
                typeof(SeoInsightsReport)
            },
            generatedTypes);
    }

    private static IEnumerable<double> DoubleValues(SeoInsightsReport report)
        => report.Routes.SelectMany(route => new[]
        {
            route.Metrics.AveragePosition,
            route.Metrics.Ctr,
            route.Metrics.EngagementRate,
            route.Metrics.KeyEventRate
        }).Where(value => value.HasValue).Select(value => value!.Value);

    private static string EvidenceKey(SeoUnmatchedObservation evidence)
        => $"{evidence.Provider}|{evidence.NormalizedUrl}|{evidence.OriginalUrl}";

    private static IReadOnlyList<SeoObservationDataset> Datasets()
    {
        var window = new SeoObservationWindow(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), "Asia/Kuala_Lumpur");
        return
        [
            new SeoObservationDataset(
                "https://bukit.dev/schemas/seo-observation.v1.json", "1.0", "google-search-console", "google-organic",
                DateTimeOffset.Parse("2026-08-03T01:00:00Z"), window,
                [
                    new SeoObservationRow("https://example.com/article/", 10, 2, 2, null, null, null),
                    new SeoObservationRow("https://example.com/article?utm_source=x", 30, 4, 4, null, null, null),
                    new SeoObservationRow("https://example.com/missing-z/", 7, 1, 8, null, null, null),
                    new SeoObservationRow("https://example.com/shared/", 5, 1, 5, null, null, null)
                ]),
            new SeoObservationDataset(
                "https://bukit.dev/schemas/seo-observation.v1.json", "1.0", "google-analytics-4", "google-organic",
                DateTimeOffset.Parse("2026-08-03T02:00:00Z"), window,
                [
                    new SeoObservationRow("https://example.com/article/", null, null, null, 10, 5, 1),
                    new SeoObservationRow("https://example.com/zero/", null, null, null, 0, 0, 0),
                    new SeoObservationRow("https://example.com/missing-a/", null, null, null, 9, 4, 2)
                ])
        ];
    }

    private static SeoObservationRouteMatcher CreateMatcher()
    {
        var routeMap = CreateRouteMap(
        [
            new("route:zero", null, "/zero/", "/zero/"),
            new("route:article", "content:article", "/article/", "/article/"),
            new("route:shared-b", null, "/shared-b/", "/shared/"),
            new("route:shared-a", "content:shared", "/shared-a/", "/shared/")
        ]);
        var constructor = typeof(SeoObservationRouteMatcher).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (SeoObservationRouteMatcher)constructor.Invoke(
        [
            routeMap,
            new SeoObservationUrlOptions(
                "example.com",
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(["utm_source"], StringComparer.Ordinal))
        ]);
    }

    private static object CreateRouteMap(IReadOnlyList<RouteDefinition> routes)
    {
        var engineAssembly = Assembly.Load("Bukit.Engine");
        var entryType = engineAssembly.GetType("Bukit.Engine.SeoRouteMapEntry", throwOnError: true)!;
        var entryConstructor = entryType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 10);
        var entries = Array.CreateInstance(entryType, routes.Count);
        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            entries.SetValue(entryConstructor.Invoke(
                [route.RouteKey, route.ContentKey, route.Route, route.Canonical, null, null, null, true, null, null]), index);
        }

        var mapType = engineAssembly.GetType("Bukit.Engine.SeoRouteMap", throwOnError: true)!;
        var mapConstructor = mapType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 6);
        return mapConstructor.Invoke(
            ["https://bukit.dev/schemas/seo-route-map.v1.json", "1.0", DateTimeOffset.Parse("2026-08-03T00:00:00Z"), "https://example.com", "/", entries]);
    }

    private sealed record RouteDefinition(string RouteKey, string? ContentKey, string Route, string Canonical);
}
