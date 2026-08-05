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
