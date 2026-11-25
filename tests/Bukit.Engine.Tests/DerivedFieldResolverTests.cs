using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.DerivedFields;
using Bukit.Engine.Incremental;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DerivedFieldResolverTests
{
    [Fact]
    public void Apply_DefaultFillsOnlyEmptyFieldsAndOverrideAlwaysWrites()
    {
        var content = Content(
            Rule("seo_title", "{{ name }} guide"),
            Rule("summary", "{{ name }}: {{ summary }}", "override"),
            Rule("seo_desc", "{{ summary | seo.describe 12 }}"));
        var city = Page("kl", "city", ("name", "KL"), ("summary", "Capital city"), ("seo_title", "Mine"));

        var page = Single(DerivedFieldResolver.Apply(content, [Routed(city)]));

        Assert.Equal("Mine", page.CustomFields!["seo_title"].Value);
        Assert.Equal("KL: Capital city", page.CustomFields["summary"].Value);
        Assert.Equal("KL: Capital…", page.CustomFields["seo_desc"].Value);
        Assert.Equal("KL: Capital city", page.Record.Presentation.Summary);
    }

    [Fact]
    public void Apply_OtherCollectionsAndEmptyDefaultResults_AreUnchanged()
    {
        var content = Content(Rule("seo_title", "{{ missing }}"));
        var city = Routed(Page("kl", "city", ("name", "KL")));
        var post = Routed(Page("p1", "post", ("name", "Post")));

        var result = DerivedFieldResolver.Apply(content, [city, post]);

        Assert.Same(city, result[0]);
        Assert.Same(post, result[1]);
    }

    [Fact]
    public void Validate_RuntimeErrorIsReportedOnThePage()
    {
        var config = Config(Rule("seo_title", "{{ for i in 1..5000 }}x{{ end }}"));

        var issue = Assert.Single(DerivedFieldResolver.Validate(config, [Page("kl", "city", ("name", "KL"))]));

        Assert.Equal(DerivedFieldResolver.FailedCode, issue.Code);
        Assert.Equal("seo_title", issue.Field);
        Assert.Equal("kl", issue.SourcePath);
    }

    [Fact]
    public void Validate_TemplateThatDoesNotParse_Throws()
    {
        var exception = Assert.Throws<ConfigException>(() => DerivedFieldResolver.Validate(Config(Rule("seo_title", "{{ if }}")), [Page("kl", "city")]).ToList());

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("content.derivedFields.city.seo_title.value does not parse", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_InputChange_ChangesThePageHash()
    {
        var content = Content(Rule("seo_title", "{{ name }} guide", "override"));
        string Hash(string name) => IncrementalBuildEngine.ComputeMetadataHash(Single(DerivedFieldResolver.Apply(content, [Routed(Page("kl", "city", ("name", name)))])));

        Assert.Equal(Hash("KL"), Hash("KL"));
        Assert.NotEqual(Hash("KL"), Hash("Kuala Lumpur"));
    }

    [Fact]
    public void RoutePipeline_DerivedValuesCanReadLinksAndReachListRoutes()
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
            Content = TestContent.Markdown() with
            {
                Links = [new ContentLinkConfig { Collection = "route", Field = "cities", Target = new ContentLinkTargetConfig { Collection = "city" } }],
                DerivedFields = [new ContentDerivedFieldConfig { Collection = "route", Field = "seo_title", Value = "Via {{ cities_links[0].title }}" }]
            }
        };

        var result = new RoutePipeline().Execute(config, [Page("r1", "route", ("cities", "kl")), Page("kl", "city")]);

        var route = Assert.Single(result.RoutedDocuments, page => page.Document.Id == "r1").Document;
        Assert.Equal("Via kl", route.CustomFields!["seo_title"].Value);
        var list = Assert.Single(result.ListRouteGraph.Routes, listRoute => listRoute.Url == "/routes/");
        Assert.Equal("Via kl", Assert.Single(list.Items).Fields!["seo_title"].Value);
    }

    private static ContentDerivedFieldConfig Rule(string field, string value, string mode = "default")
        => new() { Collection = "city", Field = field, Value = value, Mode = mode };

    private static ContentConfig Content(params ContentDerivedFieldConfig[] rules) => TestContent.Markdown() with { DerivedFields = rules };

    private static AppConfig Config(params ContentDerivedFieldConfig[] rules) => new()
    {
        Site = new SiteConfig { Name = "test", Title = "Test", Language = "en" },
        Content = Content(rules)
    };

    private static ContentDocument Single(IReadOnlyList<RoutedContentDocument> result) => Assert.Single(result).Document;

    private static RoutedContentDocument Routed(ContentDocument document)
    {
        var url = $"/{document.Record.Classification.Collection}/{document.Slug}/";
        return new RoutedContentDocument(document, new RouteInfo(url, url.Trim('/') + "/index.html", "page.html"));
    }

    private static ContentDocument Page(string id, string collection, params (string Name, string Value)[] fields)
    {
        var map = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase) { ["collection"] = new("text", collection) };
        foreach (var (name, value) in fields)
        {
            map[name] = new ContentField("text", value);
        }

        return ContentDocument.Create(id, id, id, DateTimeOffset.UnixEpoch, null, map);
    }
}
