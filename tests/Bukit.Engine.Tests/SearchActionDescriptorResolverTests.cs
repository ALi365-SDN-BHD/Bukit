using Bukit.Config;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SearchActionDescriptorResolverTests
{
    [Fact]
    public void Resolve_NoDeclaredRoute_ReturnsNull()
    {
        var config = CreateConfig(route: null);

        var result = SearchActionDescriptorResolver.Resolve(config, "/", Array.Empty<RouteInfo>());

        Assert.Null(result);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Resolve_DisabledContract_ReturnsNull(bool seoEnabled, bool searchActionEnabled)
    {
        var config = CreateConfig("/search/", seoEnabled, searchActionEnabled);

        var result = SearchActionDescriptorResolver.Resolve(config, "/", Array.Empty<RouteInfo>());

        Assert.Null(result);
    }

    [Theory]
    [InlineData("/docs/", "https://example.com/docs/search/?q={search_term_string}")]
    [InlineData("/docs/ms/", "https://example.com/docs/ms/search/?q={search_term_string}")]
    public void Resolve_DeclaredFinalRoute_ReturnsAbsoluteTargetWithVariantBaseUrl(
        string baseUrl,
        string expectedTarget)
    {
        var config = CreateConfig("/search/");
        var routes = new[] { new RouteInfo("/SEARCH", "search/index.html", "pages/search.html") };

        var result = SearchActionDescriptorResolver.Resolve(config, baseUrl, routes);

        Assert.NotNull(result);
        Assert.Equal(expectedTarget, result.Target);
        Assert.Equal("required name=search_term_string", result.QueryInput);
    }

    [Fact]
    public void Resolve_DeclaredRouteMissingFromFinalInventory_Throws()
    {
        var config = CreateConfig("/search/");

        var ex = Assert.Throws<ConfigException>(() =>
            SearchActionDescriptorResolver.Resolve(config, "/", Array.Empty<RouteInfo>()));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("site.search.route '/search/'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("final HTML route", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_EnabledContractWithoutSiteUrl_Throws()
    {
        var config = CreateConfig("/search/", siteUrl: null);
        var routes = new[] { new RouteInfo("/search/", "search/index.html", "pages/search.html") };

        var ex = Assert.Throws<ConfigException>(() =>
            SearchActionDescriptorResolver.Resolve(config, "/", routes));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("site.url", ex.Message, StringComparison.Ordinal);
    }

    private static AppConfig CreateConfig(
        string? route,
        bool seoEnabled = true,
        bool searchActionEnabled = true,
        string? siteUrl = "https://example.com")
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = siteUrl,
                Search = new SearchDetailConfig { Route = route },
                Seo = new SeoConfig
                {
                    Enabled = seoEnabled,
                    Schema = new SeoSchemaConfig { SearchAction = searchActionEnabled }
                }
            },
            Content = TestContent.Markdown()
        };
}
