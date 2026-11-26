using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.ContentLinks;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class CollectionPagePolicyTests
{
    private static readonly ContentLinkConfig CityLink = new()
    {
        Collection = "route",
        Field = "cities",
        Target = new ContentLinkTargetConfig { Collection = "city" },
        Backref = new ContentLinkBackrefConfig { Name = "routes" }
    };

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData("TRUE", true)]
    [InlineData("yes", false)]
    [InlineData(1L, false)]
    [InlineData(null, false)]
    public void HasPage_FollowsRouteWhen(object? value, bool expected)
    {
        var site = Site(new CollectionRouteWhenConfig { Field = "has_page", Eq = "true" });

        Assert.Equal(expected, CollectionPagePolicy.HasPage(City("kl", value), site));
    }

    [Fact]
    public void HasPage_WholeNumberValue_MatchesItsDigits()
    {
        var site = Site(new CollectionRouteWhenConfig { Field = "has_page", Eq = "1" });

        Assert.True(CollectionPagePolicy.HasPage(City("kl", 1d), site));
        Assert.True(CollectionPagePolicy.HasPage(City("kl", 1L), site));
        Assert.True(CollectionPagePolicy.HasPage(City("kl", 1.0m), site));
        Assert.False(CollectionPagePolicy.HasPage(City("kl", 1.5m), site));
    }

    [Fact]
    public void HasPage_WithoutRouteWhenOrForData_IsUnchanged()
    {
        var site = Site(routeWhen: null);

        Assert.True(CollectionPagePolicy.HasPage(City("kl", false), site));
        Assert.False(CollectionPagePolicy.HasPage(Document("d1", "city", ("sourceMode", new ContentField("text", "data"))), site));
    }

    [Fact]
    public void RoutePipeline_EntryFailingRouteWhen_HasNoPageButStaysALinkTarget()
    {
        var config = new AppConfig
        {
            Site = Site(new CollectionRouteWhenConfig { Field = "has_page", Eq = "true" }),
            Content = TestContent.Markdown() with { Links = [CityLink] }
        };
        var route = Document("r1", "route", ("cities", new ContentField("list", new[] { "kl", "ipoh" })));

        var result = new RoutePipeline().Execute(config, [route, City("kl", true), City("ipoh", false)]);

        Assert.Equal(["kl", "r1"], result.RoutedDocuments.Select(page => page.Document.Id).Order(StringComparer.Ordinal));
        var links = Assert.IsType<List<object?>>(result.RoutedDocuments.Single(page => page.Document.Id == "r1").Document.CustomFields!["cities_links"].Value)
            .Cast<SortedDictionary<string, object?>>()
            .ToList();
        Assert.Equal("/cities/kl/", links[0]["url"]);
        Assert.Equal("ipoh", links[1]["key"]);
        Assert.Null(links[1]["url"]);
        var list = Assert.Single(result.ListRouteGraph.Routes, listRoute => listRoute.Url == "/cities/");
        Assert.Equal("kl", Assert.Single(list.Items).Id);
    }

    [Fact]
    public void ContentLinkValidate_SkipsEntriesWithoutAPage()
    {
        var config = new AppConfig
        {
            Site = Site(new CollectionRouteWhenConfig { Field = "has_page", Eq = "true" }),
            Content = TestContent.Markdown() with
            {
                Links = [new ContentLinkConfig { Collection = "city", Field = "nearby", Target = new ContentLinkTargetConfig { Collection = "city" } }]
            }
        };
        var hidden = Document("ipoh", "city", ("has_page", new ContentField("boolean", false)), ("nearby", new ContentField("text", "missing")));

        Assert.Empty(ContentLinkResolver.Validate(config, [hidden]));
    }

    private static SiteConfig Site(CollectionRouteWhenConfig? routeWhen) => new()
    {
        Name = "test",
        Title = "Test",
        Language = "en",
        Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["route"] = new() { Permalink = "/routes/{slug}/", Template = "route.html" },
            ["city"] = new() { Permalink = "/cities/{slug}/", Template = "city.html", ListRoute = "/cities/", ListTemplate = "cities.html", RouteWhen = routeWhen }
        }
    };

    private static ContentDocument City(string slug, object? hasPage)
        => hasPage is null
            ? Document(slug, "city")
            : Document(slug, "city", ("has_page", new ContentField(hasPage is bool ? "boolean" : "text", hasPage)));

    private static ContentDocument Document(string id, string collection, params (string Name, ContentField Field)[] fields)
    {
        var map = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = new("text", collection)
        };
        foreach (var (name, field) in fields)
        {
            map[name] = field;
        }

        return ContentDocument.Create(id, id, id, DateTimeOffset.UnixEpoch, null, map);
    }
}
