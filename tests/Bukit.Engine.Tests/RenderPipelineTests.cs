using System.Collections.Concurrent;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RenderPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_RendersPagesAndSpecialListsAndAggregatesResult()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-render-pipeline-tests", Guid.NewGuid().ToString("N"));
        var layoutsDir = Path.Combine(outputDir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        var item = ContentDocument.Create(
            id: "hello",
            title: "Hello",
            slug: "hello",
            publishAt: DateTimeOffset.UnixEpoch,
            contentHtml: "<p>Hello</p>",
            bodyKey: null);
        var route = new RouteInfo("/blog/hello/", "blog/hello/index.html", "pages/post.html");
        var routedDocuments = new[] { (item, route) }.ToRoutedDocuments();
        var renderer = new CaptureRenderer();
        var pipeline = new RenderPipeline();

        var result = await pipeline.ExecuteAsync(new RenderPipelineContext(
            BodyStore: EmptyContentBodyStore.Instance,
            Renderer: renderer,
            SiteModel: new SiteModel { Name = "test", Title = "Test", BaseUrl = "/", Language = "en" },
            Collections: CreateCollections(),
            LayoutsDir: layoutsDir,
            ListPageContentMode: "auto",
            OutputPathEncoding: "pretty",
            OutputDir: outputDir,
            TemplateHash: string.Empty,
            RenderDependencyHash: string.Empty,
            IncrementalEnabled: false,
            Manifest: new BuildManifest(),
            ManifestEntries: null,
            MaxDegreeOfParallelism: 1,
            Logger: new ConsoleLogger(LogLevel.Error),
            ListRouteGraph: ListRouteGraphBuilder.Build(routedDocuments, CreateCollections(), "pretty"),
            RenderDocuments: routedDocuments,
            RoutedDocuments: routedDocuments), CancellationToken.None);

        Assert.Equal(4, result.RenderedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(1, result.RenderReasons["full_render"]);
        Assert.Contains("blog/hello/index.html", result.CurrentKeys.Keys);
        Assert.Contains("index.html", result.CurrentKeys.Keys);
        Assert.Contains("blog/index.html", result.CurrentKeys.Keys);
        Assert.Contains("pages/index.html", result.CurrentKeys.Keys);
        Assert.Equal(1, renderer.PageRenderCount);
        Assert.True(renderer.ListRenderCount >= 1, $"Expected at least 1 list render, got {renderer.ListRenderCount}");
        Assert.True(File.Exists(Path.Combine(outputDir, "blog", "hello", "index.html")));
        Assert.True(File.Exists(Path.Combine(outputDir, "index.html")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenIncrementalEnabled_AppliesManifestEntries()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-render-pipeline-tests", Guid.NewGuid().ToString("N"));
        var layoutsDir = Path.Combine(outputDir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        var item = ContentDocument.Create(
            id: "hello",
            title: "Hello",
            slug: "hello",
            publishAt: DateTimeOffset.UnixEpoch,
            contentHtml: "<p>Hello</p>",
            bodyKey: null);
        var route = new RouteInfo("/blog/hello/", "blog/hello/index.html", "pages/post.html");
        var routedDocuments = new[] { (item, route) }.ToRoutedDocuments();
        var manifest = new BuildManifest();
        var manifestEntries = new ConcurrentDictionary<string, BuildManifestEntry>(StringComparer.Ordinal);
        var pipeline = new RenderPipeline();

        await pipeline.ExecuteAsync(new RenderPipelineContext(
            BodyStore: EmptyContentBodyStore.Instance,
            Renderer: new CaptureRenderer(),
            SiteModel: new SiteModel { Name = "test", Title = "Test", BaseUrl = "/", Language = "en" },
            Collections: CreateCollections(),
            LayoutsDir: layoutsDir,
            ListPageContentMode: "auto",
            OutputPathEncoding: "pretty",
            OutputDir: outputDir,
            TemplateHash: "template-v1",
            RenderDependencyHash: string.Empty,
            IncrementalEnabled: true,
            Manifest: manifest,
            ManifestEntries: manifestEntries,
            MaxDegreeOfParallelism: 1,
            Logger: new ConsoleLogger(LogLevel.Error),
            ListRouteGraph: ListRouteGraphBuilder.Build(routedDocuments, CreateCollections(), "pretty"),
            RenderDocuments: routedDocuments,
            RoutedDocuments: routedDocuments), CancellationToken.None);

        Assert.True(manifest.Entries.ContainsKey("blog/hello/index.html"));
        Assert.True(manifest.Entries.ContainsKey("index.html"));
    }

    private static IReadOnlyDictionary<string, CollectionConfig> CreateCollections()
        => new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new()
            {
                Permalink = "/blog/{slug}/",
                ListRoute = "/blog/",
                ListTemplate = "pages/list.html"
            },
            ["page"] = new()
            {
                Permalink = "/pages/{slug}/",
                ListRoute = "/pages/",
                ListTemplate = "pages/list.html"
            }
        };

    private sealed class CaptureRenderer : ITemplateRenderer
    {
        public int PageRenderCount { get; private set; }
        public int ListRenderCount { get; private set; }

        public string RenderPage(string templateRelativePath, PageModel model)
        {
            PageRenderCount++;
            return model.Page.Content;
        }

        public string RenderList(string templateRelativePath, ListPageModel model)
        {
            ListRenderCount++;
            return string.Join('\n', model.Pages.Select(page => page.Title));
        }
    }
}
