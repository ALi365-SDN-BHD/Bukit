using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoDiagnosticsTests
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
    public void AnalyzeIndex_WarnMode_ReportsDuplicateCanonicalAcrossRoutes()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/"),
            ["b/index.html"] = Entry("/b/", "b/index.html", "https://example.com/a/")
        };

        SeoDiagnostics.AnalyzeIndex(config, index, new Dictionary<string, SeoModel>(), logger);

        Assert.Contains(logger.Warnings, x => x.Contains("seo.canonical_duplicate_index", StringComparison.Ordinal) &&
                                             x.Contains("/a/", StringComparison.Ordinal) &&
                                             x.Contains("/b/", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsRelativeCanonical()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "blog/post/")
        };

        SeoDiagnostics.AnalyzeIndex(config, index, new Dictionary<string, SeoModel>(), logger);

        Assert.Contains(logger.Warnings, warning =>
            warning.Contains("seo.canonical_relative", StringComparison.Ordinal) &&
            warning.Contains("blog/post/", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_ReportsBareOpenGraphLocale()
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
                Og = new SeoOpenGraphModel { Locale = "en" }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, warning =>
            warning.Contains("seo.og_locale_region_missing", StringComparison.Ordinal) &&
            warning.Contains("locale=en", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_StrictMode_BareOpenGraphLocale_WarnsWithoutThrowing()
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
                Og = new SeoOpenGraphModel { Locale = "en" }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.Contains(logger.Warnings, warning =>
            warning.Contains("seo.og_locale_region_missing", StringComparison.Ordinal));
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void AnalyzeIndex_WarnMode_DoesNotReportRegionalOpenGraphLocale()
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
                Og = new SeoOpenGraphModel { Locale = "en_US" }
            }
        };

        SeoDiagnostics.AnalyzeIndex(config, index, models, logger);

        Assert.DoesNotContain(logger.Warnings, warning =>
            warning.Contains("seo.og_locale_region_missing", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeIndex_StrictMode_ThrowsWhenHreflangMissingXDefault()
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
                Alternates = new[] { new SeoAlternateModel("en", "https://example.com/a/") }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => SeoDiagnostics.AnalyzeIndex(config, index, models, logger));

        Assert.Contains("seo.hreflang_x_default_missing", ex.Message, StringComparison.Ordinal);
        Assert.Contains(logger.Errors, x => x.Contains("seo.hreflang_x_default_missing", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeHtml_WarnMode_LeavesDocumentTitleProblemsToAudit()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var route = new RouteInfo("/page/", "page/index.html", "pages/page.html");
        var model = new SeoModel
        {
            Title = "Semantic title",
            DocumentTitle = "Expected title",
            Canonical = "https://example.com/page/"
        };
        var html = """
            <html><head>
              <title>Actual &amp; title</title>
              <title> </title>
            </head><body></body></html>
            """;

        SeoDiagnostics.AnalyzeHtml(config, route, model, html, logger);

        // The publish audit owns title findings outside strict mode; render time stays quiet.
        Assert.DoesNotContain(logger.Warnings, warning => warning.Contains("seo.document_title", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzeHtml_StrictMode_StopsOnDocumentTitleProblems()
    {
        var config = ConfigWithDiagnostics("strict");
        var route = new RouteInfo("/page/", "page/index.html", "pages/page.html");
        var model = new SeoModel
        {
            Title = "Semantic title",
            DocumentTitle = "Expected title",
            Canonical = "https://example.com/page/"
        };
        var html = """<html><head><link rel="canonical" href="https://example.com/page/"><title>Actual</title></head><body></body></html>""";

        var ex = Assert.Throws<ConfigException>(() => SeoDiagnostics.AnalyzeHtml(config, route, model, html, new TestLogger()));
        Assert.Contains("seo.document_title_mismatch", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyzeHtml_StrictMode_EncodedExpectedTitleMatchesDecodedHtml()
    {
        var config = ConfigWithDiagnostics("strict");
        var route = new RouteInfo("/page/", "page/index.html", "pages/page.html");
        var model = new SeoModel
        {
            Title = "Semantic title",
            DocumentTitle = "Tom &amp; Jerry",
            Canonical = "https://example.com/page/"
        };
        var html = """<html><head><link rel="canonical" href="https://example.com/page/"><title>Tom &amp; Jerry</title></head><body></body></html>""";

        SeoDiagnostics.AnalyzeHtml(config, route, model, html, new TestLogger());
    }

    [Fact]
    public void AnalyzeHtml_WarnMode_DecodedEquivalentDocumentTitleDoesNotMismatch()
    {
        var config = ConfigWithDiagnostics("warn");
        var logger = new TestLogger();
        var route = new RouteInfo("/page/", "page/index.html", "pages/page.html");
        var model = new SeoModel
        {
            Title = "Semantic title",
            DocumentTitle = "Page & Site",
            Canonical = "https://example.com/page/"
        };
        var html = "<html><head><title> Page &amp;   Site </title></head><body></body></html>";

        SeoDiagnostics.AnalyzeHtml(config, route, model, html, logger);

        Assert.DoesNotContain(logger.Warnings, warning => warning.Contains("seo.document_title_mismatch", StringComparison.Ordinal));
    }

    private static SeoIndexEntry Entry(string url, string outputPath, string canonical)
        => new(new RouteInfo(url, outputPath, "pages/page.html"), canonical, Robots: null, Indexable: true, DateTimeOffset.UtcNow, SourceItemId: null, ContentType: "page");

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
