using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class CollectionRouteWhenConfigTests
{
    [Fact]
    public void Load_RouteWhen_ReadsFieldAndValue()
    {
        var collection = Load("routeWhen: { field: has_page, eq: true }").Site.Collections!["city"];

        Assert.Equal(("has_page", "true"), (collection.RouteWhen!.Field, collection.RouteWhen.Eq));
    }

    [Fact]
    public void Load_WithoutRouteWhen_IsNull()
        => Assert.Null(Load(string.Empty).Site.Collections!["city"].RouteWhen);

    [Theory]
    [InlineData("routeWhen: { eq: true }", "site.collections.city.routeWhen.field is required.")]
    [InlineData("routeWhen: { field: has_page }", "site.collections.city.routeWhen.eq is required.")]
    [InlineData("routeWhen: { field: 'has page', eq: true }", "site.collections.city.routeWhen.field 'has page' is not a valid field name.")]
    [InlineData("routeWhen: { field: has_page, eq: true, ne: false }", "Unknown config field 'site.collections.city.routeWhen.ne'")]
    public void Load_InvalidRouteWhen_Throws(string routeWhen, string message)
    {
        var exception = Assert.Throws<ConfigException>(() => Load(routeWhen));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    private static AppConfig Load(string collectionYaml)
    {
        var yaml = $$"""
            site:
              name: myblog
              title: My Blog
              collections:
                city:
                  permalink: /cities/{slug}/
                  {{collectionYaml}}
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-route-when-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        try
        {
            return ConfigLoader.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
