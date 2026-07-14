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

    private static RoutedContentDocument Routed(string id, string title, string url)
    {
        var document = ContentDocument.Create(
            id,
            title,
            id,
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["collection"] = "page"
            }));
        return new RoutedContentDocument(
            document,
            new RouteInfo(url, RoutePathBuilder.BuildOutputPathFromUrl(url), "pages/page.html"),
            document.PublishAt);
    }
}
