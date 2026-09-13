using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-i18n-regression-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Published = DateTimeOffset.Parse("2026-06-05T00:00:00Z");

    public I18nRegressionTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("searchExclude")]
    [InlineData("excludeFromSearch")]
    [InlineData("publish-policy")]
    public void SearchExclusion_IsIdenticalInSplitAndMergedOutputs(string field)
    {
        var hidden = Document("hidden", "en") with
        {
            Publish = new ContentPublishPolicy(ExcludeFromSearch: field == "publish-policy"),
            CustomFields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                [field] = true
            })
        };
        var visible = Document("visible", "en");
        var variant = Variant("en", hidden, visible);
        SearchIndexBuilder.GenerateSingleSearchIndex(_root, "/en", false, false, 100,
            variant.RoutedDocuments, [], variant.SeoIndex, variant.BodyStore);
        Assert.Equal(new[] { "visible" }, SearchIds());
        SearchIndexBuilder.GenerateMergedSearchIndex(_root, [variant], false, 100);
        Assert.Equal(new[] { "visible" }, SearchIds());
    }

    [Theory]
    [InlineData("split")]
    [InlineData("merged")]
    [InlineData("index")]
    public void RootRobots_ReferencesTheSitemapsForTheSelectedMode(string mode)
    {
        var config = Config() with { Site = Config().Site with { SitemapMode = mode } };
        var variants = new[] { Variant("en", Document("a", "en")), Variant("zh", Document("a", "zh")) };
        foreach (var variant in variants)
        {
            Directory.CreateDirectory(variant.OutputDir);
            File.WriteAllText(Path.Combine(variant.OutputDir, "sitemap.xml"), "<urlset />");
        }
        I18nOutputMerger.GenerateRootOutputs(config, _root, "/docs", variants,
            new ConsoleLogger(LogLevel.Error), new DefaultSearchIndexBuilder());
        var sitemapLines = File.ReadAllLines(Path.Combine(_root, "robots.txt"))
            .Where(line => line.StartsWith("Sitemap:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(mode == "split"
            ? new[] { "Sitemap: https://example.com/docs/en/sitemap.xml", "Sitemap: https://example.com/docs/zh/sitemap.xml" }
            : new[] { "Sitemap: https://example.com/docs/sitemap.xml" }, sitemapLines);
        Assert.Equal(mode != "split", File.Exists(Path.Combine(_root, "sitemap.xml")));
    }

    [Fact]
    public async Task PagesIndex_SameIdTranslationsKeepTheirOwnMetadata()
    {
        var english = Document("shared", "en");
        var chinese = Document("shared", "zh");
        var variant = Variant("zh", chinese);
        var context = new BuildContext
        {
            RootDir = _root, OutputDir = _root, BaseUrl = variant.BaseUrl, LayoutsDir = _root,
            RoutedDocuments = variant.RoutedDocuments, StaticHtmlRoutes = [],
            ContentGraph = new CanonicalContentGraph([english.Record, chinese.Record], []),
            BodyStore = variant.BodyStore, Logger = new ConsoleLogger(LogLevel.Error)
        };
        await new PagesIndexPlugin(Config()).DerivePagesAsync(context);
        var index = Assert.IsType<Dictionary<string, object>>(context.Data["pages_by_id"]);
        var page = Assert.IsType<Dictionary<string, object>>(index["shared"]);
        Assert.Equal(chinese.Title, page["title"]);
        Assert.Equal(chinese.Record.Presentation.Summary, page["summary"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullLlms_SameIdTranslationsKeepTheirOwnMetadata(bool asynchronous)
    {
        var english = Document("shared", "en");
        var chinese = Document("shared", "zh");
        var state = I18nMergedVariantState.Create([Variant("en", english), Variant("zh", chinese)]);
        if (asynchronous)
            await LlmsTxtPlugin.WriteLlmsFullTxtAsync(Config(), _root, "/docs", state.RoutedDocuments,
                [], state.ContentGraph, state.SeoIndex, state.BodyStore);
        else
            LlmsTxtPlugin.WriteLlmsFullTxt(Config(), _root, "/docs", state.RoutedDocuments,
                [], state.ContentGraph, state.SeoIndex, state.BodyStore);
        var text = File.ReadAllText(Path.Combine(_root, "llms-full.txt"));
        Assert.Contains("# en title", text, StringComparison.Ordinal);
        Assert.Contains("# zh title", text, StringComparison.Ordinal);
        Assert.Contains("zh summary", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("search", "search.json", "[{\"url\":\"/docs/en/a/b/\",\"content\":\"/docs/en/a/\"}]")]
    [InlineData("sitemap", "sitemap.xml", "<urlset><url><loc>https://example.com/docs/en/a/b/</loc></url></urlset>")]
    [InlineData("feed", "rss.xml", "<rss><channel><item><link>https://example.com/docs/en/a/b/</link><description>/docs/en/a/</description></item></channel></rss>")]
    [InlineData("atom", "atom.xml", "<feed xmlns='http://www.w3.org/2005/Atom'><entry><link href='https://example.com/docs/en/a/b/'/><summary>/docs/en/a/</summary></entry></feed>")]
    [InlineData("jsonfeed", "feed.json", "{\"items\":[{\"url\":\"https://example.com/docs/en/a/b/\",\"content_text\":\"/docs/en/a/\"}]}")]
    [InlineData("agent-manifest", "agent-manifest.json", "{\"documents\":[{\"route\":\"/docs/en/a/b/\",\"entities\":[\"/docs/en/a/\"]}]}")]
    public void RootInventory_RequiresExactEntryUrl(string kind, string path, string contents)
    {
        File.WriteAllText(Path.Combine(_root, path), contents);
        var variant = Variant("en", Document("a", "en"), Document("a/b", "en"));
        var outputs = I18nRootProjectionInventory.BuildOutputs(_root, new PublishRepresentation(kind, path, true), [variant]);
        Assert.False(Assert.Single(outputs, output => output.Url == "/docs/en/a/").Exists);
        Assert.True(Assert.Single(outputs, output => output.Url == "/docs/en/a/b/").Exists);
    }

    [Fact]
    public void RootInventory_DecodesJsonEscapesAndKeepsPathCaseSensitive()
    {
        File.WriteAllText(Path.Combine(_root, "search.json"), "[{\"url\":\"/docs/en/\\u4e2d/\"},{\"url\":\"/docs/en/A/\"}]");
        var outputs = I18nRootProjectionInventory.BuildOutputs(_root, new PublishRepresentation("search", "search.json", true),
            [Variant("en", Document("中", "en"), Document("a", "en"))]);
        Assert.True(Assert.Single(outputs, output => output.Url == "/docs/en/中/").Exists);
        Assert.False(Assert.Single(outputs, output => output.Url == "/docs/en/a/").Exists);
    }

    [Theory]
    [InlineData("llms")]
    [InlineData("llms-full")]
    public void LlmsInventory_DoesNotTreatBodyOrSummaryLinksAsPublishedEntries(string kind)
    {
        var excluded = Document("a", "en") with
        {
            CustomFields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["geo"] = new Dictionary<string, object>
                {
                    ["llms"] = new Dictionary<string, object> { ["visibility"] = "exclude" }
                }
            })
        };
        const string missingUrl = "https://example.com/docs/en/a/";
        var visible = Document("a/b", "en");
        visible = visible with
        {
            Record = visible.Record with { Presentation = visible.Record.Presentation with { Summary = "See\n- [Mention](" + missingUrl + ")" } },
            Body = new ContentBodyRef("<p>URL: " + missingUrl + "</p>")
        };
        var config = Config() with { Site = Config().Site with { Seo = new SeoConfig { Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true, LlmsFullTxt = true } } } };
        var projection = I18nOutputMerger.GenerateRootOutputs(config, _root, "/docs",
            [Variant("en", excluded, visible)], new ConsoleLogger(LogLevel.Error), new DefaultSearchIndexBuilder())
            .Single(result => result.Representation.Kind == kind);
        Assert.Contains(missingUrl, File.ReadAllText(Path.Combine(_root, kind + ".txt")), StringComparison.Ordinal);
        Assert.False(Assert.Single(projection.Outputs, output => output.Url == "/docs/en/a/").Exists);
        Assert.True(Assert.Single(projection.Outputs, output => output.Url == "/docs/en/a/b/").Exists);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public void FeedDispatch_ReadsEachPostOncePerEnabledFormat(bool allFormats, int expectedReads)
    {
        var store = new CountingBodyStore();
        var variant = Variant("en", Document("a", "en") with { Body = new ContentBodyRef(null, "a") }) with { BodyStore = store };
        var config = Config() with
        {
            Site = Config().Site with { Feed = new FeedConfig { Mode = "merged", Formats = allFormats ? ["rss", "atom", "json"] : ["rss"] } }
        };
        var context = new I18nRootProjectionWriterContext(config, _root, "/docs", [variant], new ConsoleLogger(LogLevel.Error));
        foreach (var entry in I18nRootProjectionWriterRegistry.CreateDefault().BuildPlan(PublishRepresentationRegistry.AggregateRepresentations()))
            if (entry.Writer is I18nRootFeedWriter)
            {
                entry.Writer.Write(context, entry.Representation);
                if (entry.Representation.Kind == "feed")
                {
                    Assert.False(File.Exists(Path.Combine(_root, "feed", "atom.xml")));
                    Assert.False(File.Exists(Path.Combine(_root, "feed", "feed.json")));
                }
            }
        Assert.Equal(expectedReads, store.Reads);
        Assert.True(File.Exists(Path.Combine(_root, "rss.xml")));
        Assert.Equal(allFormats, File.Exists(Path.Combine(_root, "feed", "atom.xml")));
        Assert.Equal(allFormats, File.Exists(Path.Combine(_root, "feed", "feed.json")));
    }

    private string[] SearchIds()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "search.json")));
        return json.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetString()!).ToArray();
    }

    [Theory]
    [InlineData("searchExclude", false)]
    [InlineData("excludeFromSearch", false)]
    [InlineData("publish-policy", false)]
    [InlineData("searchExclude", true)]
    [InlineData("excludeFromSearch", true)]
    [InlineData("publish-policy", true)]
    public void FinalReport_ExclusionsSuppressOnlyIntentionalMissingOutputs(string field, bool merged)
    {
        var excluded = Document("shared", "en") with
        {
            Publish = new ContentPublishPolicy(ExcludeFromSearch: field == "publish-policy"),
            CustomFields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                [field] = true,
                ["geo"] = new Dictionary<string, object>
                {
                    ["llms"] = new Dictionary<string, object> { ["visibility"] = "exclude" }
                }
            })
        };
        // Same ID, different language and policy: audit must resolve by output path.
        var variants = new[] { Variant("en", excluded), Variant("zh", Document("shared", "zh")) };
        var config = Config() with
        {
            Site = Config().Site with
            {
                BaseUrl = "/docs",
                Seo = new SeoConfig { Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true, LlmsFullTxt = true } }
            }
        };
        var logger = new ConsoleLogger(LogLevel.Error);
        foreach (var variant in variants)
        {
            Directory.CreateDirectory(variant.OutputDir);
            if (!merged)
            {
                new BuildReportPipeline().Execute(new BuildReportPipelineContext(
                    Config: config with { Site = config.Site with { Language = variant.Language, BaseUrl = variant.BaseUrl } },
                    Language: variant.Language, OutputDir: variant.OutputDir, BaseUrl: variant.BaseUrl,
                    SearchSnippetsEnabled: false, BodyStore: variant.BodyStore,
                    DerivedRoutes: [], SeoIndex: variant.SeoIndex, SeoModels: variant.SeoModels,
                    PluginExecutions: [], RenderedCount: 0, SkippedCount: 0,
                    RenderReasons: new Dictionary<string, int>(), StageMetrics: BuildStageMetrics.Empty,
                    Logger: logger, DefaultLanguage: "en", RoutedDocuments: variant.RoutedDocuments,
                    ContentGraph: variant.ContentGraph));
                AssertExclusionReport(variant.OutputDir, "/shared/", variant.Language == "en");
            }
        }
        if (merged)
        {
            SeoAuditReportWriter.WriteMerged(config, _root, variants, logger);
            AssertExclusionReport(_root, "/docs/en/shared/", excluded: true);
            AssertExclusionReport(_root, "/docs/zh/shared/", excluded: false);
        }
    }

    private static void AssertExclusionReport(string outputDir, string route, bool excluded)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDir, ".bukit", "publish-audit-report.json")));
        var issues = json.RootElement.GetProperty("issues").EnumerateArray()
            .Where(issue => issue.GetProperty("route").GetString() == route)
            .Select(issue => issue.GetProperty("code").GetString()).ToArray();
        foreach (var code in new[] { "publish.search_missing_route", "publish.llms_missing_route", "publish.llms_full_missing_route" })
            Assert.Equal(!excluded, issues.Contains(code));
        var document = Assert.Single(json.RootElement.GetProperty("documents").EnumerateArray(),
            item => item.GetProperty("routeUrl").GetString() == route);
        var kinds = document.GetProperty("representationKinds").EnumerateArray().Select(kind => kind.GetString()).ToArray();
        foreach (var kind in new[] { "search", "llms", "llms-full" })
            Assert.Equal(!excluded, kinds.Contains(kind));
        Assert.False(document.GetProperty("searchIncluded").GetBoolean());
        Assert.False(document.GetProperty("llmsIncluded").GetBoolean());
        Assert.False(document.GetProperty("llmsFullIncluded").GetBoolean());
    }

    private static ContentDocument Document(string id, string language) => ContentDocument.Create(
        id, language + " title", id, Published, "<p>body</p>",
        ContentFieldReader.ToFieldMap(new Dictionary<string, object>
        {
            ["language"] = language, ["summary"] = language + " summary", ["collection"] = "post", ["type"] = "post"
        }));

    private BuildVariantResult Variant(string language, params ContentDocument[] documents)
    {
        var baseUrl = "/docs/" + language;
        var routed = documents.Select(document => new RoutedContentDocument(document,
            new RouteInfo("/" + document.Slug + "/", document.Slug + "/index.html", "post.html"))).ToArray();
        return new BuildVariantResult(language, Path.Combine(_root, language), baseUrl, false,
            NullContentBodyStore.Instance, [], routed.ToDictionary(item => item.Route.OutputPath,
                item => new SeoIndexEntry(item.Route, "https://example.com" + baseUrl + item.Route.Url,
                    null, true, Published, item.Document.Id, "post", Collection: "post")),
            new Dictionary<string, Bukit.Rendering.SeoModel>(), [], documents.Length, 0,
            new Dictionary<string, int>(), BuildStageMetrics.Empty, routed,
            new CanonicalContentGraph(documents.Select(document => document.Record).ToArray(), []));
    }

    private static AppConfig Config() => new()
    {
        Site = new SiteConfig
        {
            Name = "test", Title = "Test", Url = "https://example.com", Languages = ["en", "zh"],
            Collections = new Dictionary<string, CollectionConfig>
            {
                ["post"] = new() { Permalink = "/{slug}/", Output = new() { Rss = true } }
            },
            Seo = new SeoConfig { RobotsTxt = new SeoRobotsTxtConfig { Enabled = true } }
        },
        Content = TestContent.Markdown()
    };

    private sealed class CountingBodyStore : IContentBodyStore
    {
        internal int Reads { get; private set; }
        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(new ContentBody("<p>body</p>"));
        }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
