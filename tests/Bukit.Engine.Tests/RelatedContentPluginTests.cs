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
    private static ContentDocument CreateItem(string id, string title, string slug, string? tags = null, string? categories = null, string? collection = null)
    {
        var fieldValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (tags is not null) fieldValues["tags"] = tags;
        if (categories is not null) fieldValues["categories"] = categories;
        if (collection is not null) fieldValues["collection"] = collection;

        return ContentDocument.Create(
            id: id,
            title: title,
            slug: slug,
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>content</p>",
            fields: ContentFieldReader.ToFieldMap(fieldValues));
    }

    private static ContentDocument CreateFieldItem(string id, string title, string slug, IReadOnlyList<string> tags)
    {
        return ContentDocument.Create(
            id: id,
            title: title,
            slug: slug,
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>content</p>",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["tags"] = new("list", tags)
            });
    }

    private static RouteInfo Route(string url) => new(url, $"out{url}index.html", "pages/post.html");

    [Fact]
    public void DerivePages_NotEnabled_DoesNotWriteRelatedData()
    {
        var config = CreateConfig(new RelatedConfig
        {
            Enabled = false,
            Threshold = 50,
            Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } }
        });
        var ctx = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = new List<(ContentDocument, RouteInfo)>
            {
                (CreateItem("1", "A", "a", tags: "go", collection: "post"), Route("/a/")),
                (CreateItem("2", "B", "b", tags: "go", collection: "post"), Route("/b/"))
            }.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = new RelatedContentPlugin(config).DerivePages(ctx);
        Assert.Empty(derived);
        Assert.False(ctx.TryGet(BuildDataKeys.RelatedPages, out _));
    }

    [Fact]
    public void DerivePages_SharedTags_CreatesRelatedLinks()
    {
        var routed = new List<(ContentDocument, RouteInfo)>
        {
            (CreateItem("1", "Go Post", "go-post", tags: "go,runtime"), Route("/go-post/")),
            (CreateItem("2", "Rust Post", "rust-post", tags: "rust,cargo"), Route("/rust-post/")),
            (CreateItem("3", "Go Tips", "go-tips", tags: "go,tips"), Route("/go-tips/")),
        };
        var config = CreateConfig(new RelatedConfig
        {
            Enabled = true,
            Threshold = 80,
            Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } }
        });
        var ctx = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = routed.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin(config).DerivePages(ctx);

        Assert.True(ctx.TryGet(BuildDataKeys.RelatedPages, out var val));
        var dict = Assert.IsType<Dictionary<string, List<object>>>(val);
        Assert.Contains("1", dict);
        Assert.Contains("3", dict);
    }

    [Fact]
    public void DerivePages_ShouldCreateRelatedLinks_WhenTagsExistOnlyInStructuredFields()
    {
        var routed = new List<(ContentDocument, RouteInfo)>
        {
            (CreateFieldItem("1", "Go Post", "go-post", new[] { "go", "runtime" }), Route("/go-post/")),
            (CreateFieldItem("2", "Rust Post", "rust-post", new[] { "rust", "cargo" }), Route("/rust-post/")),
            (CreateFieldItem("3", "Go Tips", "go-tips", new[] { "go", "tips" }), Route("/go-tips/")),
        };
        var config = CreateConfig(new RelatedConfig
        {
            Enabled = true,
            Threshold = 80,
            Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } }
        });
        var ctx = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = routed.ToRoutedDocuments(),
            ContentGraph = CanonicalContentGraphBuilder.BuildFromDocuments(routed.ToRoutedDocuments().Select(x => x.Document).ToList()),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin(config).DerivePages(ctx);

        Assert.True(ctx.TryGet(BuildDataKeys.RelatedPages, out var val));
        var dict = Assert.IsType<Dictionary<string, List<object>>>(val);
        Assert.Contains("1", dict);
        Assert.Contains("3", dict);
    }

    [Fact]
    public void DerivePages_NoSharedTags_NoRelatedData()
    {
        var routed = new List<(ContentDocument, RouteInfo)>
        {
            (CreateItem("1", "A", "a", tags: "go"), Route("/a/")),
            (CreateItem("2", "B", "b", tags: "rust"), Route("/b/")),
        };
        var config = CreateConfig(new RelatedConfig
        {
            Enabled = true,
            Threshold = 50,
            Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } }
        });
        var ctx = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = routed.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin(config).DerivePages(ctx);

        Assert.False(ctx.TryGet(BuildDataKeys.RelatedPages, out var val)
            && val is Dictionary<string, List<object>> d && d.Count > 0);
    }

    [Fact]
    public void DerivePages_SingleItem_NoRelated()
    {
        var config = CreateConfig(new RelatedConfig
        {
            Enabled = true,
            Threshold = 50,
            Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } }
        });
        var ctx = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = new List<(ContentDocument, RouteInfo)> { (CreateItem("1", "A", "a", tags: "go", collection: "post"), Route("/a/")) }.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = new RelatedContentPlugin(config).DerivePages(ctx);
        Assert.Empty(derived);
        Assert.False(ctx.TryGet(BuildDataKeys.RelatedPages, out _));
    }

    [Fact]
    public void DerivePages_SkipsArchiveAndPaginationItems()
    {
        var fieldValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var archiveItem = ContentDocument.Create("blog-archive-2024", "Archive", "a", DateTimeOffset.UtcNow, "<p>x</p>", ContentFieldReader.ToFieldMap(fieldValues));
        var pageItem = ContentDocument.Create("blog-page-2", "Page 2", "p2", DateTimeOffset.UtcNow, "<p>x</p>", ContentFieldReader.ToFieldMap(fieldValues));
        var normalItem = CreateItem("1", "Normal", "n", tags: "tag1");
        var routed = new List<(ContentDocument, RouteInfo)>
        {
            (archiveItem, Route("/archive/")),
            (pageItem, Route("/page/2/")),
            (normalItem, Route("/normal/")),
        };
        var config = CreateConfig(new RelatedConfig { Enabled = true });
        var ctx = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = routed.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin(config).DerivePages(ctx);

        Assert.False(ctx.TryGet(BuildDataKeys.RelatedPages, out var d)
            && d is Dictionary<string, List<object>> dict
            && (dict.ContainsKey("blog-archive-2024") || dict.ContainsKey("blog-page-2")));
    }

    [Fact]
    public void DerivePages_CategoriesIndex_MatchesByCategory()
    {
        var routed = new List<(ContentDocument, RouteInfo)>
        {
            (CreateItem("1", "A", "a", categories: "tech"), Route("/a/")),
            (CreateItem("2", "B", "b", categories: "life"), Route("/b/")),
            (CreateItem("3", "C", "c", categories: "tech"), Route("/c/")),
        };
        var config = CreateConfig(new RelatedConfig
        {
            Enabled = true,
            Threshold = 60,
            Indices = new[] { new RelatedIndexConfig { Name = "categories", Weight = 100 } }
        });
        var ctx = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = routed.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin(config).DerivePages(ctx);

        Assert.True(ctx.TryGet(BuildDataKeys.RelatedPages, out var val));
        var dict = Assert.IsType<Dictionary<string, List<object>>>(val);
        Assert.Contains("1", dict);
        Assert.Contains("3", dict);
    }

    [Fact]
    public void DerivePages_HighThreshold_ReducesMatches()
    {
        var routed = new List<(ContentDocument, RouteInfo)>
        {
            (CreateItem("1", "A", "a", tags: "a"), Route("/a/")),
            (CreateItem("2", "B", "b", tags: "a"), Route("/b/")),
        };
        var config = CreateConfig(new RelatedConfig
        {
            Enabled = true,
            Threshold = 999,
            Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } }
        });
        var ctx = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = routed.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin(config).DerivePages(ctx);

        Assert.False(ctx.TryGet(BuildDataKeys.RelatedPages, out var val)
            && val is Dictionary<string, List<object>> d && d.Values.Any(v => v.Count > 0));
    }

    [Fact]
    public void DerivePages_CollectionRules_ScoreFieldsWithinTheCollectionAndKeepListOrderOnTies()
    {
        static ContentDocument Route(string id, int day, string[] cities, string[] audiences) => ContentDocument.Create(
            id, id, id, DateTimeOffset.UnixEpoch.AddDays(day), "<p></p>",
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = new("text", "route"),
                ["cities"] = new("list", cities),
                ["audiences"] = new("list", audiences)
            });
        var routed = new List<(ContentDocument, RouteInfo)>
        {
            (Route("r1", 1, ["kl", "penang"], ["family"]), RelatedContentPluginTests.Route("/r1/")),
            (Route("r2", 2, ["kl"], ["couple"]), RelatedContentPluginTests.Route("/r2/")),
            (Route("r3", 3, ["kl"], ["family"]), RelatedContentPluginTests.Route("/r3/")),
            (Route("r4", 4, ["ipoh"], ["solo"]), RelatedContentPluginTests.Route("/r4/")),
            (Route("r5", 5, ["kl"], ["couple"]), RelatedContentPluginTests.Route("/r5/")),
            (CreateItem("p1", "Post", "p1", tags: "kl", collection: "post"), RelatedContentPluginTests.Route("/p1/"))
        };
        var config = CreateConfig(new RelatedConfig
        {
            Enabled = true,
            Indices = new[] { new RelatedIndexConfig { Name = "tags", Weight = 100 } },
            Collections =
            [
                new RelatedCollectionConfig
                {
                    Collection = "route",
                    Threshold = 0,
                    Limit = 4,
                    Indices =
                    [
                        new RelatedIndexConfig { Name = "field", Field = "cities", Weight = 2 },
                        new RelatedIndexConfig { Name = "field", Field = "audiences", Weight = 1 }
                    ]
                }
            ]
        });
        var ctx = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = routed.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new RelatedContentPlugin(config).DerivePages(ctx);

        var related = ctx.Get(BuildDataKeys.RelatedPages)["r1"].Cast<Dictionary<string, object>>().ToList();
        // r3 shares a city and the audience (3); r5 and r2 share a city (2, newest first); r4 nothing (0).
        Assert.Equal(["r3", "r5", "r2", "r4"], related.Select(entry => (string)entry["id"]));
        Assert.Equal([3, 2, 2, 0], related.Select(entry => (int)entry["score"]));
        Assert.Equal("r3", related[0]["slug"]);
        Assert.DoesNotContain(related, entry => (string)entry["id"] == "p1");
    }

    private static AppConfig CreateConfig(RelatedConfig related)
        => new()
        {
            Site = new SiteConfig { Name = "t", Title = "t", Related = related },
            Content = TestContent.Markdown()
        };
}
