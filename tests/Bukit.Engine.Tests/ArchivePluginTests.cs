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
    private static ContentItem CreateItem(string id, string title, string slug, DateTimeOffset publishAt, string? collection = null)
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        meta["type"] = collection ?? "post";
        meta["collection"] = collection ?? "post";

        return new ContentItem(
            Id: id,
            Title: title,
            Slug: slug,
            PublishAt: publishAt,
            ContentHtml: "<p>content</p>",
            Fields: ContentFieldReader.ToFieldMap(meta));
    }

    private static BuildContext CreateContext(List<(ContentItem Item, RouteInfo Route)> routed)
    {
        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "test",
                    Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["post"] = new()
                        {
                            Permalink = "/blog/{slug}/",
                            ListRoute = "/blog/",
                            Output = new CollectionOutputConfig { Archive = true }
                        }
                    }
                },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = routed,
            TemplateResolver = kind => kind.Equals("archive", StringComparison.OrdinalIgnoreCase)
                ? "pages/archive.html"
                : throw new ConfigException($"Unexpected template kind: {kind}"),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }

    [Fact]
    public void DerivePages_CreatesYearAndMonthArchivePages()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
            (CreateItem("p2", "Post 2", "post-2", new DateTimeOffset(2024, 3, 20, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-2/", "blog/post-2/index.html", "pages/post.html")),
            (CreateItem("p3", "Post 3", "post-3", new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-3/", "blog/post-3/index.html", "pages/post.html")),
        };
        var ctx = CreateContext(routed);

        var plugin = new ArchivePlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.NotNull(derived);
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/03/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/06/");
    }

    [Fact]
    public void DerivePages_ItemWithoutPublishDate_IsIgnored()
    {
        var itemNoDate = new ContentItem(
            Id: "p0",
            Title: "No Date",
            Slug: "no-date",
            PublishAt: DateTimeOffset.MinValue,
            ContentHtml: "<p>no date</p>");
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            (itemNoDate, new RouteInfo("/blog/no-date/", "blog/no-date/index.html", "pages/post.html")),
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };
        var ctx = CreateContext(routed);

        var plugin = new ArchivePlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/01/");
    }

    [Fact]
    public void DerivePages_EmptyContent_ReturnsEmptyList()
    {
        var ctx = CreateContext(new List<(ContentItem Item, RouteInfo Route)>());

        var plugin = new ArchivePlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.NotNull(derived);
        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_ArchivePageTitles_ContainYearAndMonth()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2024, 5, 10, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };
        var ctx = CreateContext(routed);

        var plugin = new ArchivePlugin();
        var derived = plugin.DerivePages(ctx);

        var yearPage = Assert.Single(derived, x => x.Route.Url == "/blog/archive/2024/");
        Assert.Equal("Archive: 2024", yearPage.Item.Title);

        var monthPage = Assert.Single(derived, x => x.Route.Url == "/blog/archive/2024/05/");
        Assert.Equal("Archive: 2024-05", monthPage.Item.Title);
    }

    [Fact]
    public void DerivePages_MultipleYears_CreatesYearPagesForEach()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
            (CreateItem("p2", "Post 2", "post-2", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-2/", "blog/post-2/index.html", "pages/post.html")),
        };
        var ctx = CreateContext(routed);

        var plugin = new ArchivePlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2023/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2023/12/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2024/01/");
    }

    [Fact]
    public void DerivePages_SingleItem_InBothYearAndMonth()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            (CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2025, 4, 22, 0, 0, 0, TimeSpan.Zero)), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };
        var ctx = CreateContext(routed);

        var plugin = new ArchivePlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Equal(3, derived.Count);
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2025/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/archive/2025/04/");
    }

    [Fact]
    public void DerivePages_OutputPathEncodingUrlEncode_AppliesToArchiveOutputPath()
    {
        var item = CreateItem("p1", "Post 1", "post-1", new DateTimeOffset(2025, 4, 22, 0, 0, 0, TimeSpan.Zero), collection: "post");
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            (item, new RouteInfo("/Blog Posts/post-1/", "Blog Posts/post-1/index.html", "pages/post.html"))
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
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
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = routed,
            TemplateResolver = kind => kind.Equals("archive", StringComparison.OrdinalIgnoreCase)
                ? "pages/archive.html"
                : throw new ConfigException($"Unexpected template kind: {kind}"),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = new ArchivePlugin().DerivePages(ctx);

        var index = Assert.Single(derived, x => x.Route.Url == "/Blog Posts/archive/");
        Assert.Equal("Blog%20Posts/archive/index.html", index.Route.OutputPath);
    }
}
