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
    public void ProjectRecords_CopiesDerivedRecordsIntoTheContentGraph()
    {
        var content = Content(
            Rule("summary", "{{ name }}: {{ summary }}", "override"),
            Rule("tags", "new", "override"));
        var city = Page("kl", "city", ("name", "KL"), ("summary", "Capital city"), ("tags", new ContentField("list", new List<string> { "old" })));
        var post = Page("p1", "post", ("summary", "Post summary"));
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([city, post]);
        RoutedContentDocument[] before = [Routed(city), Routed(post)];

        var routed = DerivedFieldResolver.Apply(content, before);
        var derived = Single(routed.Take(1).ToList());
        var projected = DerivedFieldResolver.ProjectRecords(graph, routed, DerivedFieldResolver.DerivedKeys(before, routed));

        Assert.Equal(["new"], derived.Record.Classification.Tags);
        Assert.Equal("kl", derived.Record.Identity.Id);
        var record = projected.Records.Single(record => record.Identity.Id == "kl");
        Assert.Equal("KL: Capital city", record.Presentation.Summary);
        Assert.Equal(["new"], record.Classification.Tags);
        Assert.Same(post.Record, projected.Records.Single(record => record.Identity.Id == "p1"));
    }

    [Fact]
    public void ProjectRecords_PagesNoRuleChanged_KeepTheGraph()
    {
        // Media localization gives every routed page a new record, derived or not.
        var city = Page("kl", "city", ("name", "KL"));
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([city]);
        RoutedContentDocument[] localized = [Routed(city with { Record = city.Record with { } })];

        var keys = DerivedFieldResolver.DerivedKeys(localized, DerivedFieldResolver.Apply(Content(), localized));

        Assert.Empty(keys);
        Assert.Same(graph, DerivedFieldResolver.ProjectRecords(graph, localized, keys));
    }

    [Fact]
    public void ValidateDerived_ChecksDeferredFieldsOnTheDerivedValues()
    {
        var content = Content(Rule("seo_title", "{{ name }} guide"), Rule("tone", "loud", "override"));
        var config = Config(content with
        {
            ModelSchema = new ContentModelSchemaConfig
            {
                CustomFields =
                [
                    new CustomFieldDefinitionConfig { Name = "seo_title", FieldType = "string", Required = true },
                    new CustomFieldDefinitionConfig { Name = "tone", FieldType = "string", Enum = ["calm", "warm"] },
                    new CustomFieldDefinitionConfig { Name = "mood", FieldType = "string", Enum = ["calm"] }
                ]
            }
        });
        var schema = ContentModelSchemaFactory.FromConfig(config);
        var city = Page("kl", "city", ("name", "KL"), ("tone", "calm"), ("mood", "loud"));
        var documentsById = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase) { ["kl"] = city };

        var before = ContentModelSchemaValidator.Validate(CanonicalContentGraphBuilder.BuildFromDocuments([city]), schema);
        Assert.True(DerivedFieldResolver.DefersCheck(config, Assert.Single(before, issue => issue.Field == "fields.seo_title"), documentsById));
        Assert.False(DerivedFieldResolver.DefersCheck(config, Assert.Single(before, issue => issue.Field == "fields.mood"), documentsById));

        var routed = DerivedFieldResolver.Apply(content, [Routed(city)], schema: schema);
        var issue = Assert.Single(DerivedFieldResolver.ValidateDerived(config, schema, routed, []));
        Assert.Equal("fields.tone", issue.Field);
        Assert.Equal("content.custom_field_enum_mismatch", issue.Code);
    }

    [Fact]
    public void ValidateDerived_RepeatsRequiredFieldChecksOnTheDerivedValues()
    {
        var content = Content(Rule("seo_title", "{{ name }}"));
        var config = Config(content with
        {
            ModelSchema = new ContentModelSchemaConfig
            {
                FieldScopes = new Dictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["city"] = [new CustomFieldDefinitionConfig { Name = "seo_title", FieldType = "string", Required = true }]
                }
            }
        });
        var schema = ContentModelSchemaFactory.FromConfig(config);
        var named = Page("kl", "city", ("name", "KL"));
        var unnamed = Page("pg", "city");
        var documentsById = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase) { ["kl"] = named, ["pg"] = unnamed };
        var sourceIssue = new ContentValidationIssue("seo_title", "content.required_collection_field_missing", "missing", "kl");
        Assert.True(DerivedFieldResolver.DefersRequiredDiagnostic(config, sourceIssue, documentsById));
        Assert.False(DerivedFieldResolver.DefersRequiredDiagnostic(config, sourceIssue with { Field = "name" }, documentsById));

        var routed = DerivedFieldResolver.Apply(content, [Routed(named), Routed(unnamed)], schema: schema);

        // As in the validate stage, the normalizer and the content model both report the missing field.
        var issues = DerivedFieldResolver.ValidateDerived(config, schema, routed, []);
        Assert.All(issues, issue => Assert.Equal("pg", issue.SourcePath));
        Assert.Equal(
            ["content.custom_field_required_missing", "content.required_collection_field_missing"],
            issues.Select(issue => issue.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ValidateDerived_ReportsRecordChecksOfTheDerivedRecords()
    {
        var content = Content(Rule("author", "{{ missing }}", "override"), Rule("summary", "{{ name }}"));
        var config = Config(content with { ModelSchema = new ContentModelSchemaConfig { RequireAuthor = true, RequireSummary = true } });
        var schema = ContentModelSchemaFactory.FromConfig(config);
        var source = Routed(Page("kl", "city", ("author", "Ali"), ("name", "KL")));
        var documentsById = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase) { ["kl"] = source.Document };
        var summary = Assert.Single(
            ContentModelSchemaValidator.Validate(CanonicalContentGraphBuilder.BuildFromDocuments([source.Document]), schema),
            issue => issue.Field == "presentation.summary");
        Assert.True(DerivedFieldResolver.DefersCheck(config, summary, documentsById));

        var routed = DerivedFieldResolver.Apply(content, [source], schema: schema);

        var issue = Assert.Single(DerivedFieldResolver.ValidateDerived(config, schema, routed, []));
        Assert.Equal("ownership.author", issue.Field);
    }

    [Fact]
    public void ValidateDerived_ResolvesRelationsAgainstUnchangedPagesAndEntries()
    {
        var content = Content(Rule("related_to", "{{ target }}", "override"));
        var config = Config(content);
        var schema = ContentModelSchemaFactory.FromConfig(config);
        Assert.True(schema.RequireRelationTargets);
        var post = Routed(Page("p1", "post"));
        var entry = Page("e1", "place");
        RoutedContentDocument[] before = [Routed(Page("kl", "city", ("target", "p1"))), post, Routed(Page("pg", "city", ("target", "e1")))];

        var routed = DerivedFieldResolver.Apply(content, before, schema: schema);

        Assert.Empty(DerivedFieldResolver.ValidateDerived(config, schema, routed, [entry]));
        var missing = DerivedFieldResolver.ValidateDerived(config, schema, routed, []);
        Assert.Equal("pg", Assert.Single(missing).SourcePath);
    }

    [Fact]
    public void ValidateDerived_ResolvesRelationsAgainstOtherLanguagesAndKeepsTranslationsApart()
    {
        // Only a summary is derived; the relation and its target in another language are unchanged.
        var content = Content(Rule("summary", "{{ name }}"));
        var config = Config(content);
        var schema = ContentModelSchemaFactory.FromConfig(config);
        var english = Page("kl", "city", ("language", "en"), ("name", "KL"), ("related_to", "zh-only"));
        var target = Page("zh-only", "post", ("language", "zh"));
        var translation = Page("kl", "city", ("language", "zh"), ("related_to", "missing"));

        var routed = DerivedFieldResolver.Apply(content, [Routed(english)], schema: schema);

        Assert.Empty(DerivedFieldResolver.ValidateDerived(config, schema, routed, [english, target, translation]));
        Assert.Equal("kl", Assert.Single(DerivedFieldResolver.ValidateDerived(config, schema, routed, [english, translation])).SourcePath);
    }

    [Fact]
    public void RoutePipeline_LanguageVariantResolvesRelationsAgainstTheWholeSite()
    {
        // A multilingual build hands the route stage one language; the content graph has the rest.
        var config = Config(Content(Rule("summary", "{{ name }}"))) with
        {
            Site = new SiteConfig
            {
                Name = "t",
                Title = "t",
                Languages = ["en", "zh"],
                DefaultLanguage = "en",
                Language = "en",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["city"] = new() { Permalink = "/cities/{slug}/", Template = "city.html" }
                }
            }
        };
        var english = Page("kl", "city", ("language", "en"), ("name", "KL"), ("related_to", "zh-only"));
        ContentDocument[] site = [english, Page("zh-only", "post", ("language", "zh"))];

        Assert.Empty(new RoutePipeline().Execute(config, [english], siteDocuments: site).DerivedFieldIssues);
        Assert.Equal("kl", Assert.Single(new RoutePipeline().Execute(config, [english]).DerivedFieldIssues).SourcePath);
    }

    [Fact]
    public void Apply_DerivedPublishFields_UpdateThePublishPolicy()
    {
        var content = Content(Rule("noindex", "{{ if thin }}true{{ end }}", "override"), Rule("sitemapExclude", "true", "override"));

        var page = Single(DerivedFieldResolver.Apply(content, [Routed(Page("kl", "city", ("thin", "true")))]));

        Assert.True(page.Publish.NoIndex);
        Assert.True(page.Publish.ExcludeFromSitemap);
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
    public void Apply_RuntimeErrorIsCollectedAndLeavesTheFieldUnchanged()
    {
        var content = Content(Rule("seo_title", "{{ for i in 1..5000 }}x{{ end }}"));
        var city = Routed(Page("kl", "city", ("name", "KL")));
        var problems = new List<ContentValidationIssue>();

        var result = DerivedFieldResolver.Apply(content, [city], problems);

        Assert.Same(city, Assert.Single(result));
        var issue = Assert.Single(problems);
        Assert.Equal(DerivedFieldResolver.FailedCode, issue.Code);
        Assert.Equal("seo_title", issue.Field);
        Assert.Equal("kl", issue.SourcePath);
    }

    [Fact]
    public void ValidateTemplates_TemplateThatDoesNotParse_Throws()
    {
        var exception = Assert.Throws<ConfigException>(() => DerivedFieldResolver.ValidateTemplates(Content(Rule("seo_title", "{{ if }}"))));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("content.derivedFields.city.seo_title.value does not parse", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RoutePipeline_ErrorThatNeedsLinkDataIsReported()
    {
        // Each linked city runs 600 iterations: two links exceed the loop limit, no links do not.
        var config = LinkedConfig("{{ for c in cities_links }}{{ for i in 1..600 }}x{{ end }}{{ end }}");

        var result = new RoutePipeline().Execute(config, [Page("r1", "route", ("cities", new ContentField("list", new[] { "kl", "pg" }))), Page("kl", "city"), Page("pg", "city")]);

        var issue = Assert.Single(result.DerivedFieldIssues);
        Assert.Equal("r1", issue.SourcePath);
        Assert.Equal(DerivedFieldResolver.FailedCode, issue.Code);
    }

    [Theory]
    [InlineData("warn", false)]
    [InlineData("strict", true)]
    [InlineData("off", false)]
    public void Report_FollowsTheCollectionSchemaFailMode(string mode, bool throws)
    {
        var config = LinkedConfig("x");
        config = config with { Build = config.Build with { SchemaFailMode = mode } };
        var page = Routed(Page("r1", "route"));
        var issue = new ContentValidationIssue("seo_title", DerivedFieldResolver.FailedCode, "failed", "r1");
        var logger = new WarnLogger();

        var exception = Record.Exception(() => DerivedFieldResolver.Report(config, [page.Document], [issue], logger));

        Assert.Equal(throws, exception is ConfigException { Code: DiagnosticCode.SchemaStrictModeBlocked });
        Assert.Equal(mode == "off" ? 0 : 1, logger.Warnings.Count(warning => warning.Contains("code=content.derived_field_failed", StringComparison.Ordinal)));
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
        var config = LinkedConfig("Via {{ cities_links[0].title }}");

        var result = new RoutePipeline().Execute(config, [Page("r1", "route", ("cities", "kl")), Page("kl", "city")]);

        var route = Assert.Single(result.RoutedDocuments, page => page.Document.Id == "r1").Document;
        Assert.Equal("Via kl", route.CustomFields!["seo_title"].Value);
        var list = Assert.Single(result.ListRouteGraph.Routes, listRoute => listRoute.Url == "/routes/");
        Assert.Equal("Via kl", Assert.Single(list.Items).Fields!["seo_title"].Value);
    }

    private static AppConfig LinkedConfig(string value) => new()
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
            DerivedFields = [new ContentDerivedFieldConfig { Collection = "route", Field = "seo_title", Value = value }]
        }
    };

    private sealed class WarnLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }

    private static AppConfig Config(ContentConfig content) => new() { Site = new SiteConfig { Name = "t", Title = "t" }, Content = content };

    private static ContentDerivedFieldConfig Rule(string field, string value, string mode = "default")
        => new() { Collection = "city", Field = field, Value = value, Mode = mode };

    private static ContentConfig Content(params ContentDerivedFieldConfig[] rules) => TestContent.Markdown() with { DerivedFields = rules };

    private static ContentDocument Single(IReadOnlyList<RoutedContentDocument> result) => Assert.Single(result).Document;

    private static RoutedContentDocument Routed(ContentDocument document)
    {
        var url = $"/{document.Record.Classification.Collection}/{document.Slug}/";
        return new RoutedContentDocument(document, new RouteInfo(url, url.Trim('/') + "/index.html", "page.html"));
    }

    private static ContentDocument Page(string id, string collection, params (string Name, object Value)[] fields)
    {
        var map = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase) { ["collection"] = new("text", collection) };
        foreach (var (name, value) in fields)
        {
            map[name] = value as ContentField ?? new ContentField("text", value);
        }

        return ContentDocument.Create(id, id, id, DateTimeOffset.UnixEpoch, null, map);
    }
}
