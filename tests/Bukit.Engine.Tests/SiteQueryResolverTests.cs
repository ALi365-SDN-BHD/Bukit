using System.Collections;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Queries;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SiteQueryResolverTests
{
    [Fact]
    public void Resolve_NoQueries_ReturnsNull()
        => Assert.Null(SiteQueryResolver.Resolve(Config(), [], []));

    [Fact]
    public void Resolve_FiltersOneCollectionInListOrderAndKeepsEntriesWithoutAPage()
    {
        var pages = new[]
        {
            Routed(Doc("kl", "city", 3, ("region", "central"))),
            Routed(Doc("post", "post", 5, ("region", "central"))),
            Routed(Doc("pg", "city", 1, ("region", "north")))
        };
        var hidden = Doc("ipoh", "city", 2, ("region", "Central"));

        var result = Query(Config(new SiteQueryConfig
        {
            Name = "central",
            From = "city",
            Where = [new SiteQueryFilterConfig { Field = "region", Values = ["central"] }],
            Select = ["region"]
        }), pages, [hidden], "central");

        Assert.Equal(["kl", "ipoh"], Keys(result));
        Assert.Equal("/city/kl/", Item(result, 0)["url"]);
        Assert.Null(Item(result, 1)["url"]);
        Assert.Equal("Central", Item(result, 1)["region"]);
    }

    [Fact]
    public void Resolve_ListFieldMatchesAnyItemAndHasBackrefNeedsALink()
    {
        var pages = new[]
        {
            Routed(Doc("a", "city", 3, ("months", new List<object?> { 1L, 3L }), ("routes_count", 2L))),
            Routed(Doc("b", "city", 2, ("months", new List<object?> { 3L }), ("routes_count", 0L))),
            Routed(Doc("c", "city", 1, ("months", new List<object?> { 4L })))
        };

        var result = Query(Config(new SiteQueryConfig
        {
            Name = "q",
            From = "city",
            Where = [new SiteQueryFilterConfig { Field = "months", Values = ["3"] }, new SiteQueryFilterConfig { HasBackref = "routes" }]
        }), pages, [], "q");

        Assert.Equal(["a"], Keys(result));
    }

    [Fact]
    public void Resolve_SortsStablyWithMissingValuesLastAndByListedValues()
    {
        var pages = new[]
        {
            Routed(Doc("a", "route", 5, ("featured", 1L), ("kind", "nature"))),
            Routed(Doc("b", "route", 4, ("kind", "food"))),
            Routed(Doc("c", "route", 3, ("featured", 2L), ("kind", "other"))),
            Routed(Doc("d", "route", 2, ("featured", 1L), ("kind", "food")))
        };

        var byFeatured = Query(Config(new SiteQueryConfig
        {
            Name = "q",
            From = "route",
            Sort = [new SiteQuerySortConfig { Field = "featured", Direction = "desc" }],
            Limit = 3
        }), pages, [], "q");
        var byKind = Query(Config(new SiteQueryConfig
        {
            Name = "q",
            From = "route",
            Sort = [new SiteQuerySortConfig { Field = "kind", Values = ["food", "nature"] }]
        }), pages, [], "q");

        Assert.Equal(["c", "a", "d"], Keys(byFeatured));
        Assert.Equal(["b", "d", "a", "c"], Keys(byKind));
    }

    [Fact]
    public void Resolve_MixedValuesSortNumbersAndNumericTextBeforeOtherText()
    {
        var pages = new[]
        {
            Routed(Doc("a", "route", 4, ("rank", 10L))),
            Routed(Doc("b", "route", 3, ("rank", "9"))),
            Routed(Doc("c", "route", 2, ("rank", "1a"))),
            Routed(Doc("d", "route", 1, ("rank", "2")))
        };

        var sorted = Query(Config(new SiteQueryConfig
        {
            Name = "q",
            From = "route",
            Sort = [new SiteQuerySortConfig { Field = "rank" }]
        }), pages, [], "q");

        Assert.Equal(["d", "b", "a", "c"], Keys(sorted));
    }

    [Fact]
    public void Resolve_LargeAndPreciseNumbersSortExactly()
    {
        var pages = new[]
        {
            Routed(Doc("a", "route", 5, ("rank", 9007199254740992L))),
            Routed(Doc("b", "route", 4, ("rank", 9007199254740993L))),
            Routed(Doc("c", "route", 3, ("rank", "0.10000000000000000001"))),
            Routed(Doc("d", "route", 2, ("rank", "0.1"))),
            Routed(Doc("e", "route", 1, ("rank", "123456789012345678901234567890123")))
        };

        var sorted = Query(Config(new SiteQueryConfig
        {
            Name = "q",
            From = "route",
            Sort = [new SiteQuerySortConfig { Field = "rank", Direction = "desc" }]
        }), pages, [], "q");

        Assert.Equal(["e", "b", "a", "c", "d"], Keys(sorted));
    }

    [Fact]
    public void Resolve_DistinctFlattensListsAndKeepsFirstSpelling()
    {
        var pages = new[]
        {
            Routed(Doc("a", "attraction", 3, ("type", new List<object?> { "Beach", "park" }))),
            Routed(Doc("b", "attraction", 2, ("type", "beach"))),
            Routed(Doc("c", "attraction", 1, ("type", "museum")))
        };

        var result = Query(Config(new SiteQueryConfig { Name = "q", From = "attraction", Distinct = "type", Limit = 3 }), pages, [], "q");

        Assert.Equal(["Beach", "park", "museum"], result.Cast<string>());
    }

    [Fact]
    public void Resolve_GroupsByListedValuesWithAPerGroupLimit()
    {
        var pages = new[]
        {
            Routed(Doc("a", "route", 3, ("months", new List<object?> { 1L, 2L }))),
            Routed(Doc("b", "route", 2, ("months", new List<object?> { 1L }))),
            Routed(Doc("c", "route", 1, ("months", new List<object?> { 1L })))
        };

        var result = Query(Config(new SiteQueryConfig
        {
            Name = "q",
            From = "route",
            Group = new SiteQueryGroupConfig { Field = "months", Values = ["1", "2", "3"] },
            Limit = 2
        }), pages, [], "q");

        var groups = result.Cast<IDictionary<string, object?>>().ToList();
        Assert.Equal(["1", "2", "3"], groups.Select(group => group["value"]));
        Assert.Equal(["a", "b"], Keys((IList)groups[0]["items"]!));
        Assert.Equal(["a"], Keys((IList)groups[1]["items"]!));
        Assert.Empty((IList)groups[2]["items"]!);
    }

    [Fact]
    public void RoutePipeline_QueriesSeeBackrefsAndEntriesWithoutAPage()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Language = "en",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["route"] = new() { Permalink = "/routes/{slug}/", Template = "route.html" },
                    ["city"] = new()
                    {
                        Permalink = "/cities/{slug}/",
                        Template = "city.html",
                        RouteWhen = new CollectionRouteWhenConfig { Field = "has_page", Eq = "true" }
                    }
                },
                Queries =
                [
                    new SiteQueryConfig { Name = "route_cities", From = "city", Where = [new SiteQueryFilterConfig { HasBackref = "routes" }] },
                    new SiteQueryConfig { Name = "all_cities", From = "city" }
                ]
            },
            Content = TestContent.Markdown() with
            {
                Links =
                [
                    new ContentLinkConfig
                    {
                        Collection = "route",
                        Field = "cities",
                        Target = new ContentLinkTargetConfig { Collection = "city" },
                        Backref = new ContentLinkBackrefConfig { Name = "routes" }
                    }
                ]
            }
        };

        var result = new RoutePipeline().Execute(config,
        [
            Doc("r1", "route", 3, ("cities", "kl")),
            Doc("kl", "city", 2, ("has_page", "true")),
            Doc("pg", "city", 1, ("has_page", "true")),
            Doc("ipoh", "city", 0, ("has_page", "false"))
        ]);

        Assert.Equal(["kl"], Keys((IList)result.Queries!["route_cities"]));
        Assert.Equal(["kl", "pg", "ipoh"], Keys((IList)result.Queries["all_cities"]));
    }

    private static IList Query(AppConfig config, IReadOnlyList<RoutedContentDocument> pages, IReadOnlyList<ContentDocument> withoutPage, string name)
        => (IList)SiteQueryResolver.Resolve(config, pages, withoutPage)![name];

    private static IDictionary<string, object?> Item(IList items, int index) => (IDictionary<string, object?>)items[index]!;

    private static string[] Keys(IList items) => [.. items.Cast<IDictionary<string, object?>>().Select(item => (string)item["key"]!)];

    private static AppConfig Config(params SiteQueryConfig[] queries) => new()
    {
        Site = new SiteConfig { Name = "test", Title = "Test", Language = "en", Queries = queries },
        Content = TestContent.Markdown()
    };

    private static RoutedContentDocument Routed(ContentDocument document)
    {
        var url = $"/{document.Record.Classification.Collection}/{document.Slug}/";
        return new RoutedContentDocument(document, new RouteInfo(url, url.Trim('/') + "/index.html", "page.html"));
    }

    private static ContentDocument Doc(string id, string collection, int day, params (string Name, object? Value)[] fields)
    {
        var map = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase) { ["collection"] = new("text", collection) };
        foreach (var (name, value) in fields)
        {
            map[name] = new ContentField(value is string ? "text" : "list", value);
        }

        return ContentDocument.Create(id, id, id, DateTimeOffset.UnixEpoch.AddDays(day), null, map);
    }
}
