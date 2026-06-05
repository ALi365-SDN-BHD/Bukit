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

public sealed class RelatedContentPluginTests
{
    private static ContentItem CreateItem(string id, string title, string slug, string? tags = null, string? categories = null, string? collection = null)
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (tags is not null) meta["tags"] = tags;
        if (categories is not null) meta["categories"] = categories;
        if (collection is not null) meta["collection"] = collection;

        return new ContentItem(
            Id: id,
            Title: title,
            Slug: slug,
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>content</p>",
            Meta: meta,
            Fields: null);
    }

    private static ContentItem CreateFieldItem(string id, string title, string slug, IReadOnlyList<string> tags)
    {
        return new ContentItem(
            Id: id,
            Title: title,
            Slug: slug,
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>content</p>",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["tags"] = new("list", tags)
            });
    }

    private static RouteInfo Route(string url) => new(url, $"out{url}index.html", "pages/post.html");

    [Fact]
    public void DerivePages_NotEnabled_ReturnsEmpty()
    {
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t", Related = new RelatedConfig { Enabled = false } },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            Routed = new List<(ContentItem, RouteInfo)> { (CreateItem("1", "A", "a", tags: "go"), Route("/a/")) },
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = new RelatedContentPlugin().DerivePages(ctx);
        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_SharedTags_CreatesRelatedLinks()
    {
        var routed = new List<(ContentItem, RouteInfo)>
        {
            (CreateItem("1", "Go Post", "go-post", tags: "go,runtime"), Route("/go-post/")),
            (CreateItem("2", "Rust Post", "rust-post", tags: "rust,cargo"), Route("/rust-post/")),
            (CreateItem("3", "Go Tips", "go-tips", tags: "go,tips"), Route("/go-tips/")),
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Related = new RelatedConfig
                    {
                        Enabled = true,
                        Threshold = 80,
                        Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } }
                    }
                },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            Routed = routed,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin().DerivePages(ctx);

        Assert.True(ctx.Data.TryGetValue("__related_pages", out var val));
        var dict = Assert.IsType<Dictionary<string, List<object>>>(val);
        Assert.Contains("1", dict);
        Assert.Contains("3", dict);
    }

    [Fact]
    public void DerivePages_ShouldCreateRelatedLinks_WhenTagsExistOnlyInStructuredFields()
    {
        var routed = new List<(ContentItem, RouteInfo)>
        {
            (CreateFieldItem("1", "Go Post", "go-post", new[] { "go", "runtime" }), Route("/go-post/")),
            (CreateFieldItem("2", "Rust Post", "rust-post", new[] { "rust", "cargo" }), Route("/rust-post/")),
            (CreateFieldItem("3", "Go Tips", "go-tips", new[] { "go", "tips" }), Route("/go-tips/")),
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Related = new RelatedConfig
                    {
                        Enabled = true,
                        Threshold = 80,
                        Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } }
                    }
                },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            Routed = routed,
            ContentGraph = CanonicalContentGraphBuilder.Build(routed.Select(x => x.Item1).ToList()),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin().DerivePages(ctx);

        Assert.True(ctx.Data.TryGetValue("__related_pages", out var val));
        var dict = Assert.IsType<Dictionary<string, List<object>>>(val);
        Assert.Contains("1", dict);
        Assert.Contains("3", dict);
    }

    [Fact]
    public void DerivePages_NoSharedTags_NoRelatedData()
    {
        var routed = new List<(ContentItem, RouteInfo)>
        {
            (CreateItem("1", "A", "a", tags: "go"), Route("/a/")),
            (CreateItem("2", "B", "b", tags: "rust"), Route("/b/")),
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Related = new RelatedConfig { Enabled = true, Threshold = 50, Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } } }
                },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            Routed = routed,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin().DerivePages(ctx);

        Assert.False(ctx.Data.TryGetValue("__related_pages", out var val)
            && val is Dictionary<string, List<object>> d && d.Count > 0);
    }

    [Fact]
    public void DerivePages_SingleItem_NoRelated()
    {
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t", Related = new RelatedConfig { Enabled = true } },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            Routed = new List<(ContentItem, RouteInfo)> { (CreateItem("1", "A", "a"), Route("/a/")) },
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = new RelatedContentPlugin().DerivePages(ctx);
        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_SkipsArchiveAndPaginationItems()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var archiveItem = new ContentItem("blog-archive-2024", "Archive", "a", DateTimeOffset.UtcNow, "<p>x</p>", meta);
        var pageItem = new ContentItem("blog-page-2", "Page 2", "p2", DateTimeOffset.UtcNow, "<p>x</p>", meta);
        var normalItem = CreateItem("1", "Normal", "n", tags: "tag1");
        var routed = new List<(ContentItem, RouteInfo)>
        {
            (archiveItem, Route("/archive/")),
            (pageItem, Route("/page/2/")),
            (normalItem, Route("/normal/")),
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t", Related = new RelatedConfig { Enabled = true } },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            Routed = routed,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin().DerivePages(ctx);

        Assert.False(ctx.Data.TryGetValue("__related_pages", out var d)
            && d is Dictionary<string, List<object>> dict
            && (dict.ContainsKey("blog-archive-2024") || dict.ContainsKey("blog-page-2")));
    }

    [Fact]
    public void DerivePages_CategoriesIndex_MatchesByCategory()
    {
        var routed = new List<(ContentItem, RouteInfo)>
        {
            (CreateItem("1", "A", "a", categories: "tech"), Route("/a/")),
            (CreateItem("2", "B", "b", categories: "life"), Route("/b/")),
            (CreateItem("3", "C", "c", categories: "tech"), Route("/c/")),
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Related = new RelatedConfig { Enabled = true, Threshold = 60, Indices = new[] { new RelatedIndexConfig { Name = "categories", Weight = 100 } } }
                },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            Routed = routed,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin().DerivePages(ctx);

        Assert.True(ctx.Data.TryGetValue("__related_pages", out var val));
        var dict = Assert.IsType<Dictionary<string, List<object>>>(val);
        Assert.Contains("1", dict);
        Assert.Contains("3", dict);
    }

    [Fact]
    public void DerivePages_HighThreshold_ReducesMatches()
    {
        var routed = new List<(ContentItem, RouteInfo)>
        {
            (CreateItem("1", "A", "a", tags: "a"), Route("/a/")),
            (CreateItem("2", "B", "b", tags: "a"), Route("/b/")),
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Related = new RelatedConfig { Enabled = true, Threshold = 999, Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } } }
                },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            Routed = routed,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin().DerivePages(ctx);

        Assert.False(ctx.Data.TryGetValue("__related_pages", out var val)
            && val is Dictionary<string, List<object>> d && d.Values.Any(v => v.Count > 0));
    }
}
