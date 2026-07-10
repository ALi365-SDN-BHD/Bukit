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
        var item = ContentDocument.Create(
            id: "hello",
            title: "Hello World",
            slug: "hello",
            publishAt: DateTimeOffset.UnixEpoch,
            contentHtml: "<p>Hello</p>",
            bodyKey: null);
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
            Content = TestContent.Markdown()
        };
        var logger = new RecordingLogger();

        var pipeline = new SeoPipeline();
        var result = pipeline.Execute(
            config,
            baseUrl: "/",
            renderQueue: new[] { (item, route) }.ToRoutedDocuments(),
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
            Content = TestContent.Markdown()
        };
        var logger = new RecordingLogger();

        var pipeline = new SeoPipeline();
        var result = pipeline.Execute(
            config,
            baseUrl: "/",
            renderQueue: Array.Empty<RoutedContentDocument>(),
            listRoutes: Array.Empty<RouteInfo>(),
            seoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            analytics: new AnalyticsModel { Enabled = false },
            logger);

        Assert.False(result.ShouldProvideSeoModel);
        Assert.Null(result.SeoBuilder);
    }

    [Fact]
    public void Execute_OffModeProvidesModelAndDiagnosticsWithoutInjection()
    {
        var item = ContentDocument.Create(
            id: "hello",
            title: "Hello World",
            slug: "hello",
            publishAt: DateTimeOffset.UnixEpoch,
            contentHtml: "<p>Hello</p>");
        var route = new RouteInfo("/hello/", "hello/index.html", "pages/page.html");
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Seo = new SeoConfig { Enabled = true, RenderMode = "off", Diagnostics = "warn" }
            },
            Content = TestContent.Markdown()
        };
        var logger = new RecordingLogger();

        var result = new SeoPipeline().Execute(
            config,
            baseUrl: "/",
            renderQueue: new[] { (item, route) }.ToRoutedDocuments(),
            listRoutes: Array.Empty<RouteInfo>(),
            seoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            analytics: new AnalyticsModel { Enabled = false },
            logger);

        Assert.True(result.ShouldProvideSeoModel);
        Assert.False(result.ShouldInjectSeo);
        Assert.NotNull(result.SeoBuilder);
        Assert.NotNull(result.HtmlPostProcessor);

        var seo = result.SeoBuilder!(item, route);
        var page = new PageInfo { Title = item.Title, Url = route.Url, Content = string.Empty, Seo = seo };
        const string html = "<html><head><title>Theme title</title></head><body></body></html>";
        Assert.Equal(html, result.HtmlPostProcessor!(item, route, page, html));
        Assert.Contains(logger.Warnings, warning => warning.Contains("seo.document_title_mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_WhenPageDisablesSeoInjection_PreservesHtmlButStillRunsDiagnostics()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["seo_inject"] = new("boolean", false)
        };
        var item = ContentDocument.Create(
            id: "hello",
            title: "Hello World",
            slug: "hello",
            publishAt: DateTimeOffset.UnixEpoch,
            contentHtml: "<p>Hello</p>",
            fields: fields);
        var route = new RouteInfo("/hello/", "hello/index.html", "pages/page.html");
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Seo = new SeoConfig { Enabled = true, RenderMode = "inject", Diagnostics = "warn" }
            },
            Content = TestContent.Markdown()
        };
        var logger = new RecordingLogger();
        var result = new SeoPipeline().Execute(
            config,
            baseUrl: "/",
            renderQueue: new[] { (item, route) }.ToRoutedDocuments(),
            listRoutes: Array.Empty<RouteInfo>(),
            seoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            analytics: new AnalyticsModel { Enabled = false },
            logger);
        var seo = result.SeoBuilder!(item, route);
        var page = new PageInfo
        {
            Title = item.Title,
            Url = route.Url,
            Content = item.Body.Html ?? string.Empty,
            Seo = seo
        };
        const string html = "<html><head><title>Theme title</title></head><body></body></html>";

        var processed = result.HtmlPostProcessor!(item, route, page, html);

        Assert.Equal(html, processed);
        Assert.Contains(logger.Warnings, warning => warning.Contains("seo.document_title_mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_ListSeoBuilder_UsesGraphBackedIndexModel()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Seo = new SeoConfig { Enabled = true, RenderMode = "model" }
            },
            Content = TestContent.Markdown()
        };
        var graphRoute = new ListRoutePlan
        {
            RouteId = "collection:insights:2",
            Kind = ListRouteKind.CollectionPage,
            Url = "/insights/page-two/",
            OutputPath = "insights/page-two/index.html",
            Template = "pages/insight-list.html",
            Collection = "insight",
            PageNumber = 2,
            PageSize = 10,
            TotalItems = 30,
            CanonicalUrl = "/insights/p/2/",
            PrevUrl = "/insights/",
            NextUrl = "/insights/p/3/"
        };
        var graph = ListRouteGraph.Create(new[] { graphRoute });
        var logger = new RecordingLogger();

        var result = new SeoPipeline().Execute(
            config,
            baseUrl: "/",
            renderQueue: Array.Empty<RoutedContentDocument>(),
            listRoutes: graph.Routes.Select(route => route.ToRouteInfo()).ToArray(),
            seoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            analytics: new AnalyticsModel { Enabled = false },
            logger,
            graph);

        var model = result.ListSeoBuilder!(graphRoute.ToRouteInfo(), new PageInfo
        {
            Title = "Insights Page Two",
            Url = graphRoute.Url,
            Content = string.Empty
        });

        Assert.Equal("https://example.com/insights/p/2/", model.Canonical);
        Assert.Equal("https://example.com/insights/", model.Prev);
        Assert.Equal("https://example.com/insights/p/3/", model.Next);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }
}
