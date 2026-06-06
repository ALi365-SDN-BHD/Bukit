using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildReportPipelineProjectionResultTests
{
    [Fact]
    public void Execute_PassesProjectionResultsIntoPublishAudit()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-report-projection-results-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(outputDir, "post"));
        try
        {
            File.WriteAllText(Path.Combine(outputDir, "post", "index.html"), """
                <!doctype html>
                <html><head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
                <body><header></header><nav></nav><main><article><h1>Post</h1><time datetime="2026-06-05">June 5</time><p>Post body for machines.</p></article></main><footer></footer></body></html>
                """);
            var route = new RouteInfo("/post/", "post/index.html", "post.html");
            var item = Item();
            var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["post/index.html"] = new SeoIndexEntry(route, "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), item.Id, "post")
            };
            var seoModels = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
            {
                ["post/index.html"] = new SeoModel { Title = "Post", Description = "Post description", Canonical = "https://example.com/post/" }
            };
            var writer = new StubProjectionWriter(new PublishProjectionResult(
                PublishRepresentationRegistry.AggregateRepresentations().Single(x => x.Kind == "llms"),
                [new PublishRepresentationOutput("llms", "/post/", "llms.txt", Exists: true, Indexable: true)]));

            new BuildReportPipeline(writer).Execute(new BuildReportPipelineContext(
                Config: ConfigWithGeo(),
                Language: "en",
                OutputDir: outputDir,
                BaseUrl: "/",
                SearchSnippetsEnabled: false,
                BodyStore: NullContentBodyStore.Instance,
                Routed: [(item, route)],
                DerivedRouted: Array.Empty<(ContentItem Item, RouteInfo Route)>(),
                DerivedRoutes: Array.Empty<(RouteInfo Route, DateTimeOffset LastModified)>(),
                SeoIndex: seoIndex,
                SeoModels: seoModels,
                PluginExecutions: Array.Empty<PluginExecutionInfo>(),
                RenderedCount: 1,
                SkippedCount: 0,
                RenderReasons: new Dictionary<string, int>(),
                StageMetrics: new BuildStageMetrics(new Dictionary<string, long>(), new Dictionary<string, int>()),
                Logger: new ConsoleLogger(LogLevel.Error),
                DefaultLanguage: null,
                ContentGraph: ContentGraph()));

            var report = File.ReadAllText(Path.Combine(outputDir, ".bukit", "publish-audit-report.json"));
            Assert.Contains("\"kind\": \"llms\"", report, StringComparison.Ordinal);
            Assert.Contains("\"llmsIncluded\": true", report, StringComparison.Ordinal);
            Assert.DoesNotContain("publish.llms_missing_route", report, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private sealed class StubProjectionWriter : IContentProjectionWriter
    {
        private readonly PublishProjectionResult _result;

        public StubProjectionWriter(PublishProjectionResult result)
        {
            _result = result;
        }

        public IReadOnlyList<PublishProjectionResult> Write(PublishProjectionContext context) => [_result];
    }

    private static ContentItem Item()
        => new(
            "post-1",
            "Post",
            "post",
            DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
            "<p>Post body</p>",
            Fields: null);

    private static CanonicalContentGraph ContentGraph()
        => new(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>Post body for machines.</p></article>", "en", []),
                new ContentClassification("post", "post", [], []),
                new ContentOwnership("Ali", "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), DateTimeOffset.Parse("2026-06-05T01:00:00Z"), null, null),
                new ProvenanceRecord("markdown", "https://example.com/original", [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                [],
                [],
                [])
        ], []);

    private static AppConfig ConfigWithGeo()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Seo = new SeoConfig { Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true } }
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
}
