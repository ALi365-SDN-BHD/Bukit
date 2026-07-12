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
    [Theory]
    [InlineData("post")]
    [InlineData("company")]
    [InlineData("derived")]
    public async Task RenderPages_DoesNotApplyRouteMetadataToNonSingletonContent(string contentKind)
    {
        var item = ContentDocument.Create(
            id: contentKind,
            title: $"Original {contentKind}",
            slug: contentKind,
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: $"<p>{contentKind}</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = contentKind,
                ["collection"] = contentKind
            }));
        var route = new RouteInfo($"/{contentKind}/", $"{contentKind}/index.html", $"pages/{contentKind}.html");
        var renderer = new CaptureRenderer();
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));

        await PageRenderDispatcher.DispatchAsync(
            new[] { RenderEntry.ForPage(item.ToDocument(), route) },
            EmptyContentBodyStore.Instance,
            renderer,
            new SiteModel { Name = "site", Title = "site", BaseUrl = "/", Language = "en" },
            outputDir,
            templateHash: "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            manifestEntries: null,
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            logger: new ConsoleLogger(LogLevel.Error),
            cancellationToken: CancellationToken.None,
            routeMetadata: new Dictionary<string, Bukit.Engine.RouteMetadata.RouteMetadataEntry>
            {
                [route.Url] = new(route.Url, "Route metadata title", "Route metadata summary", "Route SEO", "Route SEO summary")
            });

        Assert.Equal($"Original {contentKind}", renderer.LastPageTitle);
    }

    [Fact]
    public async Task RenderPages_AppliesRouteMetadataToSingletonPageModel()
    {
        var item = ContentDocument.Create(
            id: "about",
            title: "Markdown About",
            slug: "about",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>About</p>");
        var route = new RouteInfo("/about/", "about/index.html", "pages/page.html");
        var renderer = new CaptureRenderer();
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));

        await PageRenderDispatcher.DispatchAsync(
            new[] { RenderEntry.ForPage(item.ToDocument(), route) },
            EmptyContentBodyStore.Instance,
            renderer,
            new SiteModel { Name = "site", Title = "site", BaseUrl = "/", Language = "zh-CN" },
            outputDir,
            templateHash: "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            manifestEntries: null,
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            logger: new ConsoleLogger(LogLevel.Error),
            cancellationToken: CancellationToken.None,
            routeMetadata: new Dictionary<string, Bukit.Engine.RouteMetadata.RouteMetadataEntry>
            {
                ["/about/"] = new("/about/", "关于我们", "Notion summary", null, null)
            });

        Assert.Equal("关于我们", renderer.LastPageTitle);
        Assert.Equal("Notion summary", renderer.LastPageSummary);
    }

    [Fact]
    public async Task RenderPages_HydratesBodyFromStore_WhenContentHtmlIsNull()
    {
        var item = ContentDocument.Create(
            id: "id-1",
            title: "Hello",
            slug: "hello",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            bodyKey: "body-1");

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

        var result = await PageRenderDispatcher.DispatchAsync(
            new[] { RenderEntry.ForPage(item.ToDocument(), route) },
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
        var item = ContentDocument.Create(
            id: "id-canonical",
            title: "Hello",
            slug: "hello",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary"] = new("text", "Canonical summary"),
                ["source"] = new("text", "notion"),
                ["review_status"] = new("text", "approved")
            },
            bodyKey: "body-canonical");

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

        await PageRenderDispatcher.DispatchAsync(
            new[] { RenderEntry.ForPage(item.ToDocument(), route) },
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
        var item = ContentDocument.Create(
            id: "id-1",
            title: "Hello",
            slug: "hello",
            publishAt: DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["bodyFingerprint"] = "body-v1"
            }),
            bodyKey: "body-1");

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

        var document = item.ToDocument();
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(document);
        var contentHash = IncrementalBuildEngine.ComputeStableContentHash(document, metadataHash);
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

        var result = await PageRenderDispatcher.DispatchAsync(
            new[] { RenderEntry.ForPage(document, route) },
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
            routed.ToRoutedDocuments(),
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
            CreateRoutedItems().ToRoutedDocuments(),
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
    public async Task RenderSpecialListsAsync_PassesFilteredListPageFields()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "index.html"), "{{ page.url }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "list.html"), "{{ page.url }}");

        var renderer = new CaptureRenderer();
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        await PageRenderDispatcher.RenderSpecialListsAsync(
            CreateFilteredRoutedItems().ToRoutedDocuments(),
            new CountingBodyStore(),
            renderer,
            CreateSiteModel(),
            CreateFilteredCollections(),
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

        Assert.True(renderer.ListPageFieldsByUrl.TryGetValue("/blog/malaysia/page/2/", out var fields));
        var pagination = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(fields["pagination"].Value);
        Assert.Equal(2, pagination["page"]);
        Assert.Equal(1, pagination["page_size"]);
        Assert.Equal(2, pagination["total_pages"]);
        Assert.Equal("/blog/malaysia/", pagination["prev_url"]);
        Assert.Null(pagination["next_url"]);

        var filter = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(fields["filter"].Value);
        Assert.Equal("country", filter["field"]);
        Assert.Equal("equals", filter["operator"]);
        Assert.Equal("Malaysia", filter["value"]);

        Assert.True(renderer.ListPageModelsByUrl.TryGetValue("/blog/malaysia/page/2/", out var model));
        Assert.Equal("/blog/malaysia/page/2/", model.Page?.Url);
        Assert.Equal("post", model.Collection?.Key);
        Assert.Equal(2, model.Pagination?.Page);
        Assert.Equal(1, model.Pagination?.PageSize);
        Assert.Equal(2, model.Pagination?.TotalPages);
        Assert.Equal(2, model.Pagination?.TotalItems);
        Assert.Equal("/blog/malaysia/", model.Pagination?.PrevUrl);
        Assert.Null(model.Pagination?.NextUrl);
        Assert.Equal("country", model.Filter?.Field);
        Assert.Equal("equals", model.Filter?.Operator);
        Assert.Equal("Malaysia", model.Filter?.Value);
        Assert.Equal(model.Pages.Count, model.Items?.Count);
        Assert.Equal(new[] { "Malaysia Two" }, model.Items?.Select(item => item.Title).ToArray());
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
            routed.ToRoutedDocuments(),
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
            routed.ToRoutedDocuments(),
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
            routed.ToRoutedDocuments(),
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

    private static List<(ContentDocument Item, RouteInfo Route)> CreateRoutedItems()
    {
        var item = ContentDocument.Create(
            id: "id-2",
            title: "Blog Post",
            slug: "blog-post",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "post",
                ["summary"] = "summary"
            }),
            bodyKey: "body-2");

        return new List<(ContentDocument Item, RouteInfo Route)>
        {
            (item, new RouteInfo("/blog/blog-post/", "blog/blog-post/index.html", "pages/post.html"))
        };
    }

    private static List<(ContentDocument Item, RouteInfo Route)> CreateFilteredRoutedItems()
    {
        var first = ContentDocument.Create(
            id: "id-malaysia-1",
            title: "Malaysia One",
            slug: "malaysia-one",
            publishAt: DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "post",
                ["country"] = "Malaysia",
                ["summary"] = "summary"
            }),
            bodyKey: "body-malaysia-1");

        var second = ContentDocument.Create(
            id: "id-malaysia-2",
            title: "Malaysia Two",
            slug: "malaysia-two",
            publishAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "post",
                ["country"] = "Malaysia",
                ["summary"] = "summary"
            }),
            bodyKey: "body-malaysia-2");

        return new List<(ContentDocument Item, RouteInfo Route)>
        {
            (first, new RouteInfo("/blog/malaysia-one/", "blog/malaysia-one/index.html", "pages/post.html")),
            (second, new RouteInfo("/blog/malaysia-two/", "blog/malaysia-two/index.html", "pages/post.html"))
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

    private static IReadOnlyDictionary<string, CollectionConfig> CreateFilteredCollections()
        => new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new()
            {
                Permalink = "/blog/{slug}/",
                ListRoute = "/blog/",
                ListTemplate = "pages/list.html",
                FilteredLists = new[]
                {
                    new FilteredListConfig
                    {
                        Field = "country",
                        Value = "Malaysia",
                        ListRoute = "/blog/malaysia/",
                        ListTemplate = "pages/list.html",
                        PageSize = 1,
                        UrlPattern = "page/{page}/"
                    }
                }
            }
        };

    private sealed class CaptureRenderer : ITemplateRenderer
    {
        public string? LastPageContent { get; private set; }
        public string? LastPageTitle { get; private set; }
        public string? LastPageSummary { get; private set; }
        public string? LastPageSource { get; private set; }
        public string? LastPageReviewStatus { get; private set; }
        public List<string> ListPageUrls { get; } = new();
        public Dictionary<string, IReadOnlyDictionary<string, ContentField>> ListPageFieldsByUrl { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ListPageModel> ListPageModelsByUrl { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string RenderPage(string templateRelativePath, PageModel model)
        {
            LastPageTitle = model.Page.Title;
            LastPageContent = model.Page.Content;
            LastPageSummary = model.Page.Summary;
            LastPageSource = model.Page.Provenance?.Source;
            LastPageReviewStatus = model.Page.Trust?.ReviewStatus;
            return model.Page.Content;
        }

        public string RenderList(string templateRelativePath, ListPageModel model)
        {
            var url = model.Page?.Url ?? string.Empty;
            ListPageUrls.Add(url);
            ListPageModelsByUrl[url] = model;
            if (model.Page?.Fields is { } fields)
            {
                ListPageFieldsByUrl[model.Page.Url] = fields;
            }

            return string.Empty;
        }
    }

    private sealed class CountingBodyStore : IContentBodyStore
    {
        public int Count { get; private set; }

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.FromResult(new ContentBody($"<p>{item.Id}</p>"));
        }
    }

    private sealed class ThrowingBodyStore : IContentBodyStore
    {
        public int Count { get; private set; }

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            Count++;
            throw new InvalidOperationException("Body store should not be used.");
        }
    }
}
