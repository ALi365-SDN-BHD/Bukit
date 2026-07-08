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
        IReadOnlyList<(ContentDocument Item, RouteInfo Route)> routed,
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
                Content = TestContent.Markdown()
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = routed.ToRoutedDocuments(),
            TemplateResolver = kind => kind.Equals("pagination", StringComparison.OrdinalIgnoreCase)
                ? "pages/pagination.html"
                : throw new ConfigException($"Unexpected template kind: {kind}"),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }

    private static (ContentDocument Item, RouteInfo Route) CreateRoutedItem(int index, DateTimeOffset? publishAt = null)
    {
        var publish = publishAt ?? new DateTimeOffset(2024, 1, (index % 28) + 1, 0, 0, 0, TimeSpan.Zero);
        var item = ContentDocument.Create(
            id: $"post-{index}",
            title: $"Post {index}",
            slug: $"post-{index}",
            publishAt: publish,
            contentHtml: $"<p>content {index}</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "post"
            }));
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
        var ctx = CreateContext(new List<(ContentDocument Item, RouteInfo Route)>(), pageSize: 10);

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
        Assert.NotNull(page2.Document.CustomFields);
        Assert.True(page2.Document.CustomFields!.ContainsKey("items"));
        var itemsField = page2.Document.CustomFields["items"];
        Assert.Equal("list", itemsField.Type);
    }

    [Fact]
    public void DerivePages_UsesStructuredCollectionAndSummary()
    {
        var publish = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var routed = Enumerable.Range(0, 12)
            .Select(i =>
            {
                var item = ContentDocument.Create(
                    id: $"post-{i}",
                    title: $"Post {i}",
                    slug: $"post-{i}",
                    publishAt: publish.AddDays(i),
                    contentHtml: $"<p>content {i}</p>",
                    fields: ContentFieldReader.WithValues(new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["collection"] = new("text", "post"),
                        ["summary"] = new("text", $"Canonical summary {i}")
                    }, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = "post"
                    }));
                var route = new RouteInfo($"/blog/post-{i}/", $"blog/post-{i}/index.html", "pages/post.html");
                return (item, route);
            })
            .ToList();
        var ctx = CreateContext(routed, pageSize: 5);

        var derived = new PaginationPlugin().DerivePages(ctx);

        var page2 = Assert.Single(derived, x => x.Route.Url == "/blog/page/2/");
        var items = Assert.IsType<List<object>>(page2.Document.CustomFields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(items[0]);
        Assert.Equal("Canonical summary 6", first["summary"]);
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
                Content = TestContent.Markdown()
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = routed.ToRoutedDocuments(),
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

    [Fact]
    public void DerivePages_WhenListRouteGraphOwnsPagination_ReturnsEmpty()
    {
        var routed = Enumerable.Range(0, 12)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 5);
        ctx.Data[ListRouteGraphBuilder.BuildContextDataKey] = ListRouteGraph.Create(new[]
        {
            new ListRoutePlan
            {
                RouteId = "collection:post:2",
                Kind = ListRouteKind.CollectionPage,
                Url = "/blog/page/2/",
                OutputPath = "blog/page/2/index.html",
                Template = "pages/list.html",
                Collection = "post",
                PageNumber = 2,
                PageSize = 5,
                TotalItems = 12,
                CanonicalUrl = "/blog/page/2/"
            }
        });

        var derived = new PaginationPlugin().DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void GetTemplateRequirementKinds_WhenListRouteGraphOwnsPagination_ReturnsEmpty()
    {
        var routed = Enumerable.Range(0, 12)
            .Select(i => CreateRoutedItem(i))
            .ToList();
        var ctx = CreateContext(routed, pageSize: 5);
        ctx.Data[ListRouteGraphBuilder.BuildContextDataKey] = ListRouteGraph.Create(new[]
        {
            new ListRoutePlan
            {
                RouteId = "collection:post:2",
                Kind = ListRouteKind.CollectionPage,
                Url = "/blog/page/2/",
                OutputPath = "blog/page/2/index.html",
                Template = "pages/list.html",
                Collection = "post",
                PageNumber = 2,
                PageSize = 5,
                TotalItems = 12,
                CanonicalUrl = "/blog/page/2/"
            }
        });

        var requirements = new PaginationPlugin().GetTemplateRequirementKinds(ctx);

        Assert.Empty(requirements);
    }
}
