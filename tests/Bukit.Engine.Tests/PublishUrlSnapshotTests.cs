using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Shared;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PublishUrlSnapshotTests
{
    [Fact]
    public void Writer_EmitsCandidateSnapshotWithStableContract()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "bukit",
                    Title = "Bukit",
                    Url = "https://silushangxun.com/",
                    BaseUrl = "/",
                    Language = "zh-CN"
                },
                Content = TestContent.Markdown(),
                Build = new BuildConfig
                {
                    Output = "dist",
                    Report = new BuildReportConfig { Enabled = true }
                }
            };
            var document = ContentDocument.Create(
                "article",
                "Example",
                "example",
                DateTimeOffset.Parse("2026-07-25T00:00:00Z"),
                "<p>Canonical body</p>");
            var route = new RouteInfo("/insights/example/", "insights/example/index.html", "pages/post.html");
            var seo = new SeoModel
            {
                Title = "Example",
                Description = "Canonical description",
                Canonical = "https://silushangxun.com/insights/example/",
                JsonLd = ["{\"@type\":\"Article\",\"headline\":\"Example\"}"]
            };
            var variant = new BuildVariantResult(
                Language: "zh-CN",
                OutputDir: outputDir,
                BaseUrl: "/",
                SearchSnippetsEnabled: false,
                BodyStore: EmptyContentBodyStore.Instance,
                DerivedRoutes: Array.Empty<(RouteInfo, DateTimeOffset)>(),
                SeoIndex: new Dictionary<string, SeoIndexEntry>
                {
                    [route.OutputPath] = new SeoIndexEntry(route, seo.Canonical, null, true, null, document.Id, "post")
                },
                SeoModels: new Dictionary<string, SeoModel> { [route.OutputPath] = seo },
                PluginExecutions: Array.Empty<PluginExecutionInfo>(),
                RenderedCount: 1,
                SkippedCount: 0,
                RenderReasons: new Dictionary<string, int>(),
                StageMetrics: new BuildStageMetrics(new Dictionary<string, long>(), new Dictionary<string, int>()),
                RoutedDocuments: [new RoutedContentDocument(document, route, document.PublishAt)]);
            var result = BuildResultFactory.Create(
                config,
                outputDir,
                outputDir,
                new ConfigOverrides(),
                DateTimeOffset.Parse("2026-07-25T00:00:00Z"),
                DateTimeOffset.Parse("2026-07-25T00:00:01Z"),
                1000,
                [variant]);

            BuildReporter.WriteIfEnabled(config, outputDir, outputDir, result, [variant], new ConsoleLogger(LogLevel.Error));

            var snapshotPath = Path.Combine(outputDir, ".bukit", "publish-url-snapshot.json");
            Assert.True(File.Exists(snapshotPath));

            using var snapshot = JsonDocument.Parse(File.ReadAllText(snapshotPath));
            Assert.Equal("https://bukit.dev/schemas/publish-url-snapshot.v1.json", snapshot.RootElement.GetProperty("schema").GetString());
            Assert.Equal("https://silushangxun.com/", snapshot.RootElement.GetProperty("siteUrl").GetString());
            var routeSnapshot = Assert.Single(snapshot.RootElement.GetProperty("routes").EnumerateArray());
            Assert.Equal("https://silushangxun.com/insights/example/", routeSnapshot.GetProperty("url").GetString());
            Assert.True(routeSnapshot.GetProperty("indexable").GetBoolean());
            Assert.Matches("^sha256:[0-9a-f]{64}$", routeSnapshot.GetProperty("semanticHash").GetString());
            Assert.Equal(
                new[] { "indexable", "semanticHash", "url" },
                routeSnapshot.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public void Snapshot_BuildsStableCanonicalUrlOrderAndExcludesBuildMetadata()
    {
        var config = CreateConfig();
        var later = CreateVariant(config, "https://silushangxun.com/insights/zeta/", "<p>Zeta</p>");
        var earlier = CreateVariant(config, "https://silushangxun.com/insights/alpha/", "<p>Alpha</p>");

        var first = PublishUrlSnapshotBuilder.Build(config, [later, earlier]);
        var second = PublishUrlSnapshotBuilder.Build(config, [earlier, later]);
        var firstJson = PublishUrlSnapshotJson.Serialize(first);
        var secondJson = PublishUrlSnapshotJson.Serialize(second);

        Assert.Equal(new[]
        {
            "https://silushangxun.com/insights/alpha/",
            "https://silushangxun.com/insights/zeta/"
        }, first.Routes.Select(route => route.Url));
        Assert.Equal(firstJson, secondJson);
        Assert.DoesNotContain("generatedAt", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetTempPath(), firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", firstJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SemanticHash_CoversOnlyCanonicalPublishSemantics()
    {
        var document = ContentDocument.Create(
            "11111111-1111-1111-1111-111111111111",
            "Example",
            "example",
            DateTimeOffset.Parse("2026-07-25T00:00:00Z"),
            "<p>Canonical body</p>");
        var route = new RouteInfo("/insights/example/", "insights/example/index.html", "pages/post.html");
        var entry = new SeoIndexEntry(route, "https://silushangxun.com/insights/example/", "index,follow", true, null, document.Id, "post");
        var model = CreateSeo(entry.Canonical);
        var baseline = PublishUrlSemanticHasher.Compute(document, entry, model);

        Assert.NotEqual(baseline, PublishUrlSemanticHasher.Compute(document with { Body = new ContentBodyRef(Html: "<p>Changed body</p>") }, entry, model));
        Assert.NotEqual(baseline, PublishUrlSemanticHasher.Compute(document, entry, model with { Title = "Changed title" }));
        Assert.NotEqual(baseline, PublishUrlSemanticHasher.Compute(document, entry, model with { Description = "Changed description" }));
        var changedCanonical = entry with { Canonical = "https://silushangxun.com/insights/changed/" };
        Assert.NotEqual(baseline, PublishUrlSemanticHasher.Compute(document, changedCanonical, model with { Canonical = changedCanonical.Canonical }));
        Assert.NotEqual(baseline, PublishUrlSemanticHasher.Compute(document, entry with { Robots = "noindex,follow" }, model));
        Assert.NotEqual(baseline, PublishUrlSemanticHasher.Compute(document, entry, model with { GeoAuthor = new GeoAuthorModel { Name = "Changed author" } }));
        Assert.NotEqual(baseline, PublishUrlSemanticHasher.Compute(document, entry, model with { JsonLd = ["{\"@type\":\"Article\",\"headline\":\"Changed\"}"] }));
        Assert.Equal(
            baseline,
            PublishUrlSemanticHasher.Compute(document, entry, model));
        Assert.Equal(
            baseline,
            PublishUrlSemanticHasher.Compute(document, entry, model with { JsonLd = ["{\"headline\":\"Example\",\"@type\":\"Article\"}"] }));
    }

    [Fact]
    public void SemanticHash_ExcludesVolatileMetadataAndLocalOrNotionIdentifiers()
    {
        var route = new RouteInfo("/insights/example/", "insights/example/index.html", "pages/post.html");
        var entry = new SeoIndexEntry(route, "https://silushangxun.com/insights/example/", "index,follow", true, null, null, "post");
        var model = CreateSeo(entry.Canonical);
        var localFirst = ContentDocument.Create("record-a", "Example", "example", DateTimeOffset.Parse("2026-07-25T00:00:00Z"), "<img src=\"/private/tmp/first/image.png\"> 11111111-1111-1111-1111-111111111111");
        var localSecond = ContentDocument.Create("record-b", "Example", "example", DateTimeOffset.Parse("2026-07-25T00:00:00Z"), "<img src=\"/private/tmp/second/image.png\"> 22222222-2222-2222-2222-222222222222");
        var firstJsonLd = model with { JsonLd = ["{\"@type\":\"Article\",\"headline\":\"Example\",\"generatedAt\":\"2026-07-25T00:00:00Z\"}"] };
        var secondJsonLd = model with { JsonLd = ["{\"headline\":\"Example\",\"generatedAt\":\"2026-07-26T00:00:00Z\",\"@type\":\"Article\"}"] };

        Assert.Equal(
            PublishUrlSemanticHasher.Compute(localFirst, entry, firstJsonLd),
            PublishUrlSemanticHasher.Compute(localSecond, entry, secondJsonLd));
    }

    [Fact]
    public void Diff_UsesOnlyExplicitBaselineAndCurrentSnapshots()
    {
        var baseline = Snapshot(
            new PublishUrlSnapshotRoute("https://silushangxun.com/insights/deleted/", true, "sha256:deleted"),
            new PublishUrlSnapshotRoute("https://silushangxun.com/insights/updated/", true, "sha256:before"));
        var current = Snapshot(
            new PublishUrlSnapshotRoute("https://silushangxun.com/insights/added/", true, "sha256:added"),
            new PublishUrlSnapshotRoute("https://silushangxun.com/insights/updated/", true, "sha256:after"));

        var changes = PublishUrlSnapshotDiff.Create(baseline, current);

        Assert.Equal(
            new[] { "added", "deleted", "updated" },
            changes.Changes.Select(change => change.Type));
        Assert.Equal(
            new[]
            {
                "https://silushangxun.com/insights/added/",
                "https://silushangxun.com/insights/deleted/",
                "https://silushangxun.com/insights/updated/"
            },
            changes.Changes.Select(change => change.Url));
        Assert.Equal("sha256:before", baseline.Routes.Single(route => route.Url.EndsWith("updated/", StringComparison.Ordinal)).SemanticHash);
        Assert.Equal("sha256:after", current.Routes.Single(route => route.Url.EndsWith("updated/", StringComparison.Ordinal)).SemanticHash);
        Assert.Equal(changes.Changes, PublishUrlSnapshotDiff.Create(baseline, current).Changes);
        Assert.Empty(PublishUrlSnapshotDiff.Create(current, current).Changes);
    }

    [Theory]
    [InlineData(true, false, "deleted")]
    [InlineData(false, true, "added")]
    public void Diff_MapsIndexabilityTransitionsToPresenceChanges(bool baselineIndexable, bool currentIndexable, string expectedType)
    {
        const string url = "https://silushangxun.com/insights/example/";

        var changes = PublishUrlSnapshotDiff.Create(
            Snapshot(new PublishUrlSnapshotRoute(url, baselineIndexable, "sha256:before")),
            Snapshot(new PublishUrlSnapshotRoute(url, currentIndexable, "sha256:after")));

        var change = Assert.Single(changes.Changes);
        Assert.Equal(expectedType, change.Type);
        Assert.DoesNotContain(changes.Changes, item => item.Type == "updated");
    }

    private static AppConfig CreateConfig()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "bukit",
                Title = "Bukit",
                Url = "https://silushangxun.com/",
                BaseUrl = "/",
                Language = "zh-CN"
            },
            Content = TestContent.Markdown(),
            Build = new BuildConfig { Report = new BuildReportConfig { Enabled = true } }
        };

    private static BuildVariantResult CreateVariant(AppConfig config, string canonical, string body)
    {
        var relativeUrl = new Uri(canonical).AbsolutePath;
        var outputPath = relativeUrl.Trim('/').Replace('/', Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "index.html";
        var route = new RouteInfo(relativeUrl, outputPath, "pages/post.html");
        var document = ContentDocument.Create(
            "11111111-1111-1111-1111-111111111111",
            "Example",
            "example",
            DateTimeOffset.Parse("2026-07-25T00:00:00Z"),
            body);
        var model = CreateSeo(canonical);
        return new BuildVariantResult(
            Language: config.Site.Language,
            OutputDir: Path.GetTempPath(),
            BaseUrl: "/",
            SearchSnippetsEnabled: false,
            BodyStore: EmptyContentBodyStore.Instance,
            DerivedRoutes: Array.Empty<(RouteInfo, DateTimeOffset)>(),
            SeoIndex: new Dictionary<string, SeoIndexEntry>
            {
                [route.OutputPath] = new SeoIndexEntry(route, canonical, "index,follow", true, null, document.Id, "post")
            },
            SeoModels: new Dictionary<string, SeoModel> { [route.OutputPath] = model },
            PluginExecutions: Array.Empty<PluginExecutionInfo>(),
            RenderedCount: 1,
            SkippedCount: 0,
            RenderReasons: new Dictionary<string, int>(),
            StageMetrics: new BuildStageMetrics(new Dictionary<string, long>(), new Dictionary<string, int>()),
            RoutedDocuments: [new RoutedContentDocument(document, route, document.PublishAt)]);
    }

    private static SeoModel CreateSeo(string canonical)
        => new()
        {
            Title = "Example",
            Description = "Canonical description",
            Canonical = canonical,
            JsonLd = ["{\"@type\":\"Article\",\"headline\":\"Example\"}"],
            GeoAuthor = new GeoAuthorModel
            {
                Name = "Example author",
                Url = "https://silushangxun.com/authors/example/",
                SameAs = ["https://social.example/example"]
            }
        };

    private static PublishUrlSnapshot Snapshot(params PublishUrlSnapshotRoute[] routes)
        => new("https://bukit.dev/schemas/publish-url-snapshot.v1.json", "https://silushangxun.com/", routes);
}
