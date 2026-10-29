using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class GeoDiagnosticsTests
{
    private sealed class TestLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) => Errors.Add(message);
    }

    public static IEnumerable<object[]> IncompleteRawItems()
    {
        foreach (var type in new[] { "FAQPage", "HowTo" })
        foreach (var defect in new[] { "missing-first", "blank-first", "missing-second", "blank-second", "non-map", "null" })
        foreach (var mixed in new[] { false, true })
        foreach (var mode in new[] { "warn", "strict", "off" })
            yield return [type, defect, mixed, mode];
    }

    [Theory]
    [MemberData(nameof(IncompleteRawItems))]
    public void AnalyzeIndex_RawIncompleteItems_DiagnosesBeforeFilteringJsonLd(string type, string defect, bool mixed, string mode)
    {
        var faq = type == "FAQPage";
        var first = faq ? "question" : "name";
        var second = faq ? "answer" : "text";
        var firstCode = faq ? "geo.faq_empty_question" : "geo.howto_step_empty_name";
        var secondCode = faq ? "geo.faq_empty_answer" : "geo.howto_step_empty_text";
        var malformed = defect is "non-map" or "null";
        var bad = new Dictionary<string, object> { [first] = "Invalid", [second] = "Invalid text" };
        var missingKey = defect.EndsWith("second", StringComparison.Ordinal) ? second : first;
        if (defect.StartsWith("missing", StringComparison.Ordinal)) bad.Remove(missingKey);
        if (defect.StartsWith("blank", StringComparison.Ordinal)) bad[missingKey] = " \t ";
        object invalid = defect == "non-map" ? "not a map" : defect == "null" ? null! : bad;
        var items = new List<object>();
        if (mixed) items.Add(new Dictionary<string, object> { [first] = "First valid", [second] = "First text" });
        items.Add(invalid);
        if (mixed) items.Add(new Dictionary<string, object> { [first] = "Second valid", [second] = "Second text" });
        var config = ConfigWithDiagnostics(mode);
        var model = BuildRawGeoModel(config, new Dictionary<string, object>
        {
            ["schema_type"] = type,
            [faq ? "faq" : "steps"] = items
        });
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry> { ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/") };
        var models = new Dictionary<string, SeoModel> { ["a/index.html"] = model };
        var codes = malformed ? new[] { firstCode, secondCode } : new[] { missingKey == first ? firstCode : secondCode };
        void Analyze() => SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        if (mode == "strict")
        {
            var error = Assert.Throws<ConfigException>(Analyze);
            Assert.Contains(codes[0], error.Message);
            Assert.Single(logger.Errors);
            Assert.Contains(codes[0], logger.Errors[0]);
            Assert.Empty(logger.Warnings);
        }
        else
        {
            Analyze();
            Assert.Empty(logger.Errors);
            Assert.Equal(mode == "off" ? 0 : codes.Length, logger.Warnings.Count);
            if (mode == "warn")
                foreach (var code in codes) Assert.Contains(logger.Warnings, message => message.Contains(code, StringComparison.Ordinal));
        }

        Assert.Equal(mixed ? 3 : 1, faq ? model.FaqItems!.Count : model.HowToSteps!.Count);
        var nodes = model.JsonLd.Select(json =>
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }).ToArray();
        Assert.Contains(nodes, node => node.GetProperty("@type").GetString() == "WebSite");
        Assert.Contains(nodes, node => node.GetProperty("@type").GetString() == "WebPage");
        Assert.DoesNotContain(nodes, node => node.GetProperty("@type").GetString() is "Article" or "BlogPosting");
        if (!mixed)
        {
            Assert.DoesNotContain(nodes, node => node.GetProperty("@type").GetString() == type);
            return;
        }

        var specialized = Assert.Single(nodes, node => node.GetProperty("@type").GetString() == type);
        var emitted = specialized.GetProperty(faq ? "mainEntity" : "step").EnumerateArray().ToArray();
        Assert.Equal(new[] { "First valid", "Second valid" }, emitted.Select(node => node.GetProperty("name").GetString()));
        Assert.Equal(new[] { "First text", "Second text" }, emitted.Select(node => faq
            ? node.GetProperty("acceptedAnswer").GetProperty("text").GetString()
            : node.GetProperty("text").GetString()));
        if (!faq) Assert.Equal(new[] { 1, 2 }, emitted.Select(node => node.GetProperty("position").GetInt32()));
    }

    [Theory]
    [InlineData("FAQPage", false, "warn")]
    [InlineData("FAQPage", false, "strict")]
    [InlineData("FAQPage", false, "off")]
    [InlineData("FAQPage", true, "warn")]
    [InlineData("FAQPage", true, "strict")]
    [InlineData("FAQPage", true, "off")]
    [InlineData("HowTo", false, "warn")]
    [InlineData("HowTo", false, "strict")]
    [InlineData("HowTo", false, "off")]
    [InlineData("HowTo", true, "warn")]
    [InlineData("HowTo", true, "strict")]
    [InlineData("HowTo", true, "off")]
    public void AnalyzeIndex_RawMissingOrEmptyList_RetainsMissingListPolicy(string type, bool explicitEmpty, string mode)
    {
        var config = ConfigWithDiagnostics(mode);
        var geo = new Dictionary<string, object> { ["schema_type"] = type };
        if (explicitEmpty) geo[type == "FAQPage" ? "faq" : "steps"] = new List<object>();
        var model = BuildRawGeoModel(config, geo);
        var logger = new TestLogger();
        var code = type == "FAQPage" ? "geo.faq_items_missing" : "geo.howto_steps_missing";
        void Analyze() => SeoDiagnostics.AnalyzeIndex(config,
            new Dictionary<string, SeoIndexEntry> { ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/") },
            new Dictionary<string, SeoModel> { ["a/index.html"] = model }, logger);

        if (mode == "strict") Assert.Contains(code, Assert.Throws<ConfigException>(Analyze).Message);
        else
        {
            Analyze();
            Assert.Equal(mode == "off" ? 0 : 1, logger.Warnings.Count);
            if (mode == "warn") Assert.Contains(code, Assert.Single(logger.Warnings));
        }
        Assert.DoesNotContain(model.JsonLd, json =>
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("@type").GetString() == type;
        });
    }

    private static SeoModel BuildRawGeoModel(AppConfig config, Dictionary<string, object> geo)
    {
        var document = ContentDocument.Create("a", "A", "a", DateTimeOffset.Parse("2026-10-28T00:00:00Z"), "<p>Body</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "post", ["collection"] = "post", ["geo"] = geo }));
        return SeoModelBuilder.BuildForContent(config, "/", document, new RouteInfo("/a/", "a/index.html", "pages/post.html"));
    }

    [Theory]
    [InlineData("FAQPage", "geo.faq_items_missing", "warn")]
    [InlineData("FAQPage", "geo.faq_items_missing", "strict")]
    [InlineData("FAQPage", "geo.faq_items_missing", "off")]
    [InlineData("HowTo", "geo.howto_steps_missing", "warn")]
    [InlineData("HowTo", "geo.howto_steps_missing", "strict")]
    [InlineData("HowTo", "geo.howto_steps_missing", "off")]
    public void AnalyzeIndex_EmptyStructuredContent_UsesConfiguredDiagnosticPolicy(string type, string code, string mode)
    {
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry> { ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/") };
        var models = new Dictionary<string, SeoModel> { ["a/index.html"] = new() { Title = "A", Canonical = "https://example.com/a/", SchemaType = type, FaqItems = [], HowToSteps = [] } };
        void Analyze() => SeoDiagnostics.AnalyzeIndex(ConfigWithDiagnostics(mode), index, models, logger);

        if (mode == "strict")
        {
            var error = Assert.Throws<ConfigException>(Analyze);
            Assert.Contains(code, error.Message);
            Assert.Contains(logger.Errors, message => message.Contains(code, StringComparison.Ordinal));
        }
        else
        {
            Analyze();
            Assert.Empty(logger.Errors);
            Assert.Equal(mode == "off" ? 0 : 1, logger.Warnings.Count(message => message.Contains(code, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsFaqEmptyQuestion()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                FaqItems = new[] { new GeoFaqModel { Question = "", Answer = "Answer" } }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.faq_empty_question", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsFaqEmptyAnswer()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                FaqItems = new[] { new GeoFaqModel { Question = "Q", Answer = "" } }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.faq_empty_answer", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsHowToStepEmptyName()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                HowToSteps = new[] { new GeoHowToStepModel { Name = "", Text = "Do something" } }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.howto_step_empty_name", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsHowToStepEmptyText()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                HowToSteps = new[] { new GeoHowToStepModel { Name = "Step 1", Text = "" } }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.howto_step_empty_text", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsCitationUrlInvalid()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                Citations = new[] { new GeoCitationModel { Title = "Ref", Url = "not-a-valid-url" } }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.citation_url_invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsCitationRelationInvalid()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                Citations = new[] { new GeoCitationModel { Title = "Ref", Url = "https://example.com", Relation = "copied-from" } }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.citation_relation_invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_StrictMode_ThrowsOnCitationRelationInvalid()
    {
        var config = ConfigWithDiagnostics("strict");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                Citations = new[] { new GeoCitationModel { Title = "Ref", Url = "https://example.com", Relation = "copied-from" } }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => SeoDiagnostics.AnalyzeIndex(config, index, models, logger));

        Assert.Contains("geo.citation_relation_invalid", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyzeLlmsCuration_WarnMode_ReportsInvalidCurationCodes()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var documents = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = DocumentWithLlms(new Dictionary<string, object>
            {
                ["visibility"] = "always",
                ["tier"] = "featured",
                ["priority"] = 999,
                ["weight"] = 1
            })
        };

        SeoDiagnostics.AnalyzeLlmsCuration(config, documents, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.llms_visibility_invalid", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, x => x.Contains("geo.llms_tier_invalid", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, x => x.Contains("geo.llms_priority_invalid", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, x => x.Contains("geo.llms_field_unknown", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeLlmsCuration_StrictMode_ThrowsOnInvalidCuration()
    {
        var config = ConfigWithDiagnostics("strict");
        var logger = new TestLogger();
        var documents = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = DocumentWithLlms(new Dictionary<string, object>
            {
                ["visibility"] = "always"
            })
        };

        var ex = Assert.Throws<ConfigException>(() => SeoDiagnostics.AnalyzeLlmsCuration(config, documents, logger));

        Assert.Contains("geo.llms_visibility_invalid", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyzeLlmsCuration_ValidMetadata_DoesNotReport()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var documents = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = DocumentWithLlms(new Dictionary<string, object>
            {
                ["visibility"] = "include",
                ["tier"] = "optional",
                ["priority"] = 10
            })
        };

        SeoDiagnostics.AnalyzeLlmsCuration(config, documents, logger);

        Assert.Empty(logger.Warnings);
    }

    private static ContentDocument DocumentWithLlms(Dictionary<string, object> llms)
        => ContentDocument.Create(
            id: "llms-page",
            title: "LLMS",
            slug: "llms",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>llms</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["geo"] = new Dictionary<string, object> { ["llms"] = llms }
            }));

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsAuthorNoSameAs()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                GeoAuthor = new GeoAuthorModel { Name = "Alice", SameAs = Array.Empty<string>() }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.author_no_sameas", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsSpeakablePathInvalid()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                SpeakableXPath = "body/article"
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.speakable_path_invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsSchemaTypeMissing()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                Article = new SeoArticleModel { PublishedTime = DateTimeOffset.UtcNow, Author = "Alice" }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, x => x.Contains("geo.schema_type_missing", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_DoesNotReportSchemaTypeMissingWhenAlreadyGeoEnhanced()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                Article = new SeoArticleModel { PublishedTime = DateTimeOffset.UtcNow },
                FaqItems = new[] { new GeoFaqModel { Question = "Q", Answer = "A" } }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.DoesNotContain(logger.Warnings, x => x.Contains("geo.schema_type_missing", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_StrictMode_ThrowsOnGeoFaqEmptyQuestion()
    {
        var config = ConfigWithDiagnostics("strict");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Canonical = "https://example.com/a/",
                FaqItems = new[] { new GeoFaqModel { Question = "", Answer = "Answer" } }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => SeoDiagnostics.AnalyzeIndex(config, index, models, logger));

        Assert.Contains("geo.faq_empty_question", ex.Message, StringComparison.Ordinal);
    }

    private static SeoIndexEntry Entry(string url, string outputPath, string canonical)
        => new(new RouteInfo(url, outputPath, "pages/page.html"), Canonical: canonical, Robots: null, Indexable: true, DateTimeOffset.UtcNow, SourceItemId: null, ContentType: "page");

    private static AppConfig ConfigWithDiagnostics(string diagnostics)
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Seo = new SeoConfig { Diagnostics = diagnostics }
            },
            Content = TestContent.Markdown()
        };
}
