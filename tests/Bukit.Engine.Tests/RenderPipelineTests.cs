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

    [Theory]
    [InlineData("child", "files")]
    [InlineData("parent", "files")]
    [InlineData("user", "files")]
    [InlineData("child", "size")]
    [InlineData("parent", "size")]
    [InlineData("user", "size")]
    [Trait("Defect", "KD-003")]
    public async Task ExecuteAsync_OverBudgetTemplateEdit_MatchesFullRenderAndRecoversReuse(string role, string limit)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-render-budget-" + Guid.NewGuid().ToString("N"));
        try
        {
            var context = CreateManifestContext(root) with
            {
                ParentLayoutsDir = Path.Combine(root, "parent-layouts"),
                UserLayoutsDir = Path.Combine(root, "user-layouts")
            };
            foreach (var directory in new[] { context.LayoutsDir, context.ParentLayoutsDir!, context.UserLayoutsDir! })
                Directory.CreateDirectory(directory);
            var activeLayouts = role switch
            {
                "parent" => context.ParentLayoutsDir!,
                "user" => context.UserLayoutsDir!,
                _ => context.LayoutsDir
            };
            File.WriteAllText(Path.Combine(activeLayouts, "000-marker.txt"), "seed");
            var templates = new Dictionary<string, string>
            {
                ["pages/post.html"] = "<p>old {{ page.title }}</p>",
                ["pages/list.html"] = "<ul>old {{ site.name }}</ul>"
            };
            Directory.CreateDirectory(Path.Combine(activeLayouts, "pages"));
            foreach (var (relative, text) in templates) File.WriteAllText(Path.Combine(activeLayouts, relative), text);
            // External body isolates template reuse from the separate inline-body fingerprint fast path.
            var document = ContentDocument.Create("hello", "Hello", "hello", DateTimeOffset.UnixEpoch, string.Empty, bodyKey: "hello-body");
            var bodyStore = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>
            {
                ["hello-body"] = new("<p>body</p>")
            });
            var pageRoute = new RouteInfo("/blog/hello/", "blog/hello/index.html", "pages/post.html");
            var listRoute = new RouteInfo("/blog/", "blog/index.html", "pages/list.html");
            RoutedContentDocument[] routed = [new(document, pageRoute)];
            RenderEntry[] entries = [RenderEntry.ForPage(document, pageRoute), RenderEntry.ForList(listRoute, routed, false)];
            var logger = new BudgetLogger();

            async Task<(ManifestSetupResult Setup, RenderPipelineResult Result)> BuildAsync(
                string outputName, bool incremental, int maxFiles = 10, long maxSize = 10000)
            {
                var variant = context with
                {
                    OutputDir = Path.Combine(root, outputName),
                    Documents = [document],
                    BodyStore = bodyStore,
                    Overrides = new ConfigOverrides { Incremental = incremental, CacheDir = Path.Combine(root, "cache-" + outputName) }
                };
                var setup = VariantManifestPlanner.Create(variant, variant.Overrides,
                    new DirectoryHashCache(logger, maxFiles: maxFiles, maxTotalSize: maxSize), logger);
                var result = await new RenderPipeline().ExecuteAsync(new RenderPipelineContext(
                    BodyStore: bodyStore,
                    Renderer: new ScribanTemplateRendererAdapter(context.LayoutsDir, context.ParentLayoutsDir, userLayoutsDir: context.UserLayoutsDir),
                    SiteModel: new SiteModel { Name = "test", Title = "Test", BaseUrl = "/", Language = "en" },
                    Collections: null, LayoutsDir: context.LayoutsDir, ListPageContentMode: "auto", OutputPathEncoding: "pretty",
                    OutputDir: variant.OutputDir, TemplateHash: setup.TemplateHash, RenderDependencyHash: "stable-dependency",
                    IncrementalEnabled: setup.IncrementalEnabled, Manifest: setup.Manifest, ManifestEntries: setup.ManifestEntries,
                    MaxDegreeOfParallelism: 1, Logger: logger, ListRouteGraph: ListRouteGraph.Empty,
                    RenderDocuments: routed, RoutedDocuments: routed, PrecomputedEntries: entries));
                setup.Manifest.OutputRoot = Path.GetFullPath(variant.OutputDir);
                setup.Manifest.OwnedOutputs.UnionWith(result.CurrentKeys.Keys);
                setup.Manifest.Save(setup.ManifestPath);
                return (setup, result);
            }

            var maxFiles = limit == "files" ? 1 : 10;
            var maxSize = limit == "size" ? 4 : 10000;
            var first = await BuildAsync("dist", true, maxFiles, maxSize);
            Assert.Equal(2, first.Result.RenderedCount);
            foreach (var entry in entries)
                Assert.Contains("old", File.ReadAllText(Path.Combine(root, "dist", entry.Route.OutputPath)), StringComparison.Ordinal);
            foreach (var (relative, text) in templates)
            {
                var path = Path.Combine(activeLayouts, relative);
                var length = new FileInfo(path).Length;
                var mtime = File.GetLastWriteTimeUtc(path);
                File.WriteAllText(path, text.Replace("old", "new", StringComparison.Ordinal));
                File.SetLastWriteTimeUtc(path, mtime);
                Assert.Equal(length, new FileInfo(path).Length);
                Assert.Equal(mtime, File.GetLastWriteTimeUtc(path));
            }

            var changed = await BuildAsync("dist", true, maxFiles, maxSize);
            await BuildAsync("reference", false);

            Assert.Equal(Directory.GetFiles(Path.Combine(root, "reference"), "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(Path.Combine(root, "reference"), path)).OrderBy(path => path, StringComparer.Ordinal),
                Directory.GetFiles(Path.Combine(root, "dist"), "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(Path.Combine(root, "dist"), path)).OrderBy(path => path, StringComparer.Ordinal));
            foreach (var entry in entries)
            {
                var actualPath = Path.Combine(root, "dist", entry.Route.OutputPath);
                Assert.Equal(File.ReadAllBytes(Path.Combine(root, "reference", entry.Route.OutputPath)), File.ReadAllBytes(actualPath));
                Assert.Contains("new", File.ReadAllText(actualPath), StringComparison.Ordinal);
            }
            Assert.False(changed.Setup.IncrementalEnabled);
            Assert.Empty(changed.Setup.TemplateHash);
            Assert.Null(changed.Setup.ManifestEntries);
            Assert.Empty(changed.Setup.Manifest.Entries);
            Assert.Equal(2, changed.Result.RenderedCount);
            Assert.Equal(0, changed.Result.SkippedCount);
            Assert.Contains(logger.Warnings, message => message.Contains("incremental reuse disabled for this variant", StringComparison.Ordinal));

            var recovered = await BuildAsync("dist", true);
            var unchanged = await BuildAsync("dist", true);
            Assert.True(recovered.Setup.IncrementalEnabled);
            Assert.Equal(2, recovered.Result.RenderedCount);
            Assert.True(unchanged.Setup.IncrementalEnabled);
            Assert.Equal(0, unchanged.Result.RenderedCount);
            Assert.Equal(2, unchanged.Result.SkippedCount);
        }
        finally { TestCleanup.DeleteDirectory(root); }
    }

    [Fact]
    public void ManifestPlanner_OverBudget_ClearsOnlyRenderReuseAndDoesNotDisableAnotherVariant()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-manifest-budget-" + Guid.NewGuid().ToString("N"));
        try
        {
            var context = CreateManifestContext(root);
            Directory.CreateDirectory(context.LayoutsDir);
            Directory.CreateDirectory(context.OutputDir);
            File.WriteAllText(Path.Combine(context.LayoutsDir, "a.txt"), "a");
            File.WriteAllText(Path.Combine(context.LayoutsDir, "b.txt"), "b");
            var existing = new BuildManifest { OutputRoot = Path.GetFullPath(context.OutputDir), TemplateHash = "old" };
            existing.Entries["old.html"] = new BuildManifestEntry { TemplateHash = "old" };
            existing.OwnedOutputs.Add("owned.txt");
            existing.Assets["asset.css"] = "asset-hash";
            File.WriteAllText(Path.Combine(context.OutputDir, "owned.txt"), "retain");
            existing.Save(PublicOutputLifecycle.ManifestPath(root, context.Overrides));
            var logger = new BudgetLogger();
            var cache = new DirectoryHashCache(logger, maxFiles: 1);

            var setup = VariantManifestPlanner.Create(context, context.Overrides, cache, logger);

            Assert.False(setup.IncrementalEnabled);
            Assert.Null(setup.ManifestEntries);
            Assert.Empty(setup.TemplateHash);
            Assert.Empty(setup.Manifest.Entries);
            Assert.Equal(["owned.txt"], setup.Manifest.OwnedOutputs);
            Assert.Equal("asset-hash", setup.Manifest.Assets["asset.css"]);
            Assert.Equal("retain", File.ReadAllText(Path.Combine(context.OutputDir, "owned.txt")));
            var safe = context with { LayoutsDir = Path.Combine(root, "safe-layouts"), OutputDir = Path.Combine(root, "safe-output") };
            Directory.CreateDirectory(safe.LayoutsDir);
            File.WriteAllText(Path.Combine(safe.LayoutsDir, "only.txt"), "safe");
            Assert.True(VariantManifestPlanner.Create(safe, safe.Overrides, cache, logger).IncrementalEnabled);
        }
        finally { TestCleanup.DeleteDirectory(root); }
    }

    [Theory]
    [InlineData("io")]
    [InlineData("permission")]
    [InlineData("other")]
    public void ManifestPlanner_NonBudgetHashFailure_PropagatesUnchanged(string kind)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-manifest-error-" + Guid.NewGuid().ToString("N"));
        try
        {
            var context = CreateManifestContext(root);
            Directory.CreateDirectory(context.LayoutsDir);
            Exception failure = kind switch
            {
                "io" => new IOException("read failed"),
                "permission" => new UnauthorizedAccessException("access denied"),
                _ => new InvalidOperationException("not a budget failure")
            };
            var logger = new BudgetLogger();
            var cache = new DirectoryHashCache(logger, _ => throw failure);

            Assert.Same(failure, Record.Exception(() => VariantManifestPlanner.Create(context, context.Overrides, cache, logger)));
            Assert.Empty(logger.Warnings);
        }
        finally { TestCleanup.DeleteDirectory(root); }
    }

    [Fact]
    public void ManifestPlanner_ExplicitNonincremental_DoesNotHashLayouts()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-manifest-nohash-" + Guid.NewGuid().ToString("N"));
        try
        {
            var context = CreateManifestContext(root);
            Directory.CreateDirectory(context.LayoutsDir);
            var calls = 0;
            var logger = new BudgetLogger();
            var cache = new DirectoryHashCache(logger, _ => { calls++; throw new IOException("must not read"); });

            var setup = VariantManifestPlanner.Create(context, new ConfigOverrides { Incremental = false }, cache, logger);

            Assert.False(setup.IncrementalEnabled);
            Assert.Equal(0, calls);
            Assert.Empty(logger.Warnings);
        }
        finally { TestCleanup.DeleteDirectory(root); }
    }

    private static BuildVariantContext CreateManifestContext(string root)
        => new(new AppConfig { Site = new SiteConfig { Name = "test", Title = "Test" }, Content = TestContent.Markdown() },
            root, new ConfigOverrides(), [], CanonicalContentGraph.Empty, EmptyContentBodyStore.Instance,
            Path.Combine(root, "dist"), "/", Path.Combine(root, "layouts"), Path.Combine(root, "assets"),
            Path.Combine(root, "static"), Path.Combine(root, "media"),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(), null, null, "en", DateTimeOffset.UnixEpoch);

    private sealed class BudgetLogger : ILogger
    {
        public ConcurrentQueue<string> Warnings { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Enqueue(message);
        public void Error(string message) => throw new InvalidOperationException(message);
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
