using System.Linq;
using Bukit.Config;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoAuditReportWriterTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "bukit-seo-audit-tests-" + Guid.NewGuid().ToString("N"));

    public SeoAuditReportWriterTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    [Fact]
    public void Build_DoesNotReportDuplicateTitleForMutualHreflangAlternates()
    {
        WriteOutput("en/index.html");
        WriteOutput("ms/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["en/index.html"] = Entry("/en/", "en/index.html", "https://example.com/en/"),
            ["ms/index.html"] = Entry("/ms/", "ms/index.html", "https://example.com/ms/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["en/index.html"] = Model("Home", "https://example.com/en/", "https://example.com/en/", "https://example.com/ms/"),
            ["ms/index.html"] = Model("Home", "https://example.com/ms/", "https://example.com/en/", "https://example.com/ms/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Equal("1.0", report.SchemaVersion);
        Assert.Equal("https://bukit.dev/schemas/seo-report.v1.json", report.Schema);
        Assert.DoesNotContain(report.Issues, x => x.Code == "seo.title_duplicate");
    }

    [Fact]
    public void Build_ReportsDuplicateTitleForUnrelatedRoutes()
    {
        WriteOutput("a/index.html");
        WriteOutput("b/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/"),
            ["b/index.html"] = Entry("/b/", "b/index.html", "https://example.com/b/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Model("Same", "https://example.com/a/"),
            ["b/index.html"] = Model("Same", "https://example.com/b/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "seo.title_duplicate" && x.Route == "/a/");
        Assert.Contains(report.Issues, x => x.Code == "seo.title_duplicate" && x.Route == "/b/");
    }

    [Fact]
    public void Build_ReportsInvalidSitemapXml()
    {
        WriteOutput("a/index.html");
        File.WriteAllText(Path.Combine(_outputDir, "sitemap.xml"), "<urlset><url></urlset>");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Model("A", "https://example.com/a/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "seo.sitemap_xml_invalid" && x.Severity == "error");
    }

    [Fact]
    public void Build_ReportsCanonicalThatIsRelativeOrHasFragment()
    {
        WriteOutput("a/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "/a/#section")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Model("A", "/a/#section")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.True(report.Issues.Any(x => x.Code == "seo.canonical_not_absolute" && x.Route == "/a/"), "Expected seo.canonical_not_absolute issue for route /a/");
        Assert.True(report.Issues.Any(x => x.Code == "seo.inject_canonical_missing" && x.Route == "/a/"), "Expected seo.inject_canonical_missing issue for route /a/");
    }

    [Fact]
    public void Build_ReportsCanonicalFragment_WhenAbsolute()
    {
        WriteOutput("b/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["b/index.html"] = Entry("/b/", "b/index.html", "https://example.com/b/#section")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["b/index.html"] = Model("B", "https://example.com/b/#section")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.True(report.Issues.Any(x => x.Code == "seo.canonical_has_fragment" && x.Route == "/b/"), "Expected seo.canonical_has_fragment issue for route /b/");
    }

    [Fact]
    public void Build_ReportsHreflangSelfReferenceMissing()
    {
        WriteOutput("en/index.html");
        WriteOutput("ms/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["en/index.html"] = Entry("/en/", "en/index.html", "https://example.com/en/"),
            ["ms/index.html"] = Entry("/ms/", "ms/index.html", "https://example.com/ms/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["en/index.html"] = new()
            {
                Title = "Home",
                Description = "Home description",
                Canonical = "https://example.com/en/",
                Alternates = new[] { new SeoAlternateModel("ms", "https://example.com/ms/") }
            },
            ["ms/index.html"] = Model("Home", "https://example.com/ms/", "https://example.com/en/", "https://example.com/ms/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "seo.hreflang_self_missing" && x.Route == "/en/");
    }

    [Fact]
    public void Build_ReportsSchemaRequiredAndRecommendedFieldGaps()
    {
        WriteOutput("post/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new()
            {
                Title = "Post",
                Description = "Post description",
                Canonical = "https://example.com/post/",
                JsonLd = new[]
                {
                    """{"@context":"https://schema.org","@type":"BlogPosting","url":"https://example.com/post/"}""",
                    """{"@context":"https://schema.org","@type":"ItemList","itemListElement":[{"@type":"ListItem","name":"One"}]}""",
                    """{"@context":"https://schema.org","@type":"WebSite","name":"Site","url":"https://example.com","potentialAction":{"@type":"SearchAction"}}"""
                }
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "seo.schema_blogposting_headline_missing" && x.Severity == "error");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_blogposting_date_published_missing" && x.Severity == "error");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_blogposting_author_missing" && x.Severity == "warning");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_blogposting_image_missing" && x.Severity == "warning");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_itemlist_position_missing" && x.Severity == "error");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_itemlist_url_missing" && x.Severity == "warning");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_searchaction_target_missing" && x.Severity == "warning");
    }

    [Fact]
    public void Build_ReportsMissingHeadAndSmallSameSiteImage()
    {
        var outputPath = Path.Combine(_outputDir, "image", "index.html");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, "<!doctype html><html><body>No head</body></html>");
        var imagePath = Path.Combine(_outputDir, "assets", "og.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        File.WriteAllBytes(imagePath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));

        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/index.html"] = Entry("/image/", "image/index.html", "https://example.com/image/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/index.html"] = new()
            {
                Title = "Image",
                Description = "Image description",
                Canonical = "https://example.com/image/",
                Og = new SeoOpenGraphModel { Image = "https://example.com/assets/og.png" }
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "seo.html_head_missing" && x.Route == "/image/");
        Assert.Contains(report.Issues, x => x.Code == "seo.og_image_too_small" && x.Route == "/image/");
    }

    [Fact]
    public void Write_WritesReportUnderBukitDirectory()
    {
        WriteOutput("a/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Model("A", "https://example.com/a/")
        };

        SeoAuditReportWriter.Write(Config(), _outputDir, index, models, new ConsoleLogger(LogLevel.Error));

        Assert.True(File.Exists(Path.Combine(_outputDir, ".bukit", "seo-report.json")));
    }

    [Fact]
    public void Write_DoesNotWriteLegacyRootReport()
    {
        WriteOutput("a/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Model("A", "https://example.com/a/")
        };

        SeoAuditReportWriter.Write(Config(), _outputDir, index, models, new ConsoleLogger(LogLevel.Error));

        Assert.False(File.Exists(Path.Combine(_outputDir, "seo-report.json")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_outputDir, recursive: true); } catch { }
    }

    private void WriteOutput(string path)
    {
        var fullPath = Path.Combine(_outputDir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "<!doctype html><html><head></head><body></body></html>");
    }

    private static SeoIndexEntry Entry(string url, string outputPath, string canonical)
        => new(new RouteInfo(url, outputPath, "pages/page.html"), canonical, Robots: null, Indexable: true, DateTimeOffset.UtcNow, SourceItemId: null, ContentType: "page");

    private static SeoModel Model(string title, string canonical, params string[] alternates)
        => new()
        {
            Title = title,
            Description = title + " description",
            Canonical = canonical,
            Alternates = alternates.Select(href => new SeoAlternateModel(href.EndsWith("/en/", StringComparison.Ordinal) ? "en" : "ms", href)).ToArray()
        };

    private static AppConfig Config()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com"
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
}
