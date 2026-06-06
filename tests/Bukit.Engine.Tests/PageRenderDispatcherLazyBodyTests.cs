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

public sealed class PageRenderDispatcherLazyBodyTests
{
    [Fact]
    public async Task RenderPages_HydratesBodyFromStore_WhenContentHtmlIsNull()
    {
        var item = new ContentItem(
            Id: "id-1",
            Title: "Hello",
            Slug: "hello",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: null,
            BodyKey: "body-1");

        var route = new RouteInfo("/pages/hello/", "pages/hello/index.html", "pages/page.html");
        var renderer = new CaptureRenderer();
        var siteModel = new SiteModel
        {
            Name = "site",
            Title = "site",
            BaseUrl = "/",
            Language = "zh-CN"
        };

        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        var result = await PageRenderDispatcher.RenderPagesAsync(
            new List<(ContentItem Item, RouteInfo Route)> { (item, route) },
            new DictionaryContentBodyStore(new Dictionary<string, ContentBody>(StringComparer.OrdinalIgnoreCase)
            {
                ["body-1"] = new("<p>lazy body</p>")
            }),
            renderer,
            siteModel,
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

        Assert.Equal(1, result.RenderedCount);
        Assert.Equal("<p>lazy body</p>", renderer.LastPageContent);
    }

    [Fact]
    public async Task RenderPages_PopulatesCanonicalSummaryTrustAndProvenance()
    {
        var item = new ContentItem(
            Id: "id-canonical",
            Title: "Hello",
            Slug: "hello",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary"] = new("text", "Canonical summary"),
                ["source"] = new("text", "notion"),
                ["review_status"] = new("text", "approved")
            },
            BodyKey: "body-canonical");

        var route = new RouteInfo("/pages/hello/", "pages/hello/index.html", "pages/page.html");
        var renderer = new CaptureRenderer();
        var siteModel = new SiteModel
        {
            Name = "site",
            Title = "site",
            BaseUrl = "/",
            Language = "zh-CN"
        };

        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        await PageRenderDispatcher.RenderPagesAsync(
            new List<(ContentItem Item, RouteInfo Route)> { (item, route) },
            new DictionaryContentBodyStore(new Dictionary<string, ContentBody>(StringComparer.OrdinalIgnoreCase)
            {
                ["body-canonical"] = new("<p>lazy body</p>")
            }),
            renderer,
            siteModel,
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

        Assert.Equal("Canonical summary", renderer.LastPageSummary);
        Assert.Equal("notion", renderer.LastPageSource);
        Assert.Equal("approved", renderer.LastPageReviewStatus);
    }

    [Fact]
    public async Task RenderPages_SkipsWithoutHydratingBody_WhenStableFingerprintMatchesManifest()
    {
        var item = new ContentItem(
            Id: "id-1",
            Title: "Hello",
            Slug: "hello",
            PublishAt: DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
            ContentHtml: null,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["bodyFingerprint"] = new("text", "body-v1")
            },
            BodyKey: "body-1");

        var route = new RouteInfo("/pages/hello/", "pages/hello/index.html", "pages/page.html");
        var renderer = new CaptureRenderer();
        var siteModel = new SiteModel
        {
            Name = "site",
            Title = "site",
            BaseUrl = "/",
            Language = "zh-CN"
        };

        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(outputDir, route.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, "<p>cached</p>");

        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var contentHash = IncrementalBuildEngine.ComputeStableContentHash(item, metadataHash);
        var manifest = new BuildManifest
        {
            Entries = new Dictionary<string, BuildManifestEntry>(StringComparer.Ordinal)
            {
                [BuildPathUtils.NormalizeRelPath(route.OutputPath)] = new()
                {
                    OutputPath = BuildPathUtils.NormalizeRelPath(route.OutputPath),
                    Url = route.Url,
                    Template = route.Template,
                    MetadataHash = metadataHash,
                    ContentHash = contentHash,
                    RouteHash = IncrementalBuildEngine.ComputeRouteHash(route),
                    TemplateHash = "template-hash"
                }
            }
        };

        var manifestEntries = new ConcurrentDictionary<string, BuildManifestEntry>(manifest.Entries, StringComparer.Ordinal);
        var bodyStore = new ThrowingBodyStore();

        var result = await PageRenderDispatcher.RenderPagesAsync(
            new List<(ContentItem Item, RouteInfo Route)> { (item, route) },
            bodyStore,
            renderer,
            siteModel,
            outputDir,
            templateHash: "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: true,
            manifest: manifest,
            manifestEntries: manifestEntries,
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            logger: new ConsoleLogger(LogLevel.Error),
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, result.RenderedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, bodyStore.Count);
    }

    [Fact]
    public async Task RenderSpecialListsAsync_DoesNotHydrateBodies_WhenModeIsAuto_AndTemplateDoesNotUseContent()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "index.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "index.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "list.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");

        var routed = CreateRoutedItems();
        var bodyStore = new CountingBodyStore();
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        var result = await PageRenderDispatcher.RenderSpecialListsAsync(
            routed,
            bodyStore,
            new CaptureRenderer(),
            CreateSiteModel(),
            CreateCollections(),
            layoutsDir,
            "auto",
            "none",
            outputDir,
            "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            cancellationToken: CancellationToken.None);

        Assert.Equal(3, result.RenderedCount);
        Assert.Equal(0, bodyStore.Count);
    }

    [Fact]
    public async Task RenderSpecialListsAsync_PassesListRouteAsPageUrl()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "index.html"), "{{ page.url }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "index.html"), "{{ page.url }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "list.html"), "{{ page.url }}");

        var renderer = new CaptureRenderer();
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        await PageRenderDispatcher.RenderSpecialListsAsync(
            CreateRoutedItems(),
            new CountingBodyStore(),
            renderer,
            CreateSiteModel(),
            CreateCollections(),
            layoutsDir,
            "auto",
            "none",
            outputDir,
            "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            cancellationToken: CancellationToken.None);

        Assert.Contains("/", renderer.ListPageUrls);
        Assert.Contains("/blog/", renderer.ListPageUrls);
        Assert.Contains("/pages/", renderer.ListPageUrls);
    }

    [Fact]
    public async Task RenderSpecialListsAsync_HydratesBodies_WhenModeIsAuto_AndTemplateUsesContent()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "index.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "index.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "list.html"), "{{ for p in pages }}{{ p.content }}{{ end }}");

        var routed = CreateRoutedItems();
        var bodyStore = new CountingBodyStore();
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        await PageRenderDispatcher.RenderSpecialListsAsync(
            routed,
            bodyStore,
            new CaptureRenderer(),
            CreateSiteModel(),
            CreateCollections(),
            layoutsDir,
            "auto",
            "none",
            outputDir,
            "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            cancellationToken: CancellationToken.None);

        Assert.True(bodyStore.Count > 0);
    }

    [Fact]
    public async Task RenderSpecialListsAsync_DoesNotHydrateBodies_WhenManifestDeclaresNoContent()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "bukit.templates.yaml"), """
                                                                                         templates:
                                                                                           pages/index.html:
                                                                                             capabilities:
                                                                                               needs_page_content: false
                                                                                           pages/list.html:
                                                                                             capabilities:
                                                                                               needs_page_content: false
                                                                                         """);
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "index.html"), "{{ for p in pages }}{{ include \"partials/card.html\" /}}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "index.html"), "{{ for p in pages }}{{ include \"partials/card.html\" /}}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "list.html"), "{{ for p in pages }}{{ include \"partials/card.html\" /}}{{ end }}");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "partials"));
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "partials", "card.html"), "{{ p.title }}");

        var routed = CreateRoutedItems();
        var bodyStore = new CountingBodyStore();
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        await PageRenderDispatcher.RenderSpecialListsAsync(
            routed,
            bodyStore,
            new CaptureRenderer(),
            CreateSiteModel(),
            CreateCollections(),
            layoutsDir,
            "auto",
            "none",
            outputDir,
            "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, bodyStore.Count);
    }

    [Fact]
    public async Task RenderSpecialListsAsync_HydratesBodies_WhenManifestDeclaresContentNeeded()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "bukit.templates.yaml"), """
                                                                                         templates:
                                                                                           pages/index.html:
                                                                                             capabilities:
                                                                                               needs_page_content: true
                                                                                           pages/list.html:
                                                                                             capabilities:
                                                                                               needs_page_content: true
                                                                                         """);
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "index.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "index.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "list.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");

        var routed = CreateRoutedItems();
        var bodyStore = new CountingBodyStore();
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        await PageRenderDispatcher.RenderSpecialListsAsync(
            routed,
            bodyStore,
            new CaptureRenderer(),
            CreateSiteModel(),
            CreateCollections(),
            layoutsDir,
            "auto",
            "none",
            outputDir,
            "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            cancellationToken: CancellationToken.None);

        Assert.True(bodyStore.Count > 0);
    }

    private static List<(ContentItem Item, RouteInfo Route)> CreateRoutedItems()
    {
        var item = new ContentItem(
            Id: "id-2",
            Title: "Blog Post",
            Slug: "blog-post",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = new("text", "post"),
                ["summary"] = new("text", "summary")
            },
            BodyKey: "body-2");

        return new List<(ContentItem Item, RouteInfo Route)>
        {
            (item, new RouteInfo("/blog/blog-post/", "blog/blog-post/index.html", "pages/post.html"))
        };
    }

    private static SiteModel CreateSiteModel()
    {
        return new SiteModel
        {
            Name = "site",
            Title = "site",
            BaseUrl = "/",
            Language = "zh-CN"
        };
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
        public string? LastPageContent { get; private set; }
        public string? LastPageSummary { get; private set; }
        public string? LastPageSource { get; private set; }
        public string? LastPageReviewStatus { get; private set; }
        public List<string> ListPageUrls { get; } = new();

        public string RenderPage(string templateRelativePath, PageModel model)
        {
            LastPageContent = model.Page.Content;
            LastPageSummary = model.Page.Summary;
            LastPageSource = model.Page.Provenance?.Source;
            LastPageReviewStatus = model.Page.Trust?.ReviewStatus;
            return model.Page.Content;
        }

        public string RenderList(string templateRelativePath, ListPageModel model)
        {
            ListPageUrls.Add(model.Page?.Url ?? string.Empty);
            return string.Empty;
        }
    }

    private sealed class CountingBodyStore : IContentBodyStore
    {
        public int Count { get; private set; }

        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.FromResult(new ContentBody($"<p>{item.Id}</p>"));
        }
    }

    private sealed class ThrowingBodyStore : IContentBodyStore
    {
        public int Count { get; private set; }

        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            Count++;
            throw new InvalidOperationException("Body store should not be used.");
        }
    }
}
