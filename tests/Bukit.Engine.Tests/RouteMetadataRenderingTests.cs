using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Engine.RouteMetadata;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RouteMetadataRenderingTests
{
    [Fact]
    public void ApplyRouteMetadata_OverlaysListKindsAndDerivesPaginationMetadata()
    {
        var graph = ListRouteGraph.Create(new[]
        {
            Plan("home", ListRouteKind.Home, "/", metadataRouteUrl: "/"),
            Plan("collection:insights:1", ListRouteKind.CollectionList, "/insights/", metadataRouteUrl: "/insights/"),
            Plan("collection:insights:2", ListRouteKind.CollectionPage, "/insights/page/2/", page: 2, metadataRouteUrl: "/insights/"),
            Plan("filter:companies:country:malaysia:1", ListRouteKind.FilteredListPage, "/malaysia-companies/", metadataRouteUrl: "/malaysia-companies/"),
            Plan("taxonomy:category:index", ListRouteKind.TaxonomyIndex, "/insights/category/", metadataRouteUrl: "/insights/category/"),
            Plan("taxonomy:category:market:2", ListRouteKind.TaxonomyTermPage, "/insights/category/market/page/2/", page: 2, metadataRouteUrl: "/insights/category/market/")
        });
        var metadata = new Dictionary<string, RouteMetadataEntry>(StringComparer.Ordinal)
        {
            ["/"] = Entry("/", "丝路商讯", "首页介绍", "首页 SEO", "首页 SEO 描述"),
            ["/insights/"] = Entry("/insights/", "Insights", "Latest insights", "Insights SEO", "Insights SEO description"),
            ["/malaysia-companies/"] = Entry("/malaysia-companies/", "Malaysia Companies", "Malaysia summary"),
            ["/insights/category/"] = Entry("/insights/category/", "Categories", "Category index"),
            ["/insights/category/market/"] = Entry("/insights/category/market/", "Market", "Market summary")
        };

        var result = ListRouteGraphBuilder.ApplyRouteMetadata(graph, metadata);

        Assert.Equal("丝路商讯", Find(result, "/").Title);
        Assert.Equal("Malaysia Companies", Find(result, "/malaysia-companies/").Title);
        Assert.Equal("Categories", Find(result, "/insights/category/").Title);
        var insightsPage2 = Find(result, "/insights/page/2/");
        Assert.Equal("/insights/", insightsPage2.MetadataRouteUrl);
        Assert.Equal("Insights - Page 2", ListPageMetadataBuilder.BuildTitle(CreateSite(), insightsPage2, ListPageMetadataBuilder.BuildPagination(insightsPage2)));
        Assert.Contains("page 2", ListPageMetadataBuilder.BuildSummary(CreateSite(), insightsPage2, ListPageMetadataBuilder.BuildPagination(insightsPage2)), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("items 3-4 of 5", ListPageMetadataBuilder.BuildSummary(CreateSite(), insightsPage2, ListPageMetadataBuilder.BuildPagination(insightsPage2)));
        var page2Fields = ListRouteRenderPlanBuilder.BuildPageFields(insightsPage2);
        Assert.Equal("Insights - Page 2", ContentFieldReader.GetText(page2Fields, "title"));
        Assert.Contains("page 2", ContentFieldReader.GetText(page2Fields, "summary")!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Market - Page 2", ListPageMetadataBuilder.BuildTitle(CreateSite(), Find(result, "/insights/category/market/page/2/"), ListPageMetadataBuilder.BuildPagination(Find(result, "/insights/category/market/page/2/"))));
    }

    [Fact]
    public void ApplyRouteMetadata_WhenNotConfigured_PreservesConfiguredMetadata()
    {
        var graph = ListRouteGraph.Create(new[]
        {
            Plan("collection:insights:1", ListRouteKind.CollectionList, "/insights/", metadataRouteUrl: "/insights/") with
            {
                Title = "Configured title",
                Summary = "Configured summary"
            }
        });

        var result = ListRouteGraphBuilder.ApplyRouteMetadata(graph, routeMetadata: null);

        Assert.Equal("Configured title", Assert.Single(result.Routes).Title);
        Assert.Equal("Configured summary", Assert.Single(result.Routes).Summary);
    }

    [Fact]
    public void ApplyToPage_UsesVisibleTitleAndSummaryForSingletonRoute()
    {
        var page = new PageInfo { Title = "Markdown About", Url = "/about/", Content = "<p>Body</p>", Summary = "Markdown summary" };
        var metadata = new Dictionary<string, RouteMetadataEntry>
        {
            ["/about/"] = Entry("/about/", "关于我们", "Notion summary", "About SEO", "About SEO description")
        };

        var result = RouteMetadataApplicator.ApplyToPage(page, "/about/", metadata);

        Assert.Equal("关于我们", result.Title);
        Assert.Equal("Notion summary", result.Summary);
        Assert.Equal("<p>Body</p>", result.Content);
    }

    [Fact]
    public void SeoIndex_UsesSeoOverridesForSingletonAndListRoutes()
    {
        var config = CreateConfig();
        var document = ContentDocument.Create(
            id: "about",
            title: "Markdown About",
            slug: "about",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>About</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page", ["summary"] = "Markdown summary" }));
        var routed = new[] { new RoutedContentDocument(document, new RouteInfo("/about/", "about/index.html", "pages/page.html")) };
        var graph = ListRouteGraph.Create(new[]
        {
            Plan("collection:insights:1", ListRouteKind.CollectionList, "/insights/", metadataRouteUrl: "/insights/"),
            Plan("collection:insights:2", ListRouteKind.CollectionPage, "/insights/page/2/", page: 2, metadataRouteUrl: "/insights/")
        });
        var metadata = new Dictionary<string, RouteMetadataEntry>
        {
            ["/about/"] = Entry("/about/", "关于我们", "About summary", "About SEO", "About SEO description"),
            ["/insights/"] = Entry("/insights/", "商务资讯", "Insights summary", "Insights SEO", "Insights SEO description")
        };
        graph = ListRouteGraphBuilder.ApplyRouteMetadata(graph, metadata);

        var result = SeoIndexBuilder.Build(config, "/", routed, graph.Routes.Select(x => x.ToRouteInfo()).ToArray(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(), graph, metadata);

        Assert.Equal("About SEO", result.Models["about/index.html"].Title);
        Assert.Equal("About SEO description", result.Models["about/index.html"].Description);
        Assert.Equal("Insights SEO", result.Models["insights/index.html"].Title);
        Assert.Equal("Insights SEO description", result.Models["insights/index.html"].Description);
        Assert.Equal("Insights SEO - Page 2", result.Models["insights/page/2/index.html"].Title);
        Assert.Contains("page 2", result.Models["insights/page/2/index.html"].Description!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("post")]
    [InlineData("company")]
    public void SeoIndex_DoesNotApplyRouteMetadataToDetailContent(string contentKind)
    {
        var config = CreateConfig();
        var document = ContentDocument.Create(
            id: contentKind,
            title: $"Original {contentKind}",
            slug: contentKind,
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: $"<p>{contentKind}</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = contentKind,
                ["collection"] = contentKind,
                ["summary"] = $"Original {contentKind} summary"
            }));
        var route = new RouteInfo($"/{contentKind}/", $"{contentKind}/index.html", $"pages/{contentKind}.html");
        var metadata = new Dictionary<string, RouteMetadataEntry>
        {
            [route.Url] = Entry(route.Url, "Route metadata title", "Route metadata summary", "Route SEO", "Route SEO summary")
        };

        var result = SeoIndexBuilder.Build(
            config,
            "/",
            [new RoutedContentDocument(document, route)],
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            routeMetadata: metadata);

        Assert.Equal($"Original {contentKind}", result.Models[route.OutputPath].Title);
        Assert.Equal($"Original {contentKind} summary", result.Models[route.OutputPath].Description);
    }

    [Fact]
    public void RouteDependencyHash_ChangesOnlyForRouteUsingChangedMetadata()
    {
        var config = CreateConfig();
        var baseHash = RenderDependencyHasher.Compute(config, CreateSiteModel());
        var before = new Dictionary<string, RouteMetadataEntry>
        {
            ["/insights/"] = Entry("/insights/", "Insights", "Before"),
            ["/companies/"] = Entry("/companies/", "Companies", "Companies summary")
        };
        var after = new Dictionary<string, RouteMetadataEntry>(before)
        {
            ["/insights/"] = Entry("/insights/", "Insights", "After")
        };

        Assert.NotEqual(
            RenderDependencyHasher.ComputeForRoute(baseHash, "/insights/", before),
            RenderDependencyHasher.ComputeForRoute(baseHash, "/insights/", after));
        Assert.Equal(
            RenderDependencyHasher.ComputeForRoute(baseHash, "/companies/", before),
            RenderDependencyHasher.ComputeForRoute(baseHash, "/companies/", after));
    }

    [Fact]
    public void RouteDependencyHash_WithRealTemplateData_ChangesOnlyForAffectedRoute()
    {
        var config = CreateConfig();
        var beforeMetadata = new Dictionary<string, RouteMetadataEntry>
        {
            ["/insights/"] = Entry("/insights/", "Insights", "Before"),
            ["/companies/"] = Entry("/companies/", "Companies", "Companies summary")
        };
        var afterMetadata = new Dictionary<string, RouteMetadataEntry>(beforeMetadata)
        {
            ["/insights/"] = Entry("/insights/", "Insights", "After")
        };
        var beforeSite = CreateSiteModelWithRouteMetadataTemplateData("Before");
        var afterSite = CreateSiteModelWithRouteMetadataTemplateData("After");

        var beforeBaseHash = RenderDependencyHasher.Compute(config, beforeSite);
        var afterBaseHash = RenderDependencyHasher.Compute(config, afterSite);

        Assert.Equal(beforeBaseHash, afterBaseHash);
        Assert.NotEqual(
            RenderDependencyHasher.ComputeForRoute(beforeBaseHash, "/insights/", beforeMetadata),
            RenderDependencyHasher.ComputeForRoute(afterBaseHash, "/insights/", afterMetadata));
        Assert.Equal(
            RenderDependencyHasher.ComputeForRoute(beforeBaseHash, "/companies/", beforeMetadata),
            RenderDependencyHasher.ComputeForRoute(afterBaseHash, "/companies/", afterMetadata));
    }

    [Fact]
    public void RouteDependencyHash_MultilineFieldBoundariesCannotCollide()
    {
        const string baseHash = "base";
        var first = new Dictionary<string, RouteMetadataEntry>
        {
            ["/insights/"] = Entry("/insights/", "a\nb", "c")
        };
        var second = new Dictionary<string, RouteMetadataEntry>
        {
            ["/insights/"] = Entry("/insights/", "a", "b\nc")
        };

        Assert.NotEqual(
            RenderDependencyHasher.ComputeForRoute(baseHash, "/insights/", first),
            RenderDependencyHasher.ComputeForRoute(baseHash, "/insights/", second));
    }

    [Theory]
    [InlineData("post")]
    [InlineData("company")]
    public void RouteDependencyKey_DetailContentDoesNotUseRouteMetadata(string contentKind)
    {
        var document = ContentDocument.Create(
            id: contentKind,
            title: contentKind,
            slug: contentKind,
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: string.Empty,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = contentKind,
                ["collection"] = contentKind
            }));

        Assert.Null(RouteMetadataApplicator.ResolveDependencyRouteUrl(document, $"/{contentKind}/"));
    }

    [Fact]
    public void PaginationMetadata_ZhCn_PageOneAndPageTwoStayLocalizedAcrossRenderAndSeo()
    {
        var site = CreateSite() with { Language = "zh-CN" };
        var config = CreateConfig() with { Site = CreateConfig().Site with { Language = "zh-CN" } };
        var pageOne = Plan("collection:insights:1", ListRouteKind.CollectionList, "/insights/", page: 1, metadataRouteUrl: "/insights/") with
        {
            Title = "商务资讯",
            Summary = "最新商务资讯",
            SeoTitle = "商务资讯 SEO",
            SeoDescription = "商务资讯 SEO 描述"
        };
        var pageTwo = Plan("collection:insights:2", ListRouteKind.CollectionPage, "/insights/page/2/", page: 2, metadataRouteUrl: "/insights/") with
        {
            Title = "商务资讯",
            Summary = "最新商务资讯",
            SeoTitle = "商务资讯 SEO",
            SeoDescription = "商务资讯 SEO 描述"
        };
        var graph = ListRouteGraph.Create([pageOne, pageTwo]);

        Assert.Equal("商务资讯", ListPageMetadataBuilder.BuildTitle(site, pageOne, ListPageMetadataBuilder.BuildPagination(pageOne)));
        Assert.Equal("商务资讯 - 第 2 页", ListPageMetadataBuilder.BuildTitle(site, pageTwo, ListPageMetadataBuilder.BuildPagination(pageTwo)));
        Assert.DoesNotContain("Browse", ListPageMetadataBuilder.BuildSummary(site, pageTwo, ListPageMetadataBuilder.BuildPagination(pageTwo)), StringComparison.Ordinal);

        var seo = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            graph.Routes.Select(route => route.ToRouteInfo()).ToArray(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        Assert.Equal("商务资讯 SEO", seo.Models[pageOne.OutputPath].Title);
        Assert.Equal("商务资讯 SEO - 第 2 页", seo.Models[pageTwo.OutputPath].Title);
        Assert.Contains("第 2 页", seo.Models[pageTwo.OutputPath].Description!, StringComparison.Ordinal);
        Assert.DoesNotContain("Browse", seo.Models[pageTwo.OutputPath].Description!, StringComparison.Ordinal);
    }

    [Fact]
    public void PaginationMetadata_ZhCn_EmptySeoFieldsFallsBackToLocalizedRenderMetadata()
    {
        var config = CreateConfig() with { Site = CreateConfig().Site with { Language = "zh-CN" } };
        var route = Plan("collection:companies:2", ListRouteKind.CollectionPage, "/companies/page/2/", page: 2, metadataRouteUrl: "/companies/") with
        {
            Title = "企业列表",
            Summary = "企业资源介绍",
            SeoTitle = null,
            SeoDescription = null
        };
        var graph = ListRouteGraph.Create([route]);

        var seo = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            [route.ToRouteInfo()],
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        var pagination = ListPageMetadataBuilder.BuildPagination(route);
        Assert.Equal(ListPageMetadataBuilder.BuildTitle(config.Site, route, pagination), seo.Models[route.OutputPath].Title);
        Assert.Equal(ListPageMetadataBuilder.BuildSummary(config.Site, route, pagination), seo.Models[route.OutputPath].Description);
        Assert.Equal("企业列表 - 第 2 页", seo.Models[route.OutputPath].Title);
    }

    [Fact]
    public void PaginationMetadata_English_EmptySeoFieldsFallsBackToRenderMetadata()
    {
        var config = CreateConfig();
        var pageOne = Plan("collection:companies:1", ListRouteKind.CollectionList, "/companies/", page: 1, metadataRouteUrl: "/companies/") with
        {
            Title = "Companies",
            Summary = "Company resources"
        };
        var pageTwo = Plan("collection:companies:2", ListRouteKind.CollectionPage, "/companies/page/2/", page: 2, metadataRouteUrl: "/companies/") with
        {
            Title = "Companies",
            Summary = "Company resources"
        };
        var graph = ListRouteGraph.Create([pageOne, pageTwo]);

        var seo = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            graph.Routes.Select(route => route.ToRouteInfo()).ToArray(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        foreach (var route in graph.Routes)
        {
            var pagination = ListPageMetadataBuilder.BuildPagination(route);
            Assert.Equal(ListPageMetadataBuilder.BuildTitle(config.Site, route, pagination), seo.Models[route.OutputPath].Title);
            Assert.Equal(ListPageMetadataBuilder.BuildSummary(config.Site, route, pagination), seo.Models[route.OutputPath].Description);
        }
        Assert.Equal("Companies", seo.Models[pageOne.OutputPath].Title);
        Assert.Equal("Companies - Page 2", seo.Models[pageTwo.OutputPath].Title);
    }

    [Fact]
    public void RouteDependencyHash_WithPipelineBuiltSiteModels_ChangesOnlyForAffectedRoute()
    {
        var config = CreateConfig();
        var pipeline = new VariantBuildPipeline();
        var beforeSite = BuildSiteModel(pipeline, config, "Before");
        var afterSite = BuildSiteModel(pipeline, config, "After");
        var beforeMetadata = new Dictionary<string, RouteMetadataEntry>
        {
            ["/insights/"] = Entry("/insights/", "Insights", "Before"),
            ["/companies/"] = Entry("/companies/", "Companies", "Companies summary")
        };
        var afterMetadata = new Dictionary<string, RouteMetadataEntry>(beforeMetadata)
        {
            ["/insights/"] = Entry("/insights/", "Insights", "After")
        };

        var beforeBaseHash = RenderDependencyHasher.Compute(config, beforeSite);
        var afterBaseHash = RenderDependencyHasher.Compute(config, afterSite);

        Assert.Equal(beforeBaseHash, afterBaseHash);
        Assert.NotEqual(
            RenderDependencyHasher.ComputeForRoute(beforeBaseHash, "/insights/", beforeMetadata),
            RenderDependencyHasher.ComputeForRoute(afterBaseHash, "/insights/", afterMetadata));
        Assert.Equal(
            RenderDependencyHasher.ComputeForRoute(beforeBaseHash, "/companies/", beforeMetadata),
            RenderDependencyHasher.ComputeForRoute(afterBaseHash, "/companies/", afterMetadata));
    }

    [Fact]
    public void GlobalDependencyHash_IncludesRouteMetadataFieldAliases()
    {
        var config = CreateConfig();
        var changed = config with
        {
            Content = config.Content with
            {
                RouteMetadata = config.Content.RouteMetadata! with { SeoTitleField = "meta_title" }
            }
        };

        Assert.NotEqual(
            RenderDependencyHasher.Compute(config, CreateSiteModel()),
            RenderDependencyHasher.Compute(changed, CreateSiteModel()));
    }

    private static ListRoutePlan Plan(string id, ListRouteKind kind, string url, int page = 1, string? metadataRouteUrl = null) => new()
    {
        RouteId = id,
        Kind = kind,
        Url = url,
        MetadataRouteUrl = metadataRouteUrl,
        OutputPath = url == "/" ? "index.html" : $"{url.Trim('/')}/index.html",
        Template = "pages/list.html",
        PageNumber = page,
        PageSize = 2,
        TotalItems = 5,
        Items = Array.Empty<ListRouteItem>(),
        CanonicalUrl = url
    };

    private static ListRoutePlan Find(ListRouteGraph graph, string url) => Assert.Single(graph.Routes, x => x.Url == url);

    private static RouteMetadataEntry Entry(string route, string title, string summary, string? seoTitle = null, string? seoDescription = null)
        => new(route, title, summary, seoTitle, seoDescription);

    private static SiteConfig CreateSite() => new() { Name = "test", Title = "Test Site", Language = "en" };

    private static SiteModel CreateSiteModel() => new() { Name = "test", Title = "Test Site", BaseUrl = "/", Language = "en" };

    private static SiteModel CreateSiteModelWithRouteMetadataTemplateData(string summary) => new()
    {
        Name = "test",
        Title = "Test Site",
        BaseUrl = "/",
        Language = "en",
        Modules = new Dictionary<string, IReadOnlyList<ModuleInfo>>
        {
            ["page_meta"] = [new ModuleInfo { Id = "insights", Title = "Insights", Slug = "insights", Content = string.Empty }]
        },
        Data = new Dictionary<string, object>
        {
            ["page_meta"] = new[] { new Dictionary<string, object> { ["route"] = "/insights/", ["summary"] = summary } }
        },
        DataIndex = new Dictionary<string, object>
        {
            ["page_meta"] = new Dictionary<string, object> { ["routes"] = new Dictionary<string, object> { ["insights"] = summary } }
        }
    };

    private static SiteModel BuildSiteModel(VariantBuildPipeline pipeline, AppConfig config, string summary)
    {
        var routeRows = new[]
        {
            new ModuleInfo { Id = "insights", Title = "Insights", Slug = "insights", Content = summary }
        };
        return pipeline.BuildSiteModel(
            config,
            "/",
            new Dictionary<string, IReadOnlyList<ModuleInfo>> { ["page_meta"] = routeRows },
            new Dictionary<string, object> { ["page_meta"] = routeRows },
            dataIndex: new Dictionary<string, object>
            {
                ["page_meta"] = new Dictionary<string, object> { ["routes"] = summary }
            });
    }

    private static AppConfig CreateConfig() => new()
    {
        Site = CreateSite() with { Url = "https://example.com", Seo = new SeoConfig { Enabled = true } },
        Content = TestContent.Markdown() with { RouteMetadata = new RouteMetadataConfig { Source = "page_meta" } },
        Build = new BuildConfig { Output = "dist" },
        Theme = new ThemeConfig { Layouts = "layouts" }
    };
}
