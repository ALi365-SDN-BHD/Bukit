using System.Collections.Concurrent;
using Bukit.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
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
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            manifestEntries: null,
            currentKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            logger: new ConsoleLogger(LogLevel.Error),
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.RenderedCount);
        Assert.Equal("<p>lazy body</p>", renderer.LastPageContent);
    }

    [Fact]
    public async Task RenderSpecialListsAsync_DoesNotHydrateBodies_WhenModeIsAuto_AndTemplateDoesNotUseContent()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
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
            null,
            layoutsDir,
            "auto",
            outputDir,
            "template-hash",
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(3, result.RenderedCount);
        Assert.Equal(0, bodyStore.Count);
    }

    [Fact]
    public async Task RenderSpecialListsAsync_HydratesBodies_WhenModeIsAuto_AndTemplateUsesContent()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
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
            null,
            layoutsDir,
            "auto",
            outputDir,
            "template-hash",
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase));

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
            null,
            layoutsDir,
            "auto",
            outputDir,
            "template-hash",
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase));

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
            null,
            layoutsDir,
            "auto",
            outputDir,
            "template-hash",
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase));

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
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary"] = "summary"
            },
            Fields: null,
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

    private sealed class CaptureRenderer : ITemplateRenderer
    {
        public string? LastPageContent { get; private set; }

        public string RenderPage(string templateRelativePath, PageModel model)
        {
            LastPageContent = model.Page.Content;
            return model.Page.Content;
        }

        public string RenderList(string templateRelativePath, ListPageModel model) => string.Empty;
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
}
