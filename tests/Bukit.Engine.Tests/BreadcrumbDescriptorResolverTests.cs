using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.RouteMetadata;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BreadcrumbDescriptorResolverTests
{
    [Fact]
    public void Resolve_PageTwo_IncludesOnlyRealCompaniesParentAndCurrentRoute()
    {
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            Config(),
            "/",
            [
                Routed("companies", "企业名录", "/companies/"),
                Routed("companies-page-2", "企业名录 - 第 2 页", "/companies/page/2/")
            ],
            listRouteGraph: null,
            staticEntries: null,
            routeMetadata: null);

        var descriptor = Assert.IsType<BreadcrumbDescriptor>(catalog.Find("/companies/page/2/"));
        Assert.Collection(
            descriptor.Items,
            item =>
            {
                Assert.Equal("企业名录", item.Name);
                Assert.Equal("https://example.com/companies/", item.Item);
            },
            item =>
            {
                Assert.Equal("企业名录 - 第 2 页", item.Name);
                Assert.Equal("https://example.com/companies/page/2/", item.Item);
            });
        Assert.DoesNotContain(descriptor.Items, item => item.Item.EndsWith("/companies/page/", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_TaxonomyIndexMissing_SkipsInventedIntermediateRoute()
    {
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            Config(),
            "/",
            [
                Routed("insights", "商务资讯", "/insights/"),
                Routed("category-market", "商务资讯：市场观察", "/insights/category/market/")
            ],
            listRouteGraph: null,
            staticEntries: null,
            routeMetadata: null);

        var descriptor = Assert.IsType<BreadcrumbDescriptor>(catalog.Find("/insights/category/market/"));
        Assert.Equal(
            ["https://example.com/insights/", "https://example.com/insights/category/market/"],
            descriptor.Items.Select(item => item.Item));
        Assert.DoesNotContain(descriptor.Items, item => item.Item.EndsWith("/insights/category/", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_TaxonomyIndexExists_IncludesTheRealIntermediateRoute()
    {
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            Config(),
            "/docs/",
            [
                Routed("insights", "商务资讯", "/insights/"),
                Routed("category", "资讯分类", "/insights/category/"),
                Routed("category-market", "商务资讯：市场观察", "/insights/category/market/")
            ],
            listRouteGraph: null,
            staticEntries: null,
            routeMetadata: null);

        var descriptor = Assert.IsType<BreadcrumbDescriptor>(catalog.Find("/insights/category/market/"));
        Assert.Equal(
            [
                "https://example.com/docs/insights/",
                "https://example.com/docs/insights/category/",
                "https://example.com/docs/insights/category/market/"
            ],
            descriptor.Items.Select(item => item.Item));
    }

    [Fact]
    public void Resolve_UsesVisibleRouteMetadataTitleAndMatchesCaseInsensitively()
    {
        var routeMetadata = new Dictionary<string, RouteMetadataEntry>(StringComparer.Ordinal)
        {
            ["/companies/"] = new("/companies/", "企业目录", "目录摘要", "目录 SEO", "目录 SEO 描述")
        };
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            Config(),
            "/",
            [
                Routed("companies", "Companies", "/companies/"),
                Routed("companies-page-2", "第 2 页", "/companies/page/2/")
            ],
            listRouteGraph: null,
            staticEntries: null,
            routeMetadata);

        var descriptor = Assert.IsType<BreadcrumbDescriptor>(catalog.Find("/COMPANIES/page/2"));
        Assert.Equal("企业目录", descriptor.Items[0].Name);
        Assert.Equal("https://example.com/companies/", descriptor.Items[0].Item);
    }

    [Fact]
    public void Resolve_HomeHasNoDescriptorAndOrphanKeepsCurrentNode()
    {
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            Config(),
            "/",
            [Routed("home", "首页", "/"), Routed("orphan", "孤立页面", "/deep/orphan/")],
            listRouteGraph: null,
            staticEntries: null,
            routeMetadata: null);

        Assert.Null(catalog.Find("/"));
        var descriptor = Assert.IsType<BreadcrumbDescriptor>(catalog.Find("/deep/orphan/"));
        var current = Assert.Single(descriptor.Items);
        Assert.Equal("孤立页面", current.Name);
        Assert.Equal("https://example.com/deep/orphan/", current.Item);
    }

    [Fact]
    public void Resolve_ManagedStaticHtmlCanBeARealParent()
    {
        var staticParent = new RenderEntry(
            RenderEntryKind.Static,
            Document: null,
            new RouteInfo("/help/", "help/index.html", "pages/static.html"),
            SourceDocuments: null,
            IncludeContent: false,
            ListPageFields: null,
            ListPageContext: null,
            RawContent: "<h1>帮助中心</h1>",
            Title: "帮助中心");
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            Config(),
            "/",
            [Routed("topic", "常见问题", "/help/topic/")],
            listRouteGraph: null,
            staticEntries: [staticParent],
            routeMetadata: null);

        var descriptor = Assert.IsType<BreadcrumbDescriptor>(catalog.Find("/help/topic/"));
        Assert.Equal(["帮助中心", "常见问题"], descriptor.Items.Select(item => item.Name));
    }

    [Fact]
    public void Resolve_MissingSiteUrl_PreservesRelativeBreadcrumbTargets()
    {
        var config = Config() with { Site = Config().Site with { Url = null } };
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            config,
            "/docs/",
            [Routed("orphan", "孤立页面", "/deep/orphan/")],
            listRouteGraph: null,
            staticEntries: null,
            routeMetadata: null);

        var descriptor = Assert.IsType<BreadcrumbDescriptor>(catalog.Find("/deep/orphan/"));
        Assert.Equal("/docs/deep/orphan/", Assert.Single(descriptor.Items).Item);
    }

    [Fact]
    public void Resolve_BreadcrumbHome_PrependsHomeUsingHomeTitleOrSiteTitle()
    {
        var config = Config(schema => schema with { BreadcrumbHome = true });
        var withHome = BreadcrumbDescriptorResolver.Resolve(
            config,
            "/",
            [Routed("home", "首页", "/"), Routed("a", "文章", "/a/")],
            listRouteGraph: null,
            staticEntries: null,
            routeMetadata: null);
        var withoutHomeRoute = BreadcrumbDescriptorResolver.Resolve(
            config,
            "/",
            [Routed("a", "文章", "/a/")],
            listRouteGraph: null,
            staticEntries: null,
            routeMetadata: null);

        Assert.Null(withHome.Find("/"));
        Assert.Equal(["首页", "文章"], withHome.Find("/a/")!.Items.Select(item => item.Name));
        Assert.Equal("https://example.com/", withHome.Find("/a/")!.Items[0].Item);
        Assert.Equal(["站点", "文章"], withoutHomeRoute.Find("/a/")!.Items.Select(item => item.Name));
    }

    [Fact]
    public void Resolve_BreadcrumbParent_UsesDocumentsFirstDeclaredTermPage()
    {
        var config = Config(schema => schema with { BreadcrumbHome = true, BreadcrumbParent = "categories" });
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            config,
            "/",
            [
                Routed("p1", "文章一", "/2596/", ["社团", "新闻"]),
                Routed("p2", "文章二", "/2597/"),
                Routed("p3", "文章三", "/2598/")
            ],
            TaxonomyGraph(),
            staticEntries: null,
            routeMetadata: null);

        Assert.Equal(["站点", "社团动态", "文章一"], catalog.Find("/2596/")!.Items.Select(item => item.Name));
        Assert.Equal("https://example.com/categories/club/", catalog.Find("/2596/")!.Items[1].Item);
        // No declared terms: first term route that lists the item (page 2 items still count).
        Assert.Equal(["站点", "最新動態", "文章二"], catalog.Find("/2597/")!.Items.Select(item => item.Name));
        Assert.Equal(["站点", "文章三"], catalog.Find("/2598/")!.Items.Select(item => item.Name));
        Assert.Equal(
            ["站点", "最新動態", "最新動態 - 第 2 页"],
            catalog.Find("/categories/news/page/2/")!.Items.Select(item => item.Name));
    }

    [Fact]
    public void Resolve_BreadcrumbParentForOtherKind_LeavesContentUnchanged()
    {
        var config = Config(schema => schema with { BreadcrumbParent = "tags" });
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            config,
            "/",
            [Routed("p1", "文章一", "/2596/", ["新闻"])],
            TaxonomyGraph(),
            staticEntries: null,
            routeMetadata: null);

        Assert.Equal(["文章一"], catalog.Find("/2596/")!.Items.Select(item => item.Name));
    }

    [Fact]
    public void Resolve_BreadcrumbParent_SameTermNameUnderDifferentParents_UsesListingTermPage()
    {
        var config = Config(schema => schema with { BreadcrumbParent = "categories" });
        var graph = ListRouteGraph.Create(
        [
            TermRoute("新闻", "club-news", "社团新闻", "/categories/club/news/", 1, ["/2596/"]),
            TermRoute("新闻", "trade-news", "商贸新闻", "/categories/trade/news/", 1, ["/2597/"])
        ]);
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            config,
            "/",
            [Routed("p1", "文章一", "/2596/", ["新闻"]), Routed("p2", "文章二", "/2597/", ["新闻"])],
            graph,
            staticEntries: null,
            routeMetadata: null);

        Assert.Equal(["社团新闻", "文章一"], catalog.Find("/2596/")!.Items.Select(item => item.Name));
        Assert.Equal(["商贸新闻", "文章二"], catalog.Find("/2597/")!.Items.Select(item => item.Name));
    }

    [Fact]
    public void Resolve_BreadcrumbParent_UsesRouteMetadataTitleOfTermPage()
    {
        var config = Config(schema => schema with { BreadcrumbParent = "categories" });
        var graph = ListRouteGraph.Create(
        [
            TermRoute("news", "news", "categories: news", "/categories/news/", 1, ["/2596/"]) with { RouteMetadataApplied = true, Title = "最新動態" }
        ]);
        var catalog = BreadcrumbDescriptorResolver.Resolve(
            config,
            "/",
            [Routed("p1", "文章一", "/2596/", ["news"])],
            graph,
            staticEntries: null,
            routeMetadata: null);

        Assert.Equal(["最新動態", "文章一"], catalog.Find("/2596/")!.Items.Select(item => item.Name));
    }

    private static ListRouteGraph TaxonomyGraph()
        => ListRouteGraph.Create(
        [
            TermRoute("新闻", "news", "最新動態", "/categories/news/", 1, ["/2596/", "/2597/"]),
            TermRoute("新闻", "news", "最新動態 - 第 2 页", "/categories/news/page/2/", 2, ["/2598-not-routed/"]),
            TermRoute("社团", "club", "社团动态", "/categories/club/", 1, ["/2596/"]),
            TermRoute("标签", "tag", "标签", "/tags/tag/", 1, ["/2598/"], kind: "tags")
        ]);

    private static ListRoutePlan TermRoute(
        string term,
        string slug,
        string title,
        string url,
        int page,
        IReadOnlyList<string> itemUrls,
        string kind = "categories")
        => new()
        {
            RouteId = $"taxonomy:{kind}:{slug}:{page}",
            Kind = ListRouteKind.TaxonomyTermPage,
            Url = url,
            OutputPath = RoutePathBuilder.BuildOutputPathFromUrl(url),
            Template = "pages/category.html",
            Title = title,
            PageNumber = page,
            TotalItems = itemUrls.Count,
            Items = itemUrls.Select(itemUrl => new ListRouteItem { Id = itemUrl, Title = itemUrl, Url = itemUrl }).ToArray(),
            CanonicalUrl = url,
            TaxonomyContext = new ListRouteTaxonomyContext { Kind = kind, Term = term, Slug = slug }
        };

    private static AppConfig Config(Func<SeoSchemaConfig, SeoSchemaConfig> schema)
    {
        var config = Config();
        return config with
        {
            Site = config.Site with
            {
                Seo = config.Site.Seo with { Schema = schema(config.Site.Seo.Schema) }
            }
        };
    }

    private static AppConfig Config()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "site",
                Title = "站点",
                Url = "https://example.com",
                Language = "zh-CN"
            },
            Content = TestContent.Markdown()
        };

    private static RoutedContentDocument Routed(string id, string title, string url, IReadOnlyList<string>? categories = null)
    {
        var fields = new Dictionary<string, object>
        {
            ["type"] = "page",
            ["collection"] = "page"
        };
        if (categories is not null)
        {
            fields["categories"] = categories.ToList();
        }

        var document = ContentDocument.Create(
            id,
            title,
            id,
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
            null,
            ContentFieldReader.ToFieldMap(fields));
        return new RoutedContentDocument(
            document,
            new RouteInfo(url, RoutePathBuilder.BuildOutputPathFromUrl(url), "pages/page.html"),
            document.PublishAt);
    }
}
