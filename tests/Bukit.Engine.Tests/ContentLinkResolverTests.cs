using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.ContentLinks;
using Bukit.Engine.Incremental;
using Bukit.Engine.Stages;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentLinkResolverTests
{
    private static readonly ContentLinkConfig CityLink = new()
    {
        Collection = "route",
        Field = "cities",
        Target = new ContentLinkTargetConfig { Collection = "city" },
        Select = ["image"]
    };

    private static readonly ContentLinkConfig AttractionLink = new()
    {
        Collection = "route",
        Field = "days[].attractions",
        Target = new ContentLinkTargetConfig { Collection = "attraction", Key = "code" }
    };

    [Fact]
    public void Apply_TopLevelKeys_WritesLinksInOrderNextToTheField()
    {
        var route = Page("r1", "route", ("cities", new ContentField("list", new[] { "penang", "kl" })));

        var page = Single(Apply([CityLink], route, City("kl", "Kuala Lumpur"), City("penang", "Penang")));

        Assert.Equal(new[] { "penang", "kl" }, page.CustomFields!["cities"].Value);
        var links = Links(page, "cities_links");
        Assert.Equal(["penang", "kl"], links.Select(link => link["key"]));
        Assert.Equal(["collection", "image", "key", "slug", "title", "url"], links[0].Keys);
        Assert.Equal("Penang", links[0]["title"]);
        Assert.Equal("/city/penang/", links[0]["url"]);
        Assert.Equal("city", links[0]["collection"]);
        Assert.Equal("penang.jpg", links[0]["image"]);
    }

    [Fact]
    public void Apply_DataTarget_HasNoUrlAndSingleKeyIsAList()
    {
        var route = Page("r1", "route", ("cities", new ContentField("text", "kl")));
        var data = Document("kl", "city", ("sourceMode", new ContentField("text", "data")));

        var page = Single(ContentLinkResolver.Apply(Content(CityLink), [Routed(route)], [data]));

        var link = Assert.Single(Links(page, "cities_links"));
        Assert.Null(link["url"]);
        Assert.Null(link["image"]);
    }

    [Fact]
    public void Apply_NestedPath_WritesGroupedAndFlatLinksWithoutChangingTheSource()
    {
        var days = new List<object?>
        {
            new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["attractions"] = new List<object?> { "a1", "a2" }, ["title"] = "D1" },
            new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["attractions"] = new List<object?> { "a2", "missing" }, ["title"] = "D2" },
            new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "Rest day" }
        };
        var route = Page("r1", "route", ("days", new ContentField("list", days)));

        var page = Single(Apply([AttractionLink], route, Attraction("a1"), Attraction("a2")));

        var rebuilt = Assert.IsType<List<object?>>(page.CustomFields!["days"].Value);
        var first = Assert.IsType<SortedDictionary<string, object?>>(rebuilt[0]);
        Assert.Equal(["a1", "a2"], Keys(first["attractions_links"]));
        Assert.Equal(["a2"], Keys(((IDictionary<string, object?>)rebuilt[1]!)["attractions_links"]));
        Assert.Same(days[2], rebuilt[2]);
        Assert.Equal(["a1", "a2"], Links(page, "days_attractions_links").Select(link => link["key"]));
        Assert.False(((IDictionary<string, object?>)days[0]!).ContainsKey("attractions_links"));
    }

    [Fact]
    public void Apply_MarkdownMap_KeepsItsComparer()
    {
        var day = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["Attractions"] = new List<object?> { "a1" } };
        var route = Page("r1", "route", ("days", new ContentField("list", new List<object?> { day })));

        var page = Single(Apply([AttractionLink], route, Attraction("a1")));

        var rebuilt = Assert.IsType<Dictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(page.CustomFields!["days"].Value)));
        Assert.Same(StringComparer.OrdinalIgnoreCase, rebuilt.Comparer);
        Assert.Equal(["a1"], Keys(rebuilt["attractions_links"]));
    }

    [Fact]
    public void Apply_OtherCollectionsAndPagesWithoutTheField_AreUnchanged()
    {
        var other = Routed(Page("p1", "page", ("cities", new ContentField("list", new[] { "kl" }))));
        var empty = Routed(Page("r2", "route"));

        var result = ContentLinkResolver.Apply(Content(CityLink), [other, empty, Routed(City("kl", "KL"))], []);

        Assert.Same(other, result[0]);
        Assert.Same(empty, result[1]);
    }

    [Fact]
    public void Validate_ReportsEveryUnresolvedKeyInOneIssue()
    {
        var route = Page("r1", "route", ("cities", new ContentField("list", new[] { "kl", "ipoh", "melaka" })));

        var issue = Assert.Single(ContentLinkResolver.Validate(Config(CityLink), [route, City("kl", "KL")]));

        Assert.Equal(ContentLinkResolver.TargetUnresolvedCode, issue.Code);
        Assert.Equal("cities", issue.Field);
        Assert.Equal("r1", issue.SourcePath);
        Assert.Contains("'ipoh'", issue.Message, StringComparison.Ordinal);
        Assert.Contains("'melaka'", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_KeyRepeatedInOneLanguage_IsReportedOnTheLinkingPage()
    {
        var route = Page("r1", "route", ("cities", new ContentField("list", new[] { "kl", "penang", "ipoh", "perak" })));

        var issue = Assert.Single(ContentLinkResolver.Validate(
            Config(CityLink),
            [
                route,
                City("kl", "KL"),
                City("kl", "KL again", id: "kl-2"),
                City("penang", "Penang", language: "en"),
                City("penang", "Pulau Pinang", language: "ms", id: "penang-ms"),
                DataCity("ipoh-en", "ipoh", "en"),
                DataCity("ipoh-ms", "ipoh", "ms"),
                // A data document without a locale belongs to every language.
                DataCity("perak-all", "perak", null),
                DataCity("perak-en", "perak", "en")
            ]));

        Assert.Equal(ContentLinkResolver.TargetDuplicateCode, issue.Code);
        Assert.Equal("r1", issue.SourcePath);
        Assert.Contains("'kl'", issue.Message, StringComparison.Ordinal);
        Assert.Contains("'perak'", issue.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'penang'", issue.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'ipoh'", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EachLanguageChecksItsOwnTargets()
    {
        var config = Config(CityLink) with { Site = Config(CityLink).Site with { Languages = ["en", "ms"] } };
        var enRoute = Page("r-en", "route", ("cities", new ContentField("text", "kl")), ("language", new ContentField("text", "en")));
        var msRoute = Page("r-ms", "route", ("cities", new ContentField("text", "kl")), ("language", new ContentField("text", "ms")));

        var issue = Assert.Single(ContentLinkResolver.Validate(config, [enRoute, msRoute, City("kl", "KL", language: "en")]));

        Assert.Equal(ContentLinkResolver.TargetUnresolvedCode, issue.Code);
        Assert.Equal("r-ms", issue.SourcePath);
    }

    [Fact]
    public void Apply_NumberAndCaseVariantKeys_Resolve()
    {
        var link = AttractionLink with { Field = "codes" };
        var route = Page("r1", "route", ("codes", new ContentField("list", new[] { "101", "102", "A3" })));
        var notion = Document("n", "attraction", ("code", new ContentField("number", 101d)));
        var markdown = Document("m", "attraction", ("code", new ContentField("number", 102.0m)));
        var text = Document("t", "attraction", ("code", new ContentField("text", "a3")));
        var fraction = Document("f", "attraction", ("code", new ContentField("number", 101.5d)));

        var page = Single(Apply([link], route, notion, markdown, text, fraction));

        Assert.Equal(["n", "m", "t"], Links(page, "codes_links").Select(entry => entry["slug"]));
    }

    [Fact]
    public void Validate_NestedConflictIsReportedAndNullItemsAreSkipped()
    {
        var days = new List<object?>
        {
            null,
            new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["attractions"] = new List<object?> { "a1" }, ["attractions_links"] = "mine" }
        };
        var route = Page("r1", "route", ("days", new ContentField("list", days)));

        var issue = Assert.Single(ContentLinkResolver.Validate(Config(AttractionLink), [route, Attraction("a1")]));

        Assert.Equal(ContentLinkResolver.OutputConflictCode, issue.Code);
        var page = Single(Apply([AttractionLink], route, Attraction("a1")));
        var rebuilt = Assert.IsType<List<object?>>(page.CustomFields!["days"].Value);
        Assert.Null(rebuilt[0]);
        Assert.Equal("mine", ((IDictionary<string, object?>)rebuilt[1]!)["attractions_links"]);
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("number")]
    [InlineData("map")]
    public void Validate_ReportsInvalidValuesAndConflicts(string problem)
    {
        var route = problem switch
        {
            "conflict" => Page("r1", "route", ("cities", new ContentField("text", "kl")), ("cities_links", new ContentField("text", "mine"))),
            "number" => Page("r1", "route", ("cities", new ContentField("list", new List<object?> { 5L }))),
            _ => Page("r1", "route", ("cities", new ContentField("list", new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["a"] = "kl" })))
        };

        var issue = Assert.Single(ContentLinkResolver.Validate(Config(CityLink), [route, City("kl", "KL")]));

        Assert.Equal(problem == "conflict" ? ContentLinkResolver.OutputConflictCode : ContentLinkResolver.ValueInvalidCode, issue.Code);
    }

    [Fact]
    public void Apply_TargetTitleChange_ChangesTheLinkingPageHash()
    {
        var route = Page("r1", "route", ("cities", new ContentField("text", "kl")));

        var before = Single(Apply([CityLink], route, City("kl", "Kuala Lumpur")));
        var same = Single(Apply([CityLink], route, City("kl", "Kuala Lumpur")));
        var after = Single(Apply([CityLink], route, City("kl", "KL")));

        Assert.Equal(IncrementalBuildEngine.ComputeMetadataHash(before), IncrementalBuildEngine.ComputeMetadataHash(same));
        Assert.NotEqual(IncrementalBuildEngine.ComputeMetadataHash(before), IncrementalBuildEngine.ComputeMetadataHash(after));
    }

    [Fact]
    public void Apply_Backref_ListsEachLinkingPageOnceInCollectionOrderWithTheTotal()
    {
        var link = WithBackref(CityLink, 0, "region");

        var result = Apply(
            [link],
            Route("r1", 2020, "kl", "penang"),
            Route("r2", 2022, "kl", "KL"),
            Route("r3", 2021, "ipoh"),
            City("kl", "KL"),
            City("penang", "Penang"),
            City("melaka", "Melaka"));

        var kl = ById(result, "kl");
        var routes = Links(kl, "routes");
        Assert.Equal(["r2", "r1"], routes.Select(entry => entry["key"]));
        Assert.Equal(["collection", "key", "region", "slug", "title", "url"], routes[0].Keys);
        Assert.Equal("/route/r2/", routes[0]["url"]);
        Assert.Equal("route", routes[0]["collection"]);
        Assert.Equal("Route r2", routes[0]["title"]);
        Assert.Equal("region-r2", routes[0]["region"]);
        Assert.Equal(2L, kl.CustomFields!["routes_count"].Value);
        Assert.Equal(["r1"], Links(ById(result, "penang"), "routes").Select(entry => entry["key"]));
        Assert.Empty(Links(ById(result, "melaka"), "routes"));
        Assert.Equal(0L, ById(result, "melaka").CustomFields!["routes_count"].Value);
        Assert.False(ById(result, "r1").CustomFields!.ContainsKey("routes"));
    }

    [Fact]
    public void Apply_BackrefLimit_KeepsTheFirstEntriesAndTheFullCount()
    {
        var result = Apply(
            [WithBackref(CityLink, limit: 1)],
            Route("r1", 2020, "kl"),
            Route("r2", 2022, "kl"),
            City("kl", "KL"));

        var kl = ById(result, "kl");
        Assert.Equal(["r2"], Links(kl, "routes").Select(entry => entry["key"]));
        Assert.Equal(2L, kl.CustomFields!["routes_count"].Value);
    }

    [Fact]
    public void Apply_BackrefSameTimes_UsesTheOrderingTieBreak()
    {
        var content = Content(WithBackref(CityLink)) with { OrderingTieBreakField = "region", OrderingTieBreakOrder = "desc" };

        var result = ContentLinkResolver.Apply(content, [.. new[] { Route("a", 2020, "kl"), Route("b", 2020, "kl"), City("kl", "KL") }.Select(Routed)], []);

        Assert.Equal(["b", "a"], Links(ById(result, "kl"), "routes").Select(entry => entry["key"]));
    }

    [Fact]
    public void Apply_BackrefFromNestedPath_ListsTheRouteOnce()
    {
        var day = (string code) => new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["attractions"] = new List<object?> { code } };
        var route = Page("r1", "route", ("days", new ContentField("list", new List<object?> { day("a1"), day("a1") })));
        var link = AttractionLink with { Backref = new ContentLinkBackrefConfig { Name = "routes" } };

        var result = Apply([link], route, Attraction("a1"));

        Assert.Equal(["r1"], Links(ById(result, "attraction-a1"), "routes").Select(entry => entry["key"]));
    }

    [Fact]
    public void Apply_Backref_SkipsDataTargetsAndRepeatedKeys()
    {
        var result = ContentLinkResolver.Apply(
            Content(WithBackref(CityLink)),
            [Routed(Route("r1", 2020, "kl", "penang")), Routed(City("kl", "KL", id: "kl-1")), Routed(City("kl", "KL again", id: "kl-2"))],
            [Document("penang", "city", ("sourceMode", new ContentField("text", "data")))]);

        Assert.Single(Links(ById(result, "kl-1"), "routes"));
        Assert.Empty(Links(ById(result, "kl-2"), "routes"));
    }

    [Fact]
    public void Apply_BackrefOnItsOwnCollection_ListsOnlyForwardFields()
    {
        var related = new ContentLinkConfig
        {
            Collection = "post",
            Field = "related",
            Target = new ContentLinkTargetConfig { Collection = "post" },
            Backref = new ContentLinkBackrefConfig { Name = "related_from", Select = ["related_from", "related_links"] }
        };
        var p1 = Page("p1", "post", ("related", new ContentField("text", "p2")));
        var p2 = Page("p2", "post", ("related", new ContentField("text", "p1")));

        var result = Apply([related], p1, p2);

        var entry = Assert.Single(Links(ById(result, "p1"), "related_from"));
        Assert.Equal("p2", entry["key"]);
        Assert.Null(entry["related_from"]);
        Assert.NotNull(entry["related_links"]);
    }

    [Fact]
    public void Apply_LinkWhoseOwnOutputConflicts_StillCountsForTheBackref()
    {
        var route = Page("r1", "route", ("cities", new ContentField("text", "kl")), ("cities_links", new ContentField("text", "mine")));

        var result = Apply([WithBackref(CityLink)], route, City("kl", "KL"));

        Assert.Equal("mine", ById(result, "r1").CustomFields!["cities_links"].Value);
        Assert.Equal(["r1"], Links(ById(result, "kl"), "routes").Select(entry => entry["key"]));
    }

    [Fact]
    public void Apply_TwoBackrefsOnOneCollection_AreWrittenIndependently()
    {
        var guideLink = new ContentLinkConfig
        {
            Collection = "guide",
            Field = "cities",
            Target = new ContentLinkTargetConfig { Collection = "city" },
            Backref = new ContentLinkBackrefConfig { Name = "guides" }
        };
        var guide = Page("g1", "guide", ("cities", new ContentField("text", "kl")));

        var result = Apply([WithBackref(CityLink), guideLink], Route("r1", 2020, "kl"), Route("r2", 2021, "kl"), guide, City("kl", "KL"));

        var kl = ById(result, "kl");
        Assert.Equal(["r2", "r1"], Links(kl, "routes").Select(entry => entry["key"]));
        Assert.Equal(["g1"], Links(kl, "guides").Select(entry => entry["key"]));
        Assert.Equal(2L, kl.CustomFields!["routes_count"].Value);
        Assert.Equal(1L, kl.CustomFields["guides_count"].Value);
    }

    [Fact]
    public void RoutePipeline_BackrefCountsOnlyPagesOfTheBuiltLanguage()
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
                    ["city"] = new() { Permalink = "/cities/{slug}/", Template = "city.html" }
                }
            },
            Content = TestContent.Markdown() with { Links = [WithBackref(CityLink)] }
        };
        var enRoute = Page("r-en", "route", ("cities", new ContentField("text", "kl")), ("language", new ContentField("text", "en")));
        var msRoute = Page("r-ms", "route", ("cities", new ContentField("text", "kl")), ("language", new ContentField("text", "ms")));

        var result = new RoutePipeline().Execute(config, [enRoute, msRoute, City("kl", "KL")]);

        var city = Assert.Single(result.RoutedDocuments, page => page.Document.Id == "kl");
        Assert.Equal(["r-en"], Links(city.Document, "routes").Select(entry => entry["key"]));
    }

    [Fact]
    public void Backref_ExistingField_IsKeptAndReportedOnTheTargetPage()
    {
        var link = WithBackref(CityLink);
        var city = Document("kl", "city", ("routes", new ContentField("text", "mine")));
        var route = Route("r1", 2020, "kl");

        var applied = ById(Apply([link], route, city), "kl");
        var issue = Assert.Single(ContentLinkResolver.Validate(Config(link), [route, city]));

        Assert.Equal("mine", applied.CustomFields!["routes"].Value);
        Assert.False(applied.CustomFields.ContainsKey("routes_count"));
        Assert.Equal(ContentLinkResolver.OutputConflictCode, issue.Code);
        Assert.Equal("routes", issue.Field);
        Assert.Equal("kl", issue.SourcePath);
    }

    [Fact]
    public void Apply_ReferrerTitleChange_ChangesTheTargetPageHash()
    {
        var link = WithBackref(CityLink);
        string Hash(string title)
        {
            var route = ContentDocument.Create("r1", title, "r1", DateTimeOffset.UnixEpoch, null, Fields("route", [("cities", new ContentField("text", "kl"))]));
            return IncrementalBuildEngine.ComputeMetadataHash(ById(Apply([link], route, City("kl", "KL")), "kl"));
        }

        Assert.Equal(Hash("One"), Hash("One"));
        Assert.NotEqual(Hash("One"), Hash("Two"));
    }

    [Fact]
    public void RoutePipeline_ResolvesLinksBeforeListRoutes()
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
                    ["route"] = new() { Permalink = "/routes/{slug}/", Template = "route.html", ListRoute = "/routes/", ListTemplate = "routes.html" },
                    ["city"] = new() { Permalink = "/cities/{slug}/", Template = "city.html" }
                }
            },
            Content = TestContent.Markdown() with { Links = [WithBackref(CityLink)] }
        };
        var route = Page("r1", "route", ("cities", new ContentField("text", "kl")));

        var result = new RoutePipeline().Execute(config, [route, City("kl", "KL")]);

        var routed = Assert.Single(result.RoutedDocuments, page => page.Document.Id == "r1");
        Assert.Equal("/cities/kl/", Assert.Single(Links(routed.Document, "cities_links"))["url"]);
        var city = Assert.Single(result.RoutedDocuments, page => page.Document.Id == "kl");
        Assert.Equal("/routes/r1/", Assert.Single(Links(city.Document, "routes"))["url"]);
        var list = Assert.Single(result.ListRouteGraph.Routes, listRoute => listRoute.Url == "/routes/");
        Assert.True(Assert.Single(list.Items).Fields!.ContainsKey("cities_links"));
    }

    [Fact]
    public async Task ValidateStage_StrictCollection_BlocksOnUnresolvedKey()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["route"] = new() { Permalink = "/routes/{slug}/", SchemaFailMode = "strict" }
                }
            },
            Content = TestContent.Markdown() with { Links = [CityLink] },
            Build = new BuildConfig { SchemaFailMode = "warn" }
        };
        var input = new ContentStageInput(
            [Page("r1", "route", ("cities", new ContentField("text", "ipoh")))],
            EmptyContentBodyStore.Instance,
            config,
            new ConfigOverrides(),
            "/root",
            "/cache",
            new SilentLogger());

        var exception = await Assert.ThrowsAsync<ConfigException>(() => new ContentGraphValidateStage().ExecuteAsync(input, CancellationToken.None));

        Assert.Equal(DiagnosticCode.SchemaStrictModeBlocked, exception.Code);
    }

    private static IReadOnlyList<RoutedContentDocument> Apply(IReadOnlyList<ContentLinkConfig> links, params ContentDocument[] documents)
        => ContentLinkResolver.Apply(Content([.. links]), [.. documents.Select(Routed)], []);

    private static ContentConfig Content(params ContentLinkConfig[] links) => TestContent.Markdown() with { Links = links };

    private static ContentLinkConfig WithBackref(ContentLinkConfig link, int limit = 0, params string[] select)
        => link with { Backref = new ContentLinkBackrefConfig { Name = "routes", Limit = limit, Select = select } };

    private static ContentDocument Route(string id, int year, params string[] cities)
        => ContentDocument.Create(
            id,
            $"Route {id}",
            id,
            new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            Fields("route", [("cities", new ContentField("list", cities)), ("region", new ContentField("text", $"region-{id}"))]));

    private static ContentDocument ById(IReadOnlyList<RoutedContentDocument> result, string id)
        => Assert.Single(result, page => page.Document.Id == id).Document;

    private static ContentDocument Single(IReadOnlyList<RoutedContentDocument> result)
        => Assert.Single(result, page => page.Document.Id == "r1").Document;

    private static List<SortedDictionary<string, object?>> Links(ContentDocument page, string field)
    {
        var value = page.CustomFields![field];
        Assert.Equal("list", value.Type);
        return [.. Assert.IsType<List<object?>>(value.Value).Cast<SortedDictionary<string, object?>>()];
    }

    private static IEnumerable<object?> Keys(object? links)
        => Assert.IsType<List<object?>>(links).Cast<SortedDictionary<string, object?>>().Select(link => link["key"]);

    private static RoutedContentDocument Routed(ContentDocument document)
    {
        var url = $"/{document.Record.Classification.Collection}/{document.Slug}/";
        return new RoutedContentDocument(document, new RouteInfo(url, url.Trim('/') + "/index.html", "page.html"));
    }

    private static ContentDocument City(string slug, string title, string language = "en", string? id = null)
        => ContentDocument.Create(
            id ?? slug,
            title,
            slug,
            DateTimeOffset.UnixEpoch,
            null,
            Fields("city", [("image", new ContentField("text", $"{slug}.jpg")), ("language", new ContentField("text", language))]));

    private static ContentDocument DataCity(string id, string slug, string? locale)
    {
        var fields = Fields("city", [("sourceMode", new ContentField("text", "data"))]);
        if (locale is not null)
        {
            fields["locale"] = new ContentField("text", locale);
        }

        return ContentDocument.Create(id, slug, slug, DateTimeOffset.UnixEpoch, null, fields);
    }

    private static AppConfig Config(params ContentLinkConfig[] links) => new()
    {
        Site = new SiteConfig { Name = "test", Title = "Test", Language = "en" },
        Content = TestContent.Markdown() with { Links = links }
    };

    private static ContentDocument Attraction(string code)
        => Document($"attraction-{code}", "attraction", ("code", new ContentField("text", code)));

    private static ContentDocument Page(string id, string collection, params (string Name, ContentField Field)[] fields)
        => Document(id, collection, fields);

    private static ContentDocument Document(string id, string collection, params (string Name, ContentField Field)[] fields)
        => ContentDocument.Create(id, id, id, DateTimeOffset.UnixEpoch, null, Fields(collection, fields));

    private static Dictionary<string, ContentField> Fields(string collection, (string Name, ContentField Field)[] fields)
    {
        var map = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = new("text", collection)
        };
        foreach (var (name, field) in fields)
        {
            map[name] = field;
        }

        return map;
    }

    private sealed class SilentLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
