using System.Collections.Concurrent;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Engine.RouteMetadata;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RenderPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_RendersTaxonomyRouteMetadataFromMatchingGraphPlans()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-render-pipeline-tests", Guid.NewGuid().ToString("N"));
        var layoutsDir = Path.Combine(outputDir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        var derived = new[]
        {
            Derived("category-index", "Derived categories", "Derived category summary", "/insights/category/", "insights/category/index.html"),
            Derived("category-market", "Derived market", "Derived market summary", "/insights/category/market/", "insights/category/market/index.html"),
            Derived("category-market-page-2", "Derived market page 2", "Derived market page 2 summary", "/insights/category/market/page/2/", "insights/category/market/page/2/index.html")
        };
        var graph = ListRouteGraph.Create(new[]
        {
            TaxonomyPlan("taxonomy:category:index", ListRouteKind.TaxonomyIndex, derived[0].Route, "/insights/category/", page: 1),
            TaxonomyPlan("taxonomy:category:market:1", ListRouteKind.TaxonomyTermPage, derived[1].Route, "/insights/category/market/", page: 1),
            TaxonomyPlan("taxonomy:category:market:2", ListRouteKind.TaxonomyTermPage, derived[2].Route, "/insights/category/market/", page: 2)
        });
        var metadata = new Dictionary<string, RouteMetadataEntry>
        {
            ["/insights/category/"] = new("/insights/category/", "资讯分类", "浏览全部资讯分类", null, null),
            ["/insights/category/market/"] = new("/insights/category/market/", "市场观察", "市场观察资讯", null, null)
        };
        graph = ListRouteGraphBuilder.ApplyRouteMetadata(graph, metadata);
        Assert.All(graph.Routes, route => Assert.True(route.RouteMetadataApplied));
        var renderer = new CaptureRenderer();

        await new RenderPipeline().ExecuteAsync(new RenderPipelineContext(
            BodyStore: EmptyContentBodyStore.Instance,
            Renderer: renderer,
            SiteModel: new SiteModel { Name = "test", Title = "Test", BaseUrl = "/", Language = "zh-CN" },
            Collections: null,
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
            ListRouteGraph: graph,
            RenderDocuments: derived,
            RoutedDocuments: Array.Empty<RoutedContentDocument>(),
            RouteMetadata: metadata), CancellationToken.None);

        Assert.Equal(("资讯分类", "浏览全部资讯分类"), renderer.PageMetadata["/insights/category/"]);
        Assert.Equal(("市场观察", "市场观察资讯"), renderer.PageMetadata["/insights/category/market/"]);
        Assert.Equal(("市场观察 - 第 2 页", "市场观察资讯 第 2 页，显示第 3-4 项，共 5 项。"), renderer.PageMetadata["/insights/category/market/page/2/"]);
    }

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

    [Fact]
    public async Task ExecuteAsync_Incremental_ProducesDeterministicManifestAcrossConcurrentRuns()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-render-pipeline-manifest", Guid.NewGuid().ToString("N"));
        var layoutsDir = Path.Combine(root, "layouts");
        Directory.CreateDirectory(layoutsDir);
        var routedDocuments = Enumerable.Range(0, 24)
            .Select(index =>
            {
                var item = ContentDocument.Create(
                    id: $"post-{index}",
                    title: $"Post {index}",
                    slug: $"post-{index}",
                    publishAt: DateTimeOffset.UnixEpoch.AddMinutes(index),
                    contentHtml: $"<p>Post {index}</p>",
                    bodyKey: null);
                return (item, new RouteInfo(
                    $"/blog/post-{index}/",
                    $"blog/post-{index}/index.html",
                    "pages/post.html"));
            })
            .ToRoutedDocuments();
        var graph = ListRouteGraphBuilder.Build(routedDocuments, CreateCollections(), "pretty");
        string? expectedManifest = null;

        try
        {
            for (var iteration = 0; iteration < 25; iteration++)
            {
                var outputDir = Path.Combine(root, "output", iteration.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var manifest = new BuildManifest();
                var manifestEntries = new ConcurrentDictionary<string, BuildManifestEntry>(StringComparer.Ordinal);

                var result = await new RenderPipeline().ExecuteAsync(new RenderPipelineContext(
                    BodyStore: EmptyContentBodyStore.Instance,
                    Renderer: new ConcurrentDeterministicRenderer(),
                    SiteModel: new SiteModel { Name = "test", Title = "Test", BaseUrl = "/", Language = "en" },
                    Collections: CreateCollections(),
                    LayoutsDir: layoutsDir,
                    ListPageContentMode: "auto",
                    OutputPathEncoding: "pretty",
                    OutputDir: outputDir,
                    TemplateHash: "template-v1",
                    RenderDependencyHash: "dependency-v1",
                    IncrementalEnabled: true,
                    Manifest: manifest,
                    ManifestEntries: manifestEntries,
                    MaxDegreeOfParallelism: 8,
                    Logger: new ConsoleLogger(LogLevel.Error),
                    ListRouteGraph: graph,
                    RenderDocuments: routedDocuments,
                    RoutedDocuments: routedDocuments), CancellationToken.None);

                var manifestPath = Path.Combine(root, $"manifest-{iteration}.json");
                manifest.Save(manifestPath);
                var serialized = await File.ReadAllTextAsync(manifestPath);
                expectedManifest ??= serialized;

                Assert.Equal(expectedManifest, serialized);
                Assert.Equal(result.RenderedCount, manifest.Entries.Count);
                Assert.Equal(manifestEntries.Count, manifest.Entries.Count);
            }
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, true);
        }
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

    private static RoutedContentDocument Derived(string id, string title, string summary, string url, string outputPath)
    {
        var document = ContentDocument.Create(
            id,
            title,
            id,
            DateTimeOffset.UtcNow,
            $"<p>{title}</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "derived",
                ["collection"] = "page",
                ["summary"] = summary
            }));
        return new RoutedContentDocument(document, new RouteInfo(url, outputPath, "pages/taxonomy.html"));
    }

    private static ListRoutePlan TaxonomyPlan(
        string id,
        ListRouteKind kind,
        RouteInfo route,
        string metadataRouteUrl,
        int page) => new()
    {
        RouteId = id,
        Kind = kind,
        Url = route.Url,
        OutputPath = route.OutputPath,
        Template = route.Template,
        MetadataRouteUrl = metadataRouteUrl,
        PageNumber = page,
        PageSize = kind == ListRouteKind.TaxonomyTermPage ? 2 : null,
        TotalItems = kind == ListRouteKind.TaxonomyTermPage ? 5 : 0,
        CanonicalUrl = route.Url
    };

    private sealed class CaptureRenderer : ITemplateRenderer
    {
        public int PageRenderCount { get; private set; }
        public int ListRenderCount { get; private set; }
        public Dictionary<string, (string Title, string? Summary)> PageMetadata { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string RenderPage(string templateRelativePath, PageModel model)
        {
            PageRenderCount++;
            PageMetadata[model.Page.Url] = (model.Page.Title, model.Page.Summary);
            return model.Page.Content;
        }

        public string RenderList(string templateRelativePath, ListPageModel model)
        {
            ListRenderCount++;
            return string.Join('\n', model.Pages.Select(page => page.Title));
        }
    }

    private sealed class ConcurrentDeterministicRenderer : ITemplateRenderer
    {
        public string RenderPage(string templateRelativePath, PageModel model)
            => $"page:{model.Page.Url}";

        public string RenderList(string templateRelativePath, ListPageModel model)
            => $"list:{model.Page?.Url ?? string.Empty}:{string.Join(',', model.Pages.Select(page => page.Url))}";
    }
}
