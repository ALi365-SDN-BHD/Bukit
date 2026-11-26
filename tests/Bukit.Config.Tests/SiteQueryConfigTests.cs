using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class SiteQueryConfigTests
{
    [Fact]
    public void Load_Queries_ReadsEveryPart()
    {
        var config = Load("""
              queries:
                route_cities:
                  from: city
                  where:
                    - { field: has_page, eq: true }
                    - { field: region, in: [north, south] }
                    - { hasBackref: routes }
                  sort:
                    - { field: featured, direction: desc }
                    - { field: category, values: [food, nature] }
                  select: [image]
                  limit: 6
                months:
                  from: route
                  group: { field: best_months, values: [1, 2, 3] }
                types:
                  from: attraction
                  distinct: type
            """);

        Assert.Collection(
            config.Site.Queries,
            query =>
            {
                Assert.Equal(("route_cities", "city", 6), (query.Name, query.From, query.Limit));
                Assert.Equal(["true"], query.Where[0].Values);
                Assert.Equal(("region", "north,south"), (query.Where[1].Field, string.Join(',', query.Where[1].Values)));
                Assert.Equal("routes", query.Where[2].HasBackref);
                Assert.Equal(("featured", "desc"), (query.Sort[0].Field, query.Sort[0].Direction));
                Assert.Equal(["food", "nature"], query.Sort[1].Values);
                Assert.Equal(["image"], query.Select);
            },
            query => Assert.Equal(("best_months", "1,2,3"), (query.Group!.Field, string.Join(',', query.Group.Values))),
            query => Assert.Equal("type", query.Distinct));
    }

    [Theory]
    [InlineData("q: { where: [] }", "site.queries.q.from is required.")]
    [InlineData("q: { from: city, limit: 0 }", "site.queries.q.limit must be at least 1")]
    [InlineData("q: { from: city, distinct: type, group: { field: m, values: [1] } }", "site.queries.q cannot use both distinct and group.")]
    [InlineData("q: { from: city, where: [{ field: a }] }", "site.queries.q.where[0] needs field with exactly one of eq or in, or hasBackref.")]
    [InlineData("q: { from: city, where: [{ field: a, eq: 1, in: [2] }] }", "site.queries.q.where[0] needs field")]
    [InlineData("q: { from: city, where: [{ hasBackref: r, field: a }] }", "site.queries.q.where[0] uses hasBackref")]
    [InlineData("q: { from: city, sort: [{ field: a, direction: up }] }", "site.queries.q.sort[0].direction must be asc or desc")]
    [InlineData("q: { from: city, group: { field: m } }", "site.queries.q.group.values must list at least one value.")]
    [InlineData("q: { from: city, select: ['bad name'] }", "site.queries.q.select 'bad name' must be a distinct field name")]
    [InlineData("q: { from: city, select: [url] }", "site.queries.q.select 'url' must be a distinct field name other than")]
    [InlineData("q: { from: city, select: [image, image] }", "site.queries.q.select 'image' must be a distinct field name")]
    [InlineData("'bad name': { from: city }", "site.queries.bad name must be named")]
    [InlineData("q: { from: city, top: 3 }", "Unknown config field 'site.queries.q.top'")]
    [InlineData("q: { from: city, where: [{ field: a, ne: 1 }] }", "Unknown config field 'site.queries.q.where[0].ne'")]
    public void Load_InvalidQuery_Throws(string query, string message)
    {
        var exception = Assert.Throws<ConfigException>(() => Load($$"""
              queries:
                {{query}}
            """));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("city", "routes", null)]
    [InlineData("city", "reviews", "site.queries.q.where[0].hasBackref 'reviews' is not a content.links backref on collection 'city'.")]
    [InlineData("route", "routes", "site.queries.q.where[0].hasBackref 'routes' is not a content.links backref on collection 'route'.")]
    public void Validate_HasBackref_MustNameABackrefOnTheCollection(string from, string backref, string? message)
    {
        var config = Load(
            $$"""
              queries:
                q: { from: {{from}}, where: [{ hasBackref: {{backref}} }] }
            """,
            """
              links:
                - { collection: route, field: cities, target: { collection: city }, backref: { name: routes } }
            """);

        var exception = Record.Exception(() => ConfigValidator.Validate(config));

        Assert.Equal(message, exception?.Message);
    }

    private static AppConfig Load(string siteYaml, string contentYaml = "")
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
            {{contentYaml}}
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-queries-{Guid.NewGuid():N}.yaml");
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
