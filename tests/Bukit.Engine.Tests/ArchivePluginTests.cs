using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ArchivePluginTests
{
    private static ContentDocument CreateItem(string id, string title, string slug, DateTimeOffset publishAt, string? collection = null)
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        meta["type"] = collection ?? "post";
        meta["collection"] = collection ?? "post";

        return ContentDocument.Create(
            id: id,
            title: title,
            slug: slug,
            publishAt: publishAt,
            contentHtml: "<p>content</p>",
            fields: ContentFieldReader.ToFieldMap(meta));
    }

    private static (BuildContext Context, AppConfig Config) CreateContext(
        List<(ContentDocument Item, RouteInfo Route)> routed,
        string collectionKey = "post")
    {
        var routePrefix = string.Equals(collectionKey, "post", StringComparison.OrdinalIgnoreCase) ? "blog" : collectionKey;
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    [collectionKey] = new()
                    {
                        Permalink = $"/{routePrefix}/{{slug}}/",
                        ListRoute = $"/{routePrefix}/",
                        Output = new CollectionOutputConfig { Archive = true }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
        var context = new BuildContext
        {
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = routed.ToRoutedDocuments(),
            TemplateResolver = kind => kind.Equals("archive", StringComparison.OrdinalIgnoreCase)
                ? "pages/archive.html"
                : throw new ConfigException($"Unexpected template kind: {kind}"),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        return (context, config);
    }

    [Fact]
    public void DerivePages_CreatesYearAndMonthArchivePages()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
            (CreateItem("p2", "Post 2", "post-2", new DateTimeOffset(2024, 3, 20, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-2/", "blog/post-2/index.html", "pages/post.html")),
            (CreateItem("p3", "Post 3", "post-3", new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-3/", "blog/post-3/index.html", "pages/post.html")),
        };
        var (ctx, config) = CreateContext(routed);

        var plugin = new ArchivePlugin(config);
        var derived = plugin.DerivePages(ctx);

        Assert.NotNull(derived);
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/03/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/06/");
    }

    [Fact]
    public void DerivePages_DistinctTypeAndCollectionUsesCollectionKey()
    {
        var news = ContentDocument.Create(
            "news-1",
            "News",
            "news-1",
            new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero),
            string.Empty,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "article",
                ["collection"] = "news"
            }));
        var (context, config) = CreateContext(
        [
            (news, new RouteInfo("/news/news-1/", "news/news-1/index.html", "news.html"))
        ], "news");

        var derived = new ArchivePlugin(config).DerivePages(context);

        Assert.Contains(derived, item => item.Route.Url == "/news/archive/");
        Assert.All(derived, item => Assert.Equal("news", ContentFieldReader.GetCollection(item.Document)));
    }

    [Fact]
    public void DerivePages_ItemWithoutPublishDate_IsIgnored()
    {
        var itemNoDate = ContentDocument.Create(
            id: "p0",
            title: "No Date",
            slug: "no-date",
            publishAt: DateTimeOffset.MinValue,
            contentHtml: "<p>no date</p>");
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (itemNoDate, new RouteInfo("/blog/no-date/", "blog/no-date/index.html", "pages/post.html")),
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };
        var (ctx, config) = CreateContext(routed);

        var plugin = new ArchivePlugin(config);
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/01/");
    }

    [Fact]
    public void DerivePages_EmptyContent_ReturnsEmptyList()
    {
        var (ctx, config) = CreateContext(new List<(ContentDocument Item, RouteInfo Route)>());

        var plugin = new ArchivePlugin(config);
        var derived = plugin.DerivePages(ctx);

        Assert.NotNull(derived);
        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_ArchivePageTitles_ContainYearAndMonth()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2024, 5, 10, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };
        var (ctx, config) = CreateContext(routed);

        var plugin = new ArchivePlugin(config);
        var derived = plugin.DerivePages(ctx);

        var yearPage = Assert.Single(derived, x => x.Route.Url == "/blog/archive/2024/");
        Assert.Equal("Archive: 2024", yearPage.Document.Title);

        var monthPage = Assert.Single(derived, x => x.Route.Url == "/blog/archive/2024/05/");
        Assert.Equal("Archive: 2024-05", monthPage.Document.Title);
    }

    [Fact]
    public void DerivePages_MultipleYears_CreatesYearPagesForEach()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
            (CreateItem("p2", "Post 2", "post-2", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-2/", "blog/post-2/index.html", "pages/post.html")),
        };
        var (ctx, config) = CreateContext(routed);

        var plugin = new ArchivePlugin(config);
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2023/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2023/12/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/01/");
    }

    [Fact]
    public void DerivePages_SingleItem_InBothYearAndMonth()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2025, 4, 22, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };
        var (ctx, config) = CreateContext(routed);

        var plugin = new ArchivePlugin(config);
        var derived = plugin.DerivePages(ctx);

        Assert.Equal(3, derived.Count);
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2025/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2025/04/");
    }

    [Fact]
    public void DerivePages_OutputPathEncodingUrlEncode_AppliesToArchiveOutputPath()
    {
        var item = CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2025, 4, 22, 0, 0, 0, TimeSpan.Zero), collection: "post");
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (item, new RouteInfo("/Blog Posts/post-1/", "Blog Posts/post-1/index.html", "pages/post.html"))
        };
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "test",
                OutputPathEncoding = "urlencode",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new()
                    {
                        Permalink = "/Blog Posts/{slug}/",
                        Template = "pages/post.html",
                        ListRoute = "/Blog Posts/",
                        Output = new CollectionOutputConfig { Archive = true }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
        var ctx = new BuildContext
        {
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = routed.ToRoutedDocuments(),
            TemplateResolver = kind => kind.Equals("archive", StringComparison.OrdinalIgnoreCase)
                ? "pages/archive.html"
                : throw new ConfigException($"Unexpected template kind: {kind}"),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = new ArchivePlugin(config).DerivePages(ctx);

        var index = Assert.Single(derived, x => x.Route.Url == "/Blog Posts/archive/");
        Assert.Equal("Blog%20Posts/archive/index.html", index.Route.OutputPath);
    }
}
