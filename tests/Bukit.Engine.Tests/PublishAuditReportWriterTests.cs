using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PublishAuditReportWriterTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "bukit-publish-audit-tests-" + Guid.NewGuid().ToString("N"));

    public PublishAuditReportWriterTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    [Fact]
    public void Write_WritesDistinctPublishReportWithDocuments()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><main><article><h1>Post</h1><time datetime="2026-06-05">June 5</time><p>Body content for people and machines.</p></article></main></body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };
        var graph = ContentGraph();

        SeoAuditReportWriter.Write(Config(), _outputDir, index, models, graph, new ConsoleLogger(LogLevel.Error));

        var seoPath = Path.Combine(_outputDir, ".bukit", "seo-report.json");
        var publishPath = Path.Combine(_outputDir, ".bukit", "publish-audit-report.json");
        using var seoDoc = JsonDocument.Parse(File.ReadAllText(seoPath));
        using var publishDoc = JsonDocument.Parse(File.ReadAllText(publishPath));

        Assert.True(seoDoc.RootElement.TryGetProperty("routes", out _));
        Assert.False(seoDoc.RootElement.TryGetProperty("documents", out _));
        Assert.Equal("https://bukit.dev/schemas/seo-report.v1.json", seoDoc.RootElement.GetProperty("schema").GetString());
        Assert.False(publishDoc.RootElement.TryGetProperty("routes", out _));
        Assert.True(publishDoc.RootElement.TryGetProperty("documents", out var documents));
        Assert.Equal("https://bukit.dev/schemas/publish-audit-report.v1.json", publishDoc.RootElement.GetProperty("schema").GetString());
        Assert.Equal("/post/", documents.EnumerateArray().Single().GetProperty("routeUrl").GetString());
        Assert.NotEqual(File.ReadAllText(seoPath), File.ReadAllText(publishPath));
    }

    [Fact]
    public void Build_ReportsPublishJsonLdMismatchSearchGapAndAiCrawlerConflict()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><main><article><h1>Visible Post</h1><time datetime="2026-06-05">June 5</time><p>Body content for people and machines.</p></article></main></body>
            </html>
            """);
        File.WriteAllText(Path.Combine(_outputDir, "search.json"), "[]");
        File.WriteAllText(Path.Combine(_outputDir, "robots.txt"), """
            User-agent: GPTBot
            Disallow: /
            Sitemap: https://example.com/sitemap.xml
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Structured Post", "https://example.com/post/", """
                {
                  "@context": "https://schema.org",
                  "@type": "Article",
                  "headline": "Structured Post",
                  "description": "Structured description"
                }
                """)
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, ContentGraph());

        Assert.Contains(report.Issues, x => x.Code == "publish.jsonld_title_mismatch" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.search_missing_route" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.ai_crawler_policy_conflict" && x.Route == "/post/");
    }

    [Fact]
    public void PublishAuditBuilder_BuildsDocumentsAndSummary()
    {
        var seoReport = new SeoAuditReport(
            "https://bukit.dev/schemas/seo-report.v1.json",
            "1.0",
            DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
            "Test",
            "https://example.com",
            "/",
            [
                new SeoAuditRoute(
                    "/post/",
                    "post/index.html",
                    "Post",
                    "Post description",
                    "https://example.com/post/",
                    null,
                    true,
                    DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                    "post",
                    "post-1",
                    true,
                    true,
                    false,
                    [],
                    ["Article"],
                    Language: "en",
                    Author: "Ali",
                    Source: "markdown",
                    ReviewStatus: "approved",
                    EntityNames: ["Bukit"],
                    RepresentationKinds: ["html", "json", "markdown"])
            ],
            [new SeoAuditIssue("warning", "publish.rss_missing_route", "/post/", "missing rss")],
            new SeoAuditSummary(1, 1, 0, 0, 1, false, false, 1, 50, PublishIssueCount: 1));

        var publishReport = PublishAuditBuilder.Build(seoReport);

        Assert.Equal("https://bukit.dev/schemas/publish-audit-report.v1.json", publishReport.Schema);
        Assert.Equal(1, publishReport.Summary.DocumentCount);
        Assert.Equal(1, publishReport.Summary.PublishIssueCount);
        var document = Assert.Single(publishReport.Documents);
        Assert.Equal("/post/", document.RouteUrl);
        Assert.Equal("Article", Assert.Single(document.SchemaTypes));
        Assert.False(document.RssIncluded);
    }

    [Fact]
    public void Build_ReportsExpandedSemanticJsonLdRssAndRobotsGaps()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body>
              <main>
                <article>
                  <h1>Visible Post</h1>
                  <p>Visible description for this page.</p>
                  <p>By Ali</p>
                  <time datetime="2026-06-05">June 5</time>
                  <figure><img src="/photo.jpg" alt="Photo"></figure>
                </article>
              </main>
            </body>
            </html>
            """);
        File.WriteAllText(Path.Combine(_outputDir, "rss.xml"), "<rss><channel></channel></rss>");
        File.WriteAllText(Path.Combine(_outputDir, "robots.txt"), """
            User-agent: *
            Disallow: /
            User-agent: GPTBot
            Disallow: /
            Allow: /post/
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Visible Post", "https://example.com/post/", """
                {
                  "@context": "https://schema.org",
                  "@type": "Article",
                  "headline": "Visible Post",
                  "description": "Structured description",
                  "author": { "name": "Wrong Author" },
                  "datePublished": "2026-06-04"
                }
                """)
        };
        var config = ConfigWithRssPostCollection();

        var report = SeoAuditReportWriter.Build(config, _outputDir, index, models, ContentGraph());

        Assert.Contains(report.Issues, x => x.Code == "publish.jsonld_description_mismatch" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.jsonld_author_mismatch" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.jsonld_date_mismatch" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.semantic_header_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.semantic_nav_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.semantic_footer_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.figure_caption_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.rss_missing_route" && x.Route == "/post/");
        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.ai_crawler_policy_conflict" && x.Route == "/post/");
        Assert.True(report.Summary.MachineReadabilityIssueCount >= 8);
    }

    [Fact]
    public void Build_ReportsJsonLdConsistencyGapsWithoutTitleShortCircuit()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav><main><article><h1>Visible Post</h1><time datetime="2026-06-05">June 5</time><p>Visible page text.</p></article></main><footer></footer></body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Visible Post", "https://example.com/post/", """
                {
                  "@context": "https://schema.org",
                  "@type": "Article",
                  "headline": "Wrong Post",
                  "description": "Wrong description",
                  "author": "Wrong Author",
                  "dateModified": "2026-06-04"
                }
                """)
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, ContentGraph());

        Assert.Contains(report.Issues, x => x.Code == "publish.jsonld_title_mismatch" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.jsonld_description_mismatch" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.jsonld_author_mismatch" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.jsonld_date_mismatch" && x.Route == "/post/");
    }

    [Fact]
    public void Build_ReportsTrustGraphCompletenessGaps()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav><main><article><h1>Post</h1><time datetime="2026-06-05">June 5</time><p>Body content for people and machines.</p></article></main><footer></footer></body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, ContentGraphWithTrustGaps());

        Assert.Contains(report.Issues, x => x.Code == "publish.summary_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.updated_at_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.source_references_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.entity_summary_missing" && x.Route == "/post/");
    }

    [Fact]
    public void Build_ReportsAiCrawlerConflictForWildcardDisallow()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav><main><article><h1>Post</h1><time datetime="2026-06-05">June 5</time><p>Body content for people and machines.</p></article></main><footer></footer></body>
            </html>
            """);
        File.WriteAllText(Path.Combine(_outputDir, "robots.txt"), """
            User-agent: *
            Disallow: /
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, ContentGraph());

        Assert.Contains(report.Issues, x => x.Code == "publish.ai_crawler_policy_conflict" && x.Route == "/post/");
    }

    [Fact]
    public void Build_DoesNotReportAiCrawlerConflictForEmptyDisallowOrOtherBotGroup()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav><main><article><h1>Post</h1><time datetime="2026-06-05">June 5</time><p>Body content for people and machines.</p></article></main><footer></footer></body>
            </html>
            """);
        File.WriteAllText(Path.Combine(_outputDir, "robots.txt"), """
            User-agent: GPTBot
            Disallow:

            User-agent: Googlebot
            Disallow: /
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, ContentGraph());

        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.ai_crawler_policy_conflict" && x.Route == "/post/");
    }

    public void Dispose()
    {
        try { Directory.Delete(_outputDir, recursive: true); } catch { }
    }

    private void WriteOutput(string path, string html)
    {
        var fullPath = Path.Combine(_outputDir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, html);
    }

    private static SeoIndexEntry Entry(string url, string outputPath, string canonical)
        => new(new RouteInfo(url, outputPath, "pages/post.html"), canonical, Robots: null, Indexable: true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), SourceItemId: "post-1", ContentType: "post");

    private static SeoModel Model(string title, string canonical, params string[] jsonLd)
        => new()
        {
            Title = title,
            Description = title + " description",
            Canonical = canonical,
            JsonLd = jsonLd.ToArray()
        };

    private static CanonicalContentGraph ContentGraph()
        => new(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>body</p></article>", "en", []),
                new ContentClassification("post", "post", [], ["audit"]),
                new ContentOwnership("Ali", "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), DateTimeOffset.Parse("2026-06-05T01:00:00Z"), null, null),
                new ProvenanceRecord("markdown", "https://example.com/original", [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                [new EntityRecord("company", "Bukit")],
                [],
                [])
        ], [new EntityRecord("company", "Bukit")]);

    private static CanonicalContentGraph ContentGraphWithTrustGaps()
        => new(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", null, "<article><p>body</p></article>", "en", []),
                new ContentClassification("post", "post", [], ["audit"]),
                new ContentOwnership("Ali", "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), null, null, null),
                new ProvenanceRecord("markdown", null, [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                [new EntityRecord("company", "Bukit")],
                [],
                [])
        ], [new EntityRecord("company", "Bukit")]);

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

    private static AppConfig ConfigWithRssPostCollection()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["post"] = new() { Permalink = "/post/{slug}/", Output = new CollectionOutputConfig { Rss = true } }
                }
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
}
