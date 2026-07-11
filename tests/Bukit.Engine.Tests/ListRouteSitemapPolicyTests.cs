using Bukit.Config;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ListRouteSitemapPolicyTests
{
    [Theory]
    [InlineData("CollectionList")]
    [InlineData("CollectionPage")]
    [InlineData("FilteredListPage")]
    public void BuildExcludedOutputPaths_CollectionSitemapDisabledExcludesCollectionPlans(string kindName)
    {
        var kind = Enum.Parse<ListRouteKind>(kindName);
        var plan = new ListRoutePlan
        {
            RouteId = $"news-{kind}",
            Kind = kind,
            Url = "/news/",
            OutputPath = $"news/{kind}/index.html",
            Template = "news-list.html",
            Collection = "news",
            TotalItems = 0,
            CanonicalUrl = "/news/"
        };
        var graph = ListRouteGraph.Create([plan]);
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["news"] = new() { Permalink = "/news/{slug}/", Output = new() { Sitemap = false } },
                    ["article"] = new() { Permalink = "/articles/{slug}/", Output = new() { Sitemap = true } }
                }
            },
            Content = TestContent.Markdown()
        };

        var excluded = ListRouteSitemapPolicy.BuildExcludedOutputPaths(config, graph);

        Assert.Contains(BuildPathUtils.NormalizeRelPath(plan.OutputPath), excluded);
    }
}
