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

public sealed class TaxonomyPluginDerivePagesTests
{
    private static BuildContext CreateContext(
        IReadOnlyList<(ContentDocument Item, RouteInfo Route)> routed,
        TaxonomyConfig? taxonomyConfig = null,
        string outputMode = "pages",
        string outputPathEncoding = "none",
        CanonicalContentGraph? contentGraph = null,
        string language = "en")
    {
        var layoutsDir = CreateTaxonomyLayoutsDir();
        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "test",
                    Language = language,
                    OutputPathEncoding = outputPathEncoding
                },
                Content = TestContent.Markdown(),
                Taxonomy = taxonomyConfig ?? new TaxonomyConfig
                {
                    OutputMode = outputMode,
                    IndexEnabled = true
                }
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = layoutsDir,
            RoutedDocuments = routed.ToRoutedDocuments(),
            ContentGraph = contentGraph ?? CanonicalContentGraph.Empty,
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }

    private static string ResolveTemplateKind(string kind)
        => kind.Trim().ToLowerInvariant() switch
        {
            "taxonomy_index" => "pages/taxonomy-index.html",
            "taxonomy_term" => "pages/taxonomy-term.html",
            _ => throw new ConfigException($"Unexpected template kind: {kind}")
        };

    private static string CreateTaxonomyLayoutsDir()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-taxonomy-tests-" + Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-index.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-term.html"), "{{ page.content }}");
        return layoutsDir;
    }

    private static (ContentDocument Item, RouteInfo Route) CreateItem(
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

        var item = ContentDocument.Create(
            id: id,
            title: title,
            slug: id,
            publishAt: publishAt,
            contentHtml: $"<p>{title}</p>",
            fields: ContentFieldReader.ToFieldMap(meta));
        var route = new RouteInfo($"/blog/{id}/", $"blog/{id}/index.html", "pages/post.html");
        return (item, route);
    }

    [Fact]
    public void DerivePages_GeneratesTermPagesForTags()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha", "beta" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/beta/");
    }

    [Fact]
    public void DerivePages_GeneratesTermPagesForCategories()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: new[] { "News", "Tech" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/categories/news/");
        Assert.Contains(derived, x => x.Route.Url == "/categories/tech/");
    }

    [Fact]
    public void DerivePages_GeneratesIndexPages()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
    }

    [Fact]
    public void DerivePages_OutputPathEncodingSanitize_AppliesToTermOutputPath()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "hello world" }),
        };
        var ctx = CreateContext(routed, outputPathEncoding: "sanitize");

        var derived = new TaxonomyPlugin(ctx.Config).DerivePages(ctx);

        var term = Assert.Single(derived, x => x.Route.Url == "/tags/hello-world/");
        Assert.Equal("tags/hello-world/index.html", term.Route.OutputPath);
    }

    [Fact]
    public void DerivePages_PinnedItemsSortedFirst()
    {
        var unpinned = CreateItem("p1", "Not Pinned", new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "topic" }, pinned: false);
        var pinned = CreateItem("p2", "Pinned", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "topic" }, pinned: true);
        var routed = new List<(ContentDocument Item, RouteInfo Route)> { unpinned, pinned };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        var termPage = Assert.Single(derived, x => x.Route.Url == "/tags/topic/");
        Assert.NotNull(termPage.Document.CustomFields);
        Assert.True(termPage.Document.CustomFields!.ContainsKey("items"));
        var items = Assert.IsType<List<object>>(termPage.Document.CustomFields["items"].Value);
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
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        routed[0] = routed[0] with
        {
            Item = routed[0].Item with
            {
                CustomFields = ContentFieldReader.WithValues(routed[0].Item.CustomFields, new Dictionary<string, object>
                {
                    ["series"] = new[] { "My Series" },
                    ["authors"] = new[] { "Alice" }
                }),
                Route = ContentRoutePolicy.FromFields(ContentFieldReader.WithValues(routed[0].Item.CustomFields, new Dictionary<string, object>
                {
                    ["series"] = new[] { "My Series" },
                    ["authors"] = new[] { "Alice" }
                })),
                Publish = ContentPublishPolicy.FromFields(ContentFieldReader.WithValues(routed[0].Item.CustomFields, new Dictionary<string, object>
                {
                    ["series"] = new[] { "My Series" },
                    ["authors"] = new[] { "Alice" }
                }))
            }
        };
        var ctx = CreateContext(routed, taxonomyConfig: config);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/series/");
        Assert.Contains(derived, x => x.Route.Url == "/series/my-series/");
        Assert.Contains(derived, x => x.Route.Url == "/authors/");
        Assert.Contains(derived, x => x.Route.Url == "/authors/alice/");
        Assert.DoesNotContain(derived, x => x.Route.Url.StartsWith("/tags/"));
    }

    [Fact]
    public void DerivePages_TaxonomyKindRoutePrefix_UsesConfiguredBusinessPath()
    {
        var config = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = true,
            PageSize = 1,
            Kinds = new List<TaxonomyKindConfig>
            {
                new()
                {
                    Key = "categories",
                    Kind = "category",
                    Title = "Categories",
                    Description = "Browse business insight categories.",
                    SingularTitlePrefix = "Category",
                    RoutePrefix = "/insights/category"
                }
            }
        };
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: new[] { "市场观察" }),
            CreateItem("p2", "Post 2", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), categories: new[] { "市场观察" })
        };
        var ctx = CreateContext(routed, taxonomyConfig: config);

        var derived = new TaxonomyPlugin(ctx.Config).DerivePages(ctx);

        var index = Assert.Single(derived, x => x.Route.Url == "/insights/category/");
        var termPage = Assert.Single(derived, x => x.Route.Url == "/insights/category/市场观察/");
        Assert.Contains(derived, x => x.Route.Url == "/insights/category/市场观察/page/2/");
        Assert.DoesNotContain(derived, x => x.Route.Url == "/category/市场观察/");

        var terms = Assert.IsType<List<object>>(index.Document.CustomFields!["terms"].Value);
        Assert.Equal("Browse business insight categories.", index.Document.CustomFields!["summary"].Value);
        var term = Assert.IsType<Dictionary<string, object>>(terms[0]);
        Assert.Equal("/insights/category/市场观察/", term["url"]);
        var taxonomy = Assert.IsType<Dictionary<string, object>>(termPage.Document.CustomFields!["taxonomy"].Value);
        Assert.Equal("/insights/category", taxonomy["route_prefix"]);
        Assert.Equal("/insights/category", taxonomy["routePrefix"]);
        Assert.Equal("/insights/category/市场观察/", taxonomy["url"]);

        var siteTaxonomy = Assert.IsType<Dictionary<string, object>>(ctx.Data["taxonomy"]);
        var categoryData = Assert.IsType<Dictionary<string, object>>(siteTaxonomy["category"]);
        var dataTerms = Assert.IsType<List<object>>(categoryData["terms"]);
        var dataTerm = Assert.IsType<Dictionary<string, object>>(dataTerms[0]);
        Assert.Equal("/insights/category/市场观察/", dataTerm["url"]);
    }

    [Fact]
    public void DerivePages_TaxonomyKindRoutePrefix_CanBeMergedIntoListRouteGraph()
    {
        var config = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = true,
            PageSize = 1,
            Kinds = new List<TaxonomyKindConfig>
            {
                new()
                {
                    Key = "categories",
                    Kind = "category",
                    Title = "Categories",
                    RoutePrefix = "/insights/category"
                }
            }
        };
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: new[] { "market" }),
            CreateItem("p2", "Post 2", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), categories: new[] { "market" })
        };
        var ctx = CreateContext(routed, taxonomyConfig: config);
        var derived = new TaxonomyPlugin(ctx.Config).DerivePages(ctx);

        var graph = ListRouteGraphBuilder.AddDerivedTaxonomyRoutes(ListRouteGraph.Empty, derived);

        Assert.Contains(graph.Routes, route =>
            route.Kind == ListRouteKind.TaxonomyIndex &&
            route.Url == "/insights/category/" &&
            route.TaxonomyContext?.IsIndex is true &&
            route.Items.Single().Url == "/insights/category/market/");
        Assert.Contains(graph.Routes, route =>
            route.Kind == ListRouteKind.TaxonomyTermPage &&
            route.Url == "/insights/category/market/" &&
            route.NextUrl == "/insights/category/market/page/2/" &&
            route.TaxonomyContext?.Slug == "market");
        Assert.Contains(graph.Routes, route =>
            route.Kind == ListRouteKind.TaxonomyTermPage &&
            route.Url == "/insights/category/market/page/2/" &&
            route.PrevUrl == "/insights/category/market/");
    }

    [Fact]
    public void DerivePages_EmptyItems_ReturnsEmpty()
    {
        var ctx = CreateContext(new List<(ContentDocument Item, RouteInfo Route)>());

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_ItemsWithoutTaxonomy_ReturnsEmpty()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_ItemsWithCategoriesOnly_NoTagsPages()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: new[] { "News" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.DoesNotContain(derived, x => x.Route.Url.StartsWith("/tags/"));
        Assert.Contains(derived, x => x.Route.Url == "/categories/");
        Assert.Contains(derived, x => x.Route.Url == "/categories/news/");
    }

    [Fact]
    public void DerivePages_UsesStructuredTaxonomyAndSummary()
    {
        var publishAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var item = ContentDocument.Create(
            id: "p1",
            title: "Post 1",
            slug: "p1",
            publishAt: publishAt,
            contentHtml: "<p>Post 1</p>",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["tags"] = new("list", new object[] { "alpha" }),
                ["summary"] = new("text", "Canonical taxonomy summary")
            });
        var route = new RouteInfo("/blog/p1/", "blog/p1/index.html", "pages/post.html");
        var ctx = CreateContext(new List<(ContentDocument Item, RouteInfo Route)> { (item, route) });

        var derived = new TaxonomyPlugin(ctx.Config).DerivePages(ctx);

        var termPage = Assert.Single(derived, x => x.Route.Url == "/tags/alpha/");
        var items = Assert.IsType<List<object>>(termPage.Document.CustomFields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(items[0]);
        Assert.Equal("Canonical taxonomy summary", first["summary"]);
    }

    [Fact]
    public void DerivePages_ItemFields_ProjectListCompatibleItemFields()
    {
        var publishAt = new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var item = ContentDocument.Create(
            id: "p1",
            title: "Post 1",
            slug: "p1",
            publishAt: publishAt,
            contentHtml: "<p>Post 1</p>",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["tags"] = new("list", new object[] { "alpha" }),
                ["categories"] = new("list", new object[] { "市场观察" }),
                ["cover"] = new("text", "/covers/market.jpg"),
                ["description"] = new("text", "Canonical fallback summary")
            });
        var route = new RouteInfo("/blog/p1/", "blog/p1/index.html", "pages/post.html");
        var taxonomy = new TaxonomyConfig
        {
            OutputMode = "pages",
            ItemFields = new[] { "cover", "categories", "summary", "date" }
        };
        var ctx = CreateContext(new List<(ContentDocument Item, RouteInfo Route)> { (item, route) }, taxonomyConfig: taxonomy);

        var derived = new TaxonomyPlugin(ctx.Config).DerivePages(ctx);

        var termPage = Assert.Single(derived, x => x.Route.Url == "/tags/alpha/");
        var items = Assert.IsType<List<object>>(termPage.Document.CustomFields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(items[0]);
        var fields = Assert.IsType<Dictionary<string, object>>(first["fields"]);
        var cover = Assert.IsType<Dictionary<string, object?>>(fields["cover"]);
        var categories = Assert.IsType<Dictionary<string, object?>>(fields["categories"]);
        var summary = Assert.IsType<Dictionary<string, object?>>(fields["summary"]);
        var date = Assert.IsType<Dictionary<string, object?>>(fields["date"]);

        Assert.Equal("/covers/market.jpg", cover["value"]);
        Assert.Equal(new object[] { "市场观察" }, Assert.IsType<object[]>(categories["value"]));
        Assert.Equal("Canonical fallback summary", summary["value"]);
        Assert.Equal("2024-06-02", date["value"]);
    }

    [Fact]
    public void DerivePages_ShouldUseCanonicalGraphTaxonomy_WhenItemHasNoTaxonomyFields()
    {
        var publishAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var item = ContentDocument.Create(
            id: "p1",
            title: "Post 1",
            slug: "p1",
            publishAt: publishAt,
            contentHtml: "<p>Post 1</p>");
        var route = new RouteInfo("/blog/p1/", "blog/p1/index.html", "pages/post.html");
        var graph = new CanonicalContentGraph(
        [
            new ContentRecord(
                new ContentIdentity("p1", "p1", "p1", "post", "published"),
                new ContentPresentation("Post 1", "Canonical taxonomy summary", "<p>Post 1</p>", "en", []),
                new ContentClassification("post", "post", ["Canonical Category"], ["Canonical Tag"]),
                new ContentOwnership(null, null, null, null),
                new ContentLifecycle(publishAt, null, null, null),
                new ProvenanceRecord("markdown", null, [], [], null),
                new TrustMetadata(null, "published", []),
                [],
                [],
                [])
        ], []);
        var ctx = CreateContext(new List<(ContentDocument Item, RouteInfo Route)> { (item, route) }, contentGraph: graph);

        var derived = new TaxonomyPlugin(ctx.Config).DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/canonical-tag/");
        var categoryPage = Assert.Single(derived, x => x.Route.Url == "/categories/canonical-category/");
        var items = Assert.IsType<List<object>>(categoryPage.Document.CustomFields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(items[0]);
        Assert.Equal("Canonical taxonomy summary", first["summary"]);
    }

    [Fact]
    public void DerivePages_IndexEnabledFalse_NoIndexPages()
    {
        var taxonomyConfig = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = false
        };
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = CreateContext(routed, taxonomyConfig: taxonomyConfig);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.DoesNotContain(derived, x => x.Route.Url == "/tags/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
    }

    [Fact]
    public void DerivePages_TermPageTitleContainsPrefix()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "AlphaTag" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        var termPage = Assert.Single(derived, x => x.Route.Url == "/tags/alphatag/");
        Assert.Equal("Tag: AlphaTag", termPage.Document.Title);
    }

    [Fact]
    public void DerivePages_ZhCn_LocalizesConfiguredTermTitleAndPaginationSummary()
    {
        var taxonomy = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = false,
            PageSize = 2,
            Kinds =
            [
                new TaxonomyKindConfig
                {
                    Key = "categories",
                    Kind = "category",
                    Title = "资讯分类",
                    SingularTitlePrefix = "商务资讯",
                    RoutePrefix = "/insights/category"
                }
            ]
        };
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: ["市场观察"]),
            CreateItem("p2", "Post 2", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), categories: ["市场观察"]),
            CreateItem("p3", "Post 3", new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero), categories: ["市场观察"])
        };
        var context = CreateContext(routed, taxonomyConfig: taxonomy, language: "zh-CN");

        var derived = new TaxonomyPlugin(context.Config).DerivePages(context);

        var first = Assert.Single(derived, page => page.Route.Url == "/insights/category/市场观察/");
        Assert.Equal("商务资讯：市场观察", first.Document.Title);
        Assert.Equal("浏览“市场观察”下的内容，共 3 项。", ContentFieldReader.GetText(first.Document.CustomFields, "summary"));

        var second = Assert.Single(derived, page => page.Route.Url == "/insights/category/市场观察/page/2/");
        Assert.Equal("商务资讯：市场观察 - 第 2 页", second.Document.Title);
        Assert.Equal(
            "浏览“市场观察”下的内容，第 2 页，显示第 3 项，共 3 项。",
            ContentFieldReader.GetText(second.Document.CustomFields, "summary"));
    }

    [Fact]
    public void DerivePages_English_PaginationUsesLocalizedListSuffixAndRange()
    {
        var taxonomy = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = false,
            PageSize = 1,
            Kinds =
            [
                new TaxonomyKindConfig
                {
                    Key = "categories",
                    Kind = "category",
                    Title = "Categories",
                    SingularTitlePrefix = "Category"
                }
            ]
        };
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: ["Market"]),
            CreateItem("p2", "Post 2", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), categories: ["Market"])
        };
        var context = CreateContext(routed, taxonomyConfig: taxonomy, language: "en");

        var derived = new TaxonomyPlugin(context.Config).DerivePages(context);

        var second = Assert.Single(derived, page => page.Route.Url == "/category/market/page/2/");
        Assert.Equal("Category: Market - Page 2", second.Document.Title);
        Assert.Equal(
            "Browse content in Market, page 2, showing item 2 of 2.",
            ContentFieldReader.GetText(second.Document.CustomFields, "summary"));
    }

    [Fact]
    public void DerivePages_ZhCn_LocalizesBuiltInTaxonomyDefaults()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: ["AlphaTag"])
        };
        var context = CreateContext(routed, language: "zh-CN");

        var derived = new TaxonomyPlugin(context.Config).DerivePages(context);

        var index = Assert.Single(derived, page => page.Route.Url == "/tags/");
        Assert.Equal("标签", index.Document.Title);
        Assert.Equal("浏览全部标签。", ContentFieldReader.GetText(index.Document.CustomFields, "summary"));

        var term = Assert.Single(derived, page => page.Route.Url == "/tags/alphatag/");
        Assert.Equal("标签：AlphaTag", term.Document.Title);
    }

    [Fact]
    public void DerivePages_ZhCn_ExplicitTermDescriptionGetsLocalizedPaginationSuffix()
    {
        var taxonomy = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = false,
            PageSize = 1,
            Kinds =
            [
                new TaxonomyKindConfig
                {
                    Key = "categories",
                    Kind = "category",
                    Title = "资讯分类",
                    SingularTitlePrefix = "商务资讯"
                }
            ]
        };
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: ["Market"]),
            CreateItem("p2", "Post 2", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), categories: ["Market"])
        };
        var context = CreateContext(routed, taxonomyConfig: taxonomy, language: "zh-CN");
        context.Data["taxonomy_ensure_terms"] = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["category"] =
            [
                new Dictionary<string, object>
                {
                    ["slug"] = "market",
                    ["description"] = "市场观察资讯"
                }
            ]
        };

        var derived = new TaxonomyPlugin(context.Config).DerivePages(context);

        var first = Assert.Single(derived, page => page.Route.Url == "/category/market/");
        Assert.Equal("市场观察资讯", ContentFieldReader.GetText(first.Document.CustomFields, "summary"));

        var second = Assert.Single(derived, page => page.Route.Url == "/category/market/page/2/");
        Assert.Equal(
            "市场观察资讯 第 2 页，显示第 2 项，共 2 项。",
            ContentFieldReader.GetText(second.Document.CustomFields, "summary"));
    }

    [Fact]
    public void DerivePages_ZhCn_EmptyTermUsesCustomKindKeyAndLocalizedSummary()
    {
        var taxonomy = new TaxonomyConfig
        {
            OutputMode = "pages",
            Kinds =
            [
                new TaxonomyKindConfig
                {
                    Key = "topics",
                    Kind = "topic"
                }
            ]
        };
        var context = CreateContext([], taxonomyConfig: taxonomy, language: "zh-CN");
        context.Data["taxonomy_ensure_terms"] = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["topic"] =
            [
                new Dictionary<string, object>
                {
                    ["title"] = "空主题",
                    ["slug"] = "empty"
                }
            ]
        };

        var derived = new TaxonomyPlugin(context.Config).DerivePages(context);

        var index = Assert.Single(derived, page => page.Route.Url == "/topic/");
        Assert.Equal("topic", index.Document.Title);
        Assert.Equal("浏览全部topic。", ContentFieldReader.GetText(index.Document.CustomFields, "summary"));

        var term = Assert.Single(derived, page => page.Route.Url == "/topic/empty/");
        Assert.Equal("topic：空主题", term.Document.Title);
        Assert.Equal("浏览“空主题”下的内容。", ContentFieldReader.GetText(term.Document.CustomFields, "summary"));
    }

    [Fact]
    public void DerivePages_CommaSeparatedTags_AreParsedCorrectly()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = "alpha, beta, gamma"
        };
        var item = ContentDocument.Create(
            id: "p1",
            title: "Post 1",
            slug: "post-1",
            publishAt: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>content</p>",
            fields: ContentFieldReader.ToFieldMap(meta));
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (item, new RouteInfo("/blog/p1/", "blog/p1/index.html", "pages/post.html")),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/beta/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/gamma/");
    }

    [Fact]
    public void DerivePages_WithBaseUrl_PrefixIsApplied()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = TestContent.Markdown(),
                Taxonomy = new TaxonomyConfig { OutputMode = "pages", IndexEnabled = true }
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/my-site",
            LayoutsDir = CreateTaxonomyLayoutsDir(),
            RoutedDocuments = routed.ToRoutedDocuments(),
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
    }

    [Fact]
    public void DerivePages_OutputModeData_ReturnsEmpty()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = CreateContext(routed, outputMode: "data");

        var plugin = new TaxonomyPlugin(ctx.Config);
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }
}
