using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoPipelineTests
{
    [Fact]
    public void Execute_BuildsSeoIndexAndDetectsSeoMode()
    {
        var item = new ContentItem(
            Id: "hello",
            Title: "Hello World",
            Slug: "hello",
            PublishAt: DateTimeOffset.UnixEpoch,
            ContentHtml: "<p>Hello</p>",
            Fields: null,
            BodyKey: null);
        var route = new RouteInfo("/blog/hello/", "blog/hello/index.html", "pages/post.html");
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Language = "en",
                Seo = new SeoConfig { Enabled = true, RenderMode = "inject" }
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
        var logger = new RecordingLogger();

        var pipeline = new SeoPipeline();
        var result = pipeline.Execute(
            config,
            baseUrl: "/",
            renderQueue: new[] { (item, route) },
            listRoutes: new[] { new RouteInfo("/blog/", "blog/index.html", "pages/blog-list.html") },
            seoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            analytics: new AnalyticsModel { Enabled = false },
            logger);

        Assert.True(result.ShouldProvideSeoModel);
        Assert.True(result.ShouldInjectSeo);
        Assert.True(result.SeoIndex.Entries.Count > 0);
        Assert.True(result.SeoIndex.Models.Count > 0);
        Assert.NotNull(result.SeoBuilder);
        Assert.NotNull(result.HtmlPostProcessor);
    }

    [Fact]
    public void ExecuteDocuments_BuildsSeoIndexAndTypedCallbacksFromContentDocuments()
    {
        var publishedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var document = CreateDocument(
            id: "hello",
            title: "Hello Typed",
            slug: "hello",
            publishedAt: publishedAt,
            noIndex: true);
        var route = new RouteInfo("/blog/hello/", "blog/hello/index.html", "pages/post.html");
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Language = "en",
                Url = "https://example.com",
                Seo = new SeoConfig { Enabled = true, RenderMode = "inject" }
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
        var logger = new RecordingLogger();

        var pipeline = new SeoPipeline();
        var result = pipeline.ExecuteDocuments(
            config,
            baseUrl: "/docs",
            renderQueue: new[] { (document, route) },
            listRoutes: Array.Empty<RouteInfo>(),
            seoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            analytics: new AnalyticsModel { Enabled = false },
            logger);

        var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
        Assert.True(result.ShouldProvideSeoModel);
        Assert.True(result.SeoIndex.Entries.ContainsKey(key));
        Assert.False(result.SeoIndex.Entries[key].Indexable);
        Assert.Equal("hello", result.SeoIndex.Entries[key].SourceItemId);
        Assert.NotNull(result.DocumentSeoBuilder);
        Assert.Equal("Hello Typed", result.DocumentSeoBuilder(document, route).Title);
        Assert.Equal("noindex", result.DocumentSeoBuilder(document, route).Robots);
        Assert.NotNull(result.DocumentHtmlPostProcessor);
    }

    [Fact]
    public void Execute_WhenSeoDisabled_ReturnsNoCallbacks()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Seo = new SeoConfig { Enabled = false }
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
        var logger = new RecordingLogger();

        var pipeline = new SeoPipeline();
        var result = pipeline.Execute(
            config,
            baseUrl: "/",
            renderQueue: Array.Empty<(ContentItem, RouteInfo)>(),
            listRoutes: Array.Empty<RouteInfo>(),
            seoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            analytics: new AnalyticsModel { Enabled = false },
            logger);

        Assert.False(result.ShouldProvideSeoModel);
        Assert.Null(result.SeoBuilder);
    }

    private static ContentDocument CreateDocument(string id, string title, string slug, DateTimeOffset publishedAt, bool noIndex)
    {
        var record = new ContentRecord(
            Identity: new ContentIdentity(id, slug, id, "post", "published"),
            Presentation: new ContentPresentation(title, "Summary", "<p>Hello</p>", "en", Array.Empty<string>()),
            Classification: new ContentClassification("post", "posts", Array.Empty<string>(), Array.Empty<string>()),
            Ownership: new ContentOwnership("Alice", null, null, null),
            Lifecycle: new ContentLifecycle(publishedAt, null, null, null),
            Provenance: new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            Trust: new TrustMetadata(null, "draft", Array.Empty<string>()),
            Entities: Array.Empty<EntityRecord>(),
            Relations: Array.Empty<ContentRelation>(),
            Media: Array.Empty<MediaAsset>());

        return new ContentDocument(
            Record: record,
            Body: new ContentBodyRef("<p>Hello</p>", null, "# Hello", "Hello"),
            Route: new ContentRoutePolicy(null, null, "pages/post.html", null, null),
            Publish: new ContentPublishPolicy(Draft: false, NoIndex: noIndex, NoFollow: false, ExcludeFromFeed: false, ExcludeFromSearch: false, ExcludeFromSitemap: false, IsDataModule: false),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Diagnostics: Array.Empty<ContentDiagnostic>());
    }

    private sealed class RecordingLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
