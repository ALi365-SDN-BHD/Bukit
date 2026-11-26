using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class RelatedCollectionConfigTests
{
    [Fact]
    public void Load_FieldIndicesAndCollectionRules()
    {
        var related = Load("""
              related:
                enabled: true
                threshold: 50
                limit: 3
                indices:
                  - { name: tags, weight: 80 }
                  - { field: region, weight: 10 }
                collections:
                  route:
                    threshold: 0
                    indices: [{ field: cities, weight: 2 }]
            """).Site.Related;

        Assert.Equal(("field", "region", 10), (related.Indices[1].Name, related.Indices[1].Field, related.Indices[1].Weight));
        Assert.Null(related.Indices[0].Field);
        var route = Assert.Single(related.Collections);
        Assert.Equal(("route", 0, 3), (route.Collection, route.Threshold, route.Limit));
        Assert.Equal("cities", Assert.Single(route.Indices).Field);
    }

    [Theory]
    [InlineData("indices: [{ weight: 1 }]", "site.related.indices[0] needs exactly one of name or field.")]
    [InlineData("indices: [{ name: tags, field: x }]", "site.related.indices[0] needs exactly one of name or field.")]
    [InlineData("indices: [{ field: 'bad name' }]", "site.related.indices[0].field 'bad name' is not a valid field name.")]
    [InlineData("collections: { route: { threshold: 0 } }", "site.related.collections.route.indices must list at least one index.")]
    [InlineData("collections: { route: { limit: 0, indices: [{ field: a }] } }", "site.related.collections.route needs limit of at least 1")]
    [InlineData("collections: { route: { top: 1, indices: [{ field: a }] } }", "Unknown config field 'site.related.collections.route.top'")]
    public void Load_InvalidRelated_Throws(string related, string message)
    {
        var exception = Assert.Throws<ConfigException>(() => Load($$"""
              related:
                {{related}}
            """));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    private static AppConfig Load(string siteYaml)
    {
        var yaml = $$"""
            site:
              name: myblog
              title: My Blog
            {{siteYaml}}
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-related-{Guid.NewGuid():N}.yaml");
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
