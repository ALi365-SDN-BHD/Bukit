using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyPluginDerivePagesTests
{
    private static BuildContext CreateContext(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        TaxonomyConfig? taxonomyConfig = null,
        string outputMode = "pages",
        string outputPathEncoding = "none")
    {
        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test", OutputPathEncoding = outputPathEncoding },
                Content = new ContentConfig { Provider = "markdown" },
                Taxonomy = taxonomyConfig ?? new TaxonomyConfig
                {
                    OutputMode = outputMode,
                    IndexEnabled = true
                }
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = routed,
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }

    private static (ContentItem Item, RouteInfo Route) CreateItem(
        string id,
        string title,
        DateTimeOffset publishAt,
        string[]? tags = null,
        string[]? categories = null,
        bool? pinned = null)
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (tags is { Length: > 0 })
        {
            meta["tags"] = tags;
        }

        if (categories is { Length: > 0 })
        {
            meta["categories"] = categories;
        }

        if (pinned.HasValue)
        {
            meta["pinned"] = pinned.Value;
        }

        var item = new ContentItem(
            Id: id,
            Title: title,
            Slug: id,
            PublishAt: publishAt,
            ContentHtml: $"<p>{title}</p>",
            Meta: meta,
            Fields: null);
        var route = new RouteInfo($"/blog/{id}/", $"blog/{id}/index.html", "pages/post.html");
        return (item, route);
    }

    [Fact]
    public void DerivePages_GeneratesTermPagesForTags()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha", "beta" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/beta/");
    }

    [Fact]
    public void DerivePages_GeneratesTermPagesForCategories()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: new[] { "News", "Tech" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/categories/news/");
        Assert.Contains(derived, x => x.Route.Url == "/categories/tech/");
    }

    [Fact]
    public void DerivePages_GeneratesIndexPages()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
    }

    [Fact]
    public void DerivePages_OutputPathEncodingSanitize_AppliesToTermOutputPath()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "hello world" }),
        };
        var ctx = CreateContext(routed, outputPathEncoding: "sanitize");

        var derived = new TaxonomyPlugin().DerivePages(ctx);

        var term = Assert.Single(derived, x => x.Route.Url == "/tags/hello-world/");
        Assert.Equal("tags/hello-world/index.html", term.Route.OutputPath);
    }

    [Fact]
    public void DerivePages_PinnedItemsSortedFirst()
    {
        var unpinned = CreateItem("p1", "Not Pinned", new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "topic" }, pinned: false);
        var pinned = CreateItem("p2", "Pinned", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "topic" }, pinned: true);
        var routed = new List<(ContentItem Item, RouteInfo Route)> { unpinned, pinned };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        var termPage = Assert.Single(derived, x => x.Route.Url == "/tags/topic/");
        Assert.NotNull(termPage.Item.Fields);
        Assert.True(termPage.Item.Fields!.ContainsKey("items"));
        var items = Assert.IsType<List<object>>(termPage.Item.Fields["items"].Value);
        Assert.Equal(2, items.Count);
        var first = Assert.IsType<Dictionary<string, object>>(items[0]);
        Assert.Equal("Pinned", first["title"]);
    }

    [Fact]
    public void DerivePages_MultipleTaxonomyKinds()
    {
        var config = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = true,
            Kinds = new List<TaxonomyKindConfig>
            {
                new() { Key = "series", Kind = "series", Title = "Series", SingularTitlePrefix = "Series" },
                new() { Key = "authors", Kind = "authors", Title = "Authors", SingularTitlePrefix = "Author" }
            }
        };
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var meta = routed[0].Item.Meta as Dictionary<string, object>;
        meta!["series"] = new[] { "My Series" };
        meta["authors"] = new[] { "Alice" };
        var ctx = CreateContext(routed, taxonomyConfig: config);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/series/");
        Assert.Contains(derived, x => x.Route.Url == "/series/my-series/");
        Assert.Contains(derived, x => x.Route.Url == "/authors/");
        Assert.Contains(derived, x => x.Route.Url == "/authors/alice/");
        Assert.DoesNotContain(derived, x => x.Route.Url.StartsWith("/tags/"));
    }

    [Fact]
    public void DerivePages_EmptyItems_ReturnsEmpty()
    {
        var ctx = CreateContext(new List<(ContentItem Item, RouteInfo Route)>());

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_ItemsWithoutTaxonomy_ReturnsEmpty()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_ItemsWithCategoriesOnly_NoTagsPages()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: new[] { "News" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.DoesNotContain(derived, x => x.Route.Url.StartsWith("/tags/"));
        Assert.Contains(derived, x => x.Route.Url == "/categories/");
        Assert.Contains(derived, x => x.Route.Url == "/categories/news/");
    }

    [Fact]
    public void DerivePages_IndexEnabledFalse_NoIndexPages()
    {
        var taxonomyConfig = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = false
        };
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = CreateContext(routed, taxonomyConfig: taxonomyConfig);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.DoesNotContain(derived, x => x.Route.Url == "/tags/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
    }

    [Fact]
    public void DerivePages_TermPageTitleContainsPrefix()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "AlphaTag" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        var termPage = Assert.Single(derived, x => x.Route.Url == "/tags/alphatag/");
        Assert.Equal("Tag: AlphaTag", termPage.Item.Title);
    }

    [Fact]
    public void DerivePages_CommaSeparatedTags_AreParsedCorrectly()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = "alpha, beta, gamma"
        };
        var item = new ContentItem(
            Id: "p1",
            Title: "Post 1",
            Slug: "post-1",
            PublishAt: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>content</p>",
            Meta: meta,
            Fields: null);
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            (item, new RouteInfo("/blog/p1/", "blog/p1/index.html", "pages/post.html")),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/beta/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/gamma/");
    }

    [Fact]
    public void DerivePages_WithBaseUrl_PrefixIsApplied()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" },
                Taxonomy = new TaxonomyConfig { OutputMode = "pages", IndexEnabled = true }
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/my-site",
            LayoutsDir = "/test/layouts",
            Routed = routed,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
    }

    [Fact]
    public void DerivePages_OutputModeData_ReturnsEmpty()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = CreateContext(routed, outputMode: "data");

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }
}
