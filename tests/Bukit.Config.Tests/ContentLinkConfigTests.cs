using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ContentLinkConfigTests
{
    [Fact]
    public void Load_Links_ParsesEntriesAndDefaults()
    {
        var config = Load("""
              links:
                - collection: route
                  field: cities
                  target: { collection: city }
                - collection: route
                  field: days[].attractions
                  target: { collection: attraction, key: code }
                  select: [image, summary]
            """);

        Assert.Collection(
            config.Content.Links,
            link =>
            {
                Assert.Equal("route", link.Collection);
                Assert.Equal("cities", link.Field);
                Assert.Equal("city", link.Target.Collection);
                Assert.Equal("slug", link.Target.Key);
                Assert.Empty(link.Select);
            },
            link =>
            {
                Assert.Equal("days[].attractions", link.Field);
                Assert.Equal("code", link.Target.Key);
                Assert.Equal(["image", "summary"], link.Select);
            });
    }

    [Fact]
    public void Load_WithoutLinks_IsEmpty()
        => Assert.Empty(Load(string.Empty).Content.Links);

    [Theory]
    [InlineData("field: cities", "content.links[0].target is required.")]
    [InlineData("target: { collection: city }", "content.links[0].field is required.")]
    [InlineData("field: cities\n      target: { key: slug }", "content.links[0].target.collection is required.")]
    public void Load_LinkMissingRequiredField_Throws(string entry, string message)
    {
        var exception = Assert.Throws<ConfigException>(() => Load($"""
              links:
                - collection: route
                  {entry}
            """));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, exception.Code);
        Assert.Equal(message, exception.Message);
    }

    [Theory]
    [InlineData("field: days[]", "target: { collection: city }", "is not a valid path")]
    [InlineData("field: days..stops", "target: { collection: city }", "is not a valid path")]
    [InlineData("field: a.b.c.d.e.f.g.h.i", "target: { collection: city }", "is not a valid path")]
    [InlineData("field: 'cities'", "target: { collection: city, key: 'bad key' }", "target.key 'bad key'")]
    [InlineData("field: cities", "target: { collection: city }\n      select: [url]", "select 'url'")]
    [InlineData("field: cities", "target: { collection: city }\n      select: [image, IMAGE]", "select 'IMAGE'")]
    public void Load_InvalidLink_Throws(string field, string rest, string message)
    {
        var exception = Assert.Throws<ConfigException>(() => LoadAndValidate($"""
              links:
                - collection: route
                  {field}
                  {rest}
            """));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_TwoLinksWritingOneField_Throws()
    {
        var exception = Assert.Throws<ConfigException>(() => LoadAndValidate("""
              links:
                - collection: route
                  field: cities
                  target: { collection: city }
                - collection: ROUTE
                  field: cities
                  target: { collection: town }
            """));

        Assert.Contains("writes 'cities_links' in collection 'ROUTE'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("target: { collection: city }\n      unknown: 1", "content.links[0].unknown")]
    [InlineData("target: { collection: city, extra: 1 }", "content.links[0].target.extra")]
    public void Load_UnknownLinkField_Throws(string rest, string path)
    {
        var exception = Assert.Throws<ConfigException>(() => Load($"""
              links:
                - collection: route
                  field: cities
                  {rest}
            """));

        Assert.Contains($"Unknown config field '{path}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cities", false, "cities_links", "cities_links")]
    [InlineData("days[].attractions", true, "attractions_links", "days_attractions_links")]
    [InlineData("trip.days[].stops", true, "stops_links", "trip_days_stops_links")]
    public void ContentLinkPath_NamesOutputs(string text, bool nested, string grouped, string flat)
    {
        Assert.True(ContentLinkPath.TryParse(text, out var path));
        Assert.Equal(nested, path.IsNested);
        Assert.Equal(grouped, path.GroupedName);
        Assert.Equal(flat, path.FlatName);
    }

    private static void LoadAndValidate(string linksYaml) => ConfigValidator.Validate(Load(linksYaml));

    private static AppConfig Load(string linksYaml)
    {
        var yaml = $$"""
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            {{linksYaml}}
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-content-links-{Guid.NewGuid():N}.yaml");
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
