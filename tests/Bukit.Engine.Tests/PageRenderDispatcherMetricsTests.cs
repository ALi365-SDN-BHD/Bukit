using System.Collections.Concurrent;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PageRenderDispatcherMetricsTests
{
    [Fact]
    public async Task RenderPagesAsync_CollectsContentHashBodyLoadAndRenderMetrics()
    {
        var item = ContentDocument.Create(
            id: "id-1",
            title: "Hello",
            slug: "hello",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            bodyKey: "body-1");

        var route = new RouteInfo("/pages/hello/", "pages/hello/index.html", "pages/page.html");
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        var result = await PageRenderDispatcher.DispatchAsync(
            new[] { RenderEntry.ForPage(item.ToDocument(), route) },
            new DictionaryContentBodyStore(new Dictionary<string, ContentBody>(StringComparer.OrdinalIgnoreCase)
            {
                ["body-1"] = new("<p>lazy body</p>")
            }),
            new CaptureRenderer(),
            new SiteModel
            {
                Name = "site",
                Title = "site",
                BaseUrl = "/",
                Language = "zh-CN"
            },
            outputDir,
            templateHash: "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            manifestEntries: null,
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            logger: new ConsoleLogger(LogLevel.Error),
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.StageMetrics.Counts["metadataHash"]);
        Assert.Equal(1, result.StageMetrics.Counts["bodyLoad"]);
        Assert.Equal(1, result.StageMetrics.Counts["pageRender"]);
        Assert.True(result.StageMetrics.DurationsMs["metadataHash"] >= 0);
        Assert.True(result.StageMetrics.DurationsMs["bodyLoad"] >= 0);
        Assert.True(result.StageMetrics.DurationsMs["pageRender"] >= 0);
    }

    [Theory]
    [InlineData("new", "new_page", 1, 0, 1)]
    [InlineData("missing", "output_missing", 1, 0, 1)]
    [InlineData("template", "template_changed", 1, 0, 1)]
    [InlineData("metadata", "content_changed", 1, 0, 1)]
    [InlineData("route", "route_changed", 1, 0, 1)]
    [InlineData("dependency", "render_dependency_changed", 1, 0, 1)]
    [InlineData("body", "content_changed", 1, 0, 2)]
    [InlineData("", "unchanged", 0, 1, 1)]
    [InlineData("new missing template metadata route dependency body", "new_page", 1, 0, 1)]
    [InlineData("missing template metadata route dependency body", "output_missing", 1, 0, 1)]
    [InlineData("template metadata route dependency body", "template_changed", 1, 0, 1)]
    [InlineData("metadata route dependency body", "content_changed", 1, 0, 1)]
    [InlineData("route dependency body", "route_changed", 1, 0, 1)]
    [InlineData("dependency body", "render_dependency_changed", 1, 0, 1)]
    public async Task DispatchAsync_ReportsFirstChangedInputWithoutExtraBodyReads(
        string changes, string expectedReason, int rendered, int skipped, int bodyReads)
    {
        var document = ContentDocument.Create(
            "id-1", "Hello", "hello", DateTimeOffset.UnixEpoch, null, bodyKey: "body-1");
        var route = new RouteInfo("/hello/", "hello/index.html", "page.html");
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(document);
        const string html = "<p>body</p>";
        var entries = new ConcurrentDictionary<string, BuildManifestEntry>();
        if (!changes.Contains("new", StringComparison.Ordinal))
        {
            entries[route.OutputPath] = new BuildManifestEntry
            {
                OutputPath = route.OutputPath,
                TemplateHash = changes.Contains("template", StringComparison.Ordinal) ? "old" : "template",
                MetadataHash = changes.Contains("metadata", StringComparison.Ordinal) ? "old" : metadataHash,
                RouteHash = changes.Contains("route", StringComparison.Ordinal) ? "old" : IncrementalBuildEngine.ComputeRouteHash(route),
                RenderDependencyHash = changes.Contains("dependency", StringComparison.Ordinal) ? "old" : "dependency",
                ContentHash = changes.Contains("body", StringComparison.Ordinal) ? "old" : IncrementalBuildEngine.ComputeContentHash(document, metadataHash, html)
            };
        }
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-metrics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(outputDir, "hello"));
        try
        {
            var outputPath = Path.Combine(outputDir, route.OutputPath);
            if (!changes.Contains("missing", StringComparison.Ordinal))
                File.WriteAllText(outputPath, "previous output");
            var bodyStore = new CountingBodyStore(html);
            var result = await PageRenderDispatcher.DispatchAsync(
                [RenderEntry.ForPage(document, route)], bodyStore, new CaptureRenderer(),
                new SiteModel { Name = "site", Title = "site", BaseUrl = "/", Language = "en" },
                outputDir, "template", "dependency", true, new BuildManifest(), entries,
                new ConcurrentDictionary<string, byte>(), 1, new ConsoleLogger(LogLevel.Error), CancellationToken.None);

            Assert.Equal(rendered, result.RenderedCount);
            Assert.Equal(skipped, result.SkippedCount);
            Assert.Equal(new KeyValuePair<string, int>(expectedReason, 1), Assert.Single(result.RenderReasons));
            Assert.Equal(bodyReads, bodyStore.Reads);
            Assert.Equal(rendered == 1 ? html : "previous output", File.ReadAllText(outputPath));
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    private sealed class CountingBodyStore(string html) : IContentBodyStore
    {
        public int Reads { get; private set; }
        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(new ContentBody(html));
        }
    }

    private sealed class CaptureRenderer : ITemplateRenderer
    {
        public string RenderPage(string templatePath, PageModel model) => model.Page.Content;

        public string RenderList(string templatePath, ListPageModel model) => string.Join('\n', model.Pages.Select(x => x.Title));
    }
}
