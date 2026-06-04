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

public sealed class PaginationPluginDerivePagesTests
{
    private static BuildContext CreateContext(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        int pageSize = 10,
        string collectionKey = "post",
        string listRoute = "/blog/",
        string outputPathEncoding = "none")
    {
        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "test",
                    OutputPathEncoding = outputPathEncoding,
                    Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        [collectionKey] = new CollectionConfig
                        {
                            Permalink = $"/{collectionKey}/{{slug}}/",
                            Template = $"pages/{collectionKey}.html",
                            ListRoute = listRoute,
                            Pagination = new CollectionPaginationConfig
                            {
                                Enabled = true,
                                PageSize = pageSize
                            }
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
            TemplateResolver = kind => kind.Equals("pagination", StringComparison.OrdinalIgnoreCase)
                ? "pages/pagination.html"
                : throw new ConfigException($"Unexpected template kind: {kind}"),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }

    private static (ContentItem Item, RouteInfo Route) CreateRoutedItem(int index, DateTimeOffset? publishAt = null)
    {
        var publish = publishAt ?? new DateTimeOffset(2024, 1, (index % 28) + 1, 0, 0, 0, TimeSpan.Zero);
        var item = new ContentItem(
            Id: $"post-{index}",
            Title: $"Post {index}",
            Slug: $"post-{index}",
            PublishAt: publish,
            ContentHtml: $"<p>content {index}</p>",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post"
            },
            Fields: null);
        var route = new RouteInfo($"/blog/post-{index}/", $"blog/post-{index}/index.html", "pages/post.html");
        return (item, route);
    }

    [Fact]
    public void DerivePages_SinglePageOfResults_ReturnsEmpty()
    {
        var routed = Enumerable.Range(0, 5)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 10);

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.NotNull(derived);
        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_ExactlyPageSize_ReturnsEmpty()
    {
        var routed = Enumerable.Range(0, 10)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 10);

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_MoreThanPageSize_GeneratesMultiplePages()
    {
        var routed = Enumerable.Range(0, 25)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 10);

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Equal(2, derived.Count);
    }

    [Fact]
    public void DerivePages_CorrectPageUrls()
    {
        var routed = Enumerable.Range(0, 25)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 10, listRoute: "/blog/");

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/blog/page/2/");
        Assert.Contains(derived, x => x.Route.Url == "/blog/page/3/");
    }

    [Fact]
    public void DerivePages_CustomPageSize_GeneratesCorrectPageCount()
    {
        var routed = Enumerable.Range(0, 30)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 5);

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Equal(5, derived.Count);
    }

    [Fact]
    public void DerivePages_EmptyInput_ReturnsEmpty()
    {
        var ctx = CreateContext(new List<(ContentItem Item, RouteInfo Route)>(), pageSize: 10);

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_CustomListRoute_GeneratesCorrectUrls()
    {
        var routed = Enumerable.Range(0, 12)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 5, listRoute: "/posts/", collectionKey: "post");

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/posts/page/2/");
        Assert.Contains(derived, x => x.Route.Url == "/posts/page/3/");
    }

    [Fact]
    public void DerivePages_OutputPathEncodingSlug_AppliesToDerivedOutputPath()
    {
        var routed = Enumerable.Range(0, 12)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 5, listRoute: "/Blog Posts/", outputPathEncoding: "slug");

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        var page2 = Assert.Single(derived, x => x.Route.Url == "/Blog Posts/page/2/");
        Assert.Equal("blog-posts/page/2/index.html", page2.Route.OutputPath);
    }

    [Fact]
    public void DerivePages_PageItems_PreserveOrder()
    {
        var routed = Enumerable.Range(0, 22)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 10);

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        var page2 = Assert.Single(derived, x => x.Route.Url == "/blog/page/2/");
        Assert.NotNull(page2.Item.Fields);
        Assert.True(page2.Item.Fields!.ContainsKey("items"));
        var itemsField = page2.Item.Fields["items"];
        Assert.Equal("list", itemsField.Type);
    }

    [Fact]
    public void DerivePages_NoPaginationCollection_ReturnsEmpty()
    {
        var routed = Enumerable.Range(0, 5)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "test",
                    Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["post"] = new CollectionConfig
                        {
                            Permalink = "/blog/{slug}/",
                            Template = "pages/post.html",
                            ListRoute = "/blog/",
                            Pagination = new CollectionPaginationConfig { Enabled = false, PageSize = 10 }
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
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_SeveralPages_GeneratesCorrectTotalPages()
    {
        var routed = Enumerable.Range(0, 47)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 10);

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Equal(4, derived.Count);
        Assert.Contains(derived, x => x.Route.Url == "/blog/page/5/");
    }
}
