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

    private sealed class RecordingLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
