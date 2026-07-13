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
        var document = documents.EnumerateArray().Single();
        Assert.Equal("/post/", document.GetProperty("routeUrl").GetString());
        var representations = document.GetProperty("representations").EnumerateArray().ToArray();
        Assert.Contains(representations, x => x.GetProperty("kind").GetString() == "html" && x.GetProperty("url").GetString() == "/post/" && x.GetProperty("generated").GetBoolean());
        Assert.Contains(representations, x => x.GetProperty("kind").GetString() == "json" && x.GetProperty("path").GetString() == "content/post.json");
        Assert.Contains(representations, x => x.GetProperty("kind").GetString() == "markdown" && x.GetProperty("path").GetString() == "content/post.md");
        Assert.NotEqual(File.ReadAllText(seoPath), File.ReadAllText(publishPath));
    }

    [Fact]
    public void Write_WritesPublishDocumentFactsFromContentTrustGraph()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav><main><article><h1>Post</h1><h2>Details</h2><time datetime="2026-06-05">June 5</time><p>Body content for people and machines.</p></article></main><footer></footer></body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/", """
                { "@context": "https://schema.org", "@type": "Article", "headline": "Post" }
                """)
        };

        SeoAuditReportWriter.Write(Config(), _outputDir, index, models, ContentGraph(), new ConsoleLogger(LogLevel.Error));

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_outputDir, ".bukit", "publish-audit-report.json")));
        var document = doc.RootElement.GetProperty("documents").EnumerateArray().Single();
        Assert.Equal("Post description", document.GetProperty("summary").GetString());
        Assert.Equal("2026-06-05T01:00:00+00:00", document.GetProperty("updatedAt").GetString());
        Assert.Contains("https://example.com/original", document.GetProperty("sourceReferences").EnumerateArray().Select(x => x.GetString()));
        var entity = document.GetProperty("entitySummaries").EnumerateArray().Single();
        Assert.Equal("Bukit", entity.GetProperty("name").GetString());
        Assert.Equal("company", entity.GetProperty("type").GetString());
        Assert.True(entity.TryGetProperty("description", out _));
        var outline = document.GetProperty("semanticOutline").EnumerateArray().ToArray();
        Assert.Contains(outline, x => x.GetProperty("level").GetInt32() == 1 && x.GetProperty("text").GetString() == "Post");
        Assert.Contains(outline, x => x.GetProperty("level").GetInt32() == 2 && x.GetProperty("text").GetString() == "Details");
        Assert.Contains("Article", document.GetProperty("structuredDataTypes").EnumerateArray().Select(x => x.GetString()));
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
    public void Build_DoesNotReportJsonLdTitleMismatchForAuxiliarySchemaNames()
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
            ["post/index.html"] = Model("Visible Post", "https://example.com/post/",
                """{"@context":"https://schema.org","@type":"WebSite","name":"Example Site","url":"https://example.com/"}""",
                """{"@context":"https://schema.org","@type":"WebPage","name":"Visible Post","description":"Visible Post description","url":"https://example.com/post/"}""",
                """{"@context":"https://schema.org","@type":"BreadcrumbList","itemListElement":[{"@type":"ListItem","position":1,"name":"Blog","item":"https://example.com/blog/"},{"@type":"ListItem","position":2,"name":"Visible Post","item":"https://example.com/post/"}]}""")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, ContentGraph());

        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.jsonld_title_mismatch" && x.Route == "/post/");
    }

    [Fact]
    public void Build_HeadingAuditExcludesHeaderNavFooterCommentsAndRawText()
    {
        WriteOutput("post/index.html", """
            <!doctype html><html><head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body>
              <header><h1>Header title</h1></header>
              <nav><h2>Navigation</h2></nav>
              <!-- <h1>Comment title</h1> -->
              <main><article><script>const shell = "</script-not><h1>Script title</h1>";</script><h2>Section</h2><h3>Detail</h3><time datetime="2026-06-05">June 5</time><p>Body.</p></article></main>
              <footer><h1>Footer title</h1><h4>Footer group</h4></footer>
            </body></html>
            """);
        var index = TrustAuditIndex();
        var models = TrustAuditModels();

        var result = MachineReadabilityTrustAuditBuilder.Build(Config(), _outputDir, index, models, ContentGraph());

        Assert.Contains(result.SeoReport.Issues, issue => issue.Code == "publish.heading_h1_missing" && issue.Route == "/post/");
        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.heading_level_skip" && issue.Route == "/post/");
        var outline = Assert.Single(result.PublishReport.Documents).SemanticOutline;
        Assert.Equal(new[] { "Section", "Detail" }, outline.Select(item => item.Text));
    }

    [Theory]
    [InlineData("<main><h1>Shell</h1><article><h1>Article title</h1><h2>Details</h2></article></main>", "Article title", false)]
    [InlineData("<main><h1>Main title</h1><h2>Details</h2></main>", "Main title", false)]
    [InlineData("<article><h1>Article fallback</h1><h2>Details</h2></article>", "Article fallback", true)]
    public void Build_HeadingAuditUsesConfiguredPrimaryScope(string primaryHtml, string expectedTitle, bool expectMainMissing)
    {
        WriteOutput("post/index.html", $$"""
            <!doctype html><html><head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav>{{primaryHtml}}<footer><main><h1>Footer title</h1></main></footer></body></html>
            """);
        var index = TrustAuditIndex();
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model(expectedTitle, "https://example.com/post/", $$"""
                { "@context": "https://schema.org", "@type": "Article", "headline": {{JsonSerializer.Serialize(expectedTitle)}} }
                """)
        };

        var result = MachineReadabilityTrustAuditBuilder.Build(Config(), _outputDir, index, models, ContentGraph());

        Assert.Equal(expectMainMissing, result.SeoReport.Issues.Any(issue => issue.Code == "publish.semantic_main_missing" && issue.Route == "/post/"));
        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.heading_h1_missing" && issue.Route == "/post/");
        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.jsonld_title_mismatch" && issue.Route == "/post/");
        var outline = Assert.Single(result.PublishReport.Documents).SemanticOutline;
        Assert.Equal(expectedTitle, outline[0].Text);
        Assert.DoesNotContain(outline, item => item.Text is "Shell" or "Footer title");
    }

    [Fact]
    public void Build_HeadingAuditFallsBackToMainForHeroTitleAndArticleSections()
    {
        WriteHeadingAuditOutput("""
            <main>
              <section><h1>Hero title</h1></section>
              <article><h2>Article section</h2></article>
            </main>
            """);

        var result = MachineReadabilityTrustAuditBuilder.Build(
            Config(),
            _outputDir,
            TrustAuditIndex(),
            HeadingAuditModels("Hero title"),
            ContentGraph());

        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.heading_h1_missing" && issue.Route == "/post/");
        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.jsonld_title_mismatch" && issue.Route == "/post/");
        var outline = Assert.Single(result.PublishReport.Documents).SemanticOutline;
        Assert.Equal(new[] { "Hero title", "Article section" }, outline.Select(item => item.Text));
        Assert.Equal(new[] { 1, 2 }, outline.Select(item => item.Level));
    }

    [Theory]
    [InlineData("<article></article>")]
    [InlineData("<article><h1>   </h1></article>")]
    public void Build_HeadingAuditFallsBackToMainWhenArticlesHaveNoVisibleH1(string articleHtml)
    {
        WriteHeadingAuditOutput($$"""
            <main>
              <section><h1>Hero title</h1></section>
              {{articleHtml}}
            </main>
            """);

        var result = MachineReadabilityTrustAuditBuilder.Build(
            Config(),
            _outputDir,
            TrustAuditIndex(),
            HeadingAuditModels("Hero title"),
            ContentGraph());

        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.heading_h1_missing" && issue.Route == "/post/");
        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.jsonld_title_mismatch" && issue.Route == "/post/");
        var outline = Assert.Single(result.PublishReport.Documents).SemanticOutline;
        var heading = Assert.Single(outline);
        Assert.Equal(1, heading.Level);
        Assert.Equal("Hero title", heading.Text);
    }

    [Fact]
    public void Build_HeadingAuditUsesWholeMainForCardArticlesWithoutH1()
    {
        WriteHeadingAuditOutput("""
            <main>
              <section><h1>Join us</h1></section>
              <section>
                <h2>Services</h2>
                <article><h3>China</h3></article>
                <article><h3>Malaysia</h3></article>
              </section>
            </main>
            """);

        var result = MachineReadabilityTrustAuditBuilder.Build(
            Config(),
            _outputDir,
            TrustAuditIndex(),
            HeadingAuditModels("Join us"),
            ContentGraph());

        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.heading_h1_missing" && issue.Route == "/post/");
        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.heading_level_skip" && issue.Route == "/post/");
        var outline = Assert.Single(result.PublishReport.Documents).SemanticOutline;
        Assert.Equal(new[] { "Join us", "Services", "China", "Malaysia" }, outline.Select(item => item.Text));
        Assert.Equal(new[] { 1, 2, 3, 3 }, outline.Select(item => item.Level));
    }

    [Fact]
    public void Build_HeadingAuditSelectsFirstArticleWithVisibleH1()
    {
        WriteHeadingAuditOutput("""
            <main>
              <h1>Shell title</h1>
              <article><h2>Card section</h2></article>
              <article><h1>Primary article</h1><h2>Details</h2></article>
              <article><h1>Later article</h1></article>
            </main>
            """);

        var result = MachineReadabilityTrustAuditBuilder.Build(
            Config(),
            _outputDir,
            TrustAuditIndex(),
            HeadingAuditModels("Primary article"),
            ContentGraph());

        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.heading_h1_missing" && issue.Route == "/post/");
        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.jsonld_title_mismatch" && issue.Route == "/post/");
        var outline = Assert.Single(result.PublishReport.Documents).SemanticOutline;
        Assert.Equal(new[] { "Primary article", "Details" }, outline.Select(item => item.Text));
    }

    [Fact]
    public void Build_HeadingAuditStillReportsRealLevelSkipAfterMainFallback()
    {
        WriteHeadingAuditOutput("""
            <main>
              <h1>Hero title</h1>
              <article><h3>Skipped section</h3></article>
            </main>
            """);

        var result = MachineReadabilityTrustAuditBuilder.Build(
            Config(),
            _outputDir,
            TrustAuditIndex(),
            HeadingAuditModels("Hero title"),
            ContentGraph());

        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.heading_h1_missing" && issue.Route == "/post/");
        Assert.Contains(result.SeoReport.Issues, issue => issue.Code == "publish.heading_level_skip" && issue.Route == "/post/");
        var outline = Assert.Single(result.PublishReport.Documents).SemanticOutline;
        Assert.Equal(new[] { 1, 3 }, outline.Select(item => item.Level));
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
        var issue = Assert.Single(publishReport.Issues);
        Assert.IsType<PublishAuditIssue>(issue);
        Assert.Equal("publish.rss_missing_route", issue.Code);
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
        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.source_references_missing" && x.Route == "/post/");
        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.entity_summary_missing" && x.Route == "/post/");
    }

    [Fact]
    public void Build_DefaultContentModelDoesNotRequireTrustPresenceFields()
    {
        WriteTrustAuditOutput();

        var report = SeoAuditReportWriter.Build(
            Config(),
            _outputDir,
            TrustAuditIndex(),
            TrustAuditModels(),
            ContentGraphWithPresenceValues(author: null, source: null, originalSource: null, includeEntity: false));

        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.author_missing" && x.Route == "/post/");
        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.source_missing" && x.Route == "/post/");
        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.entity_missing" && x.Route == "/post/");
    }

    [Fact]
    public void Build_ContentModelRequirementsEnableTrustPresenceIssues()
    {
        WriteTrustAuditOutput();
        var config = ConfigWithModelSchema(new ContentModelSchemaConfig
        {
            RequireAuthor = true,
            RequireProvenance = true,
            EntityMappings =
            [
                new EntityMappingConfig { RawKey = "companies", EntityType = "company", Required = true }
            ]
        });

        var report = SeoAuditReportWriter.Build(
            config,
            _outputDir,
            TrustAuditIndex(),
            TrustAuditModels(),
            ContentGraphWithPresenceValues(author: null, source: null, originalSource: null, includeEntity: false));

        Assert.Contains(report.Issues, x => x.Code == "publish.author_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.source_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.entity_missing" && x.Route == "/post/");
        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.source_references_missing");
        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.entity_summary_missing");
    }

    [Theory]
    [InlineData("markdown", null)]
    [InlineData(null, "https://example.com/original")]
    public void Build_ProvenanceRequirementAcceptsSourceOrOriginalSource(string? source, string? originalSource)
    {
        WriteTrustAuditOutput();
        var config = ConfigWithModelSchema(new ContentModelSchemaConfig { RequireProvenance = true });

        var report = SeoAuditReportWriter.Build(
            config,
            _outputDir,
            TrustAuditIndex(),
            TrustAuditModels(),
            ContentGraphWithPresenceValues("Ali", source, originalSource, includeEntity: true));

        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.source_missing" && x.Route == "/post/");
    }

    [Fact]
    public void Build_ReportsJsonFeedManifestAndContentValueGaps()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav><main><article><h1>Post</h1><time datetime="2026-06-05">June 5</time><p>Repeated body.</p></article></main><footer></footer></body>
            </html>
            """);
        WriteOutput("other/index.html", """
            <!doctype html>
            <html>
            <head><title>Other</title><link rel="canonical" href="https://example.com/other/" /></head>
            <body><header></header><nav></nav><main><article><h1>Other</h1><time datetime="2026-06-05">June 5</time><p>Repeated body.</p></article></main><footer></footer></body>
            </html>
            """);
        File.WriteAllText(Path.Combine(_outputDir, "feed.json"), """
            { "version": "https://jsonfeed.org/version/1.1", "items": [] }
            """);
        File.WriteAllText(Path.Combine(_outputDir, "agent-manifest.json"), """
            { "schema": "https://bukit.dev/schemas/agent-manifest.v1.json", "documents": [] }
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/"),
            ["other/index.html"] = Entry("/other/", "other/index.html", "https://example.com/other/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/"),
            ["other/index.html"] = Model("Other", "https://example.com/other/")
        };

        var report = SeoAuditReportWriter.Build(ConfigWithJsonFeed(), _outputDir, index, models, DuplicateContentGraph());

        Assert.Contains(report.Issues, x => x.Code == "publish.json_feed_missing_route" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.manifest_missing_route" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.content_duplicate" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.unique_value_missing" && x.Route == "/post/");
    }

    [Fact]
    public void Build_DoesNotReportContentOnlyGapsForGeneratedListRoutes()
    {
        WriteOutput("tags/index.html", """
            <!doctype html>
            <html>
            <head><title>Tags</title><link rel="canonical" href="https://example.com/tags/" /></head>
            <body><header></header><nav></nav><main><h1>Tags</h1><p>Browse all tags.</p></main><footer></footer></body>
            </html>
            """);
        WriteOutput("blog/archive/index.html", """
            <!doctype html>
            <html>
            <head><title>Archive</title><link rel="canonical" href="https://example.com/blog/archive/" /></head>
            <body><header></header><nav></nav><main><h1>Archive</h1><p>Browse archive.</p></main><footer></footer></body>
            </html>
            """);
        File.WriteAllText(Path.Combine(_outputDir, "search.json"), "[]");
        File.WriteAllText(Path.Combine(_outputDir, "rss.xml"), "<rss><channel></channel></rss>");
        File.WriteAllText(Path.Combine(_outputDir, "agent-manifest.json"), """
            { "schema": "https://bukit.dev/schemas/agent-manifest.v1.json", "documents": [] }
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags/index.html"] = GeneratedEntry("/tags/", "tags/index.html", "https://example.com/tags/", "tags-index", "page"),
            ["blog/archive/index.html"] = GeneratedEntry("/blog/archive/", "blog/archive/index.html", "https://example.com/blog/archive/", "post-archive-index", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags/index.html"] = Model("Tags", "https://example.com/tags/"),
            ["blog/archive/index.html"] = Model("Archive", "https://example.com/blog/archive/")
        };

        var report = SeoAuditReportWriter.Build(ConfigWithRssPostCollection(), _outputDir, index, models, CanonicalContentGraph.Empty);
        // Generated list routes should not report content-quality gaps.
        // Instead of enumerating every possible code, verify that the only reported
        // issues for these routes are structural (feed/manifest/llms) rather than content-quality.
        var contentQualityCodes = new[]
        {
            "publish.author_missing",
            "publish.source_missing",
            "publish.review_status_missing",
            "publish.updated_at_missing",
            "publish.entity_missing",
            "publish.heading_h1_missing",
            "publish.semantic_article_missing",
            "publish.time_missing",
            "publish.search_missing_route",
            "publish.unique_value_missing",
            "publish.rss_missing_route",
            "publish.manifest_missing_route",
            "publish.llms_missing_route",
            "publish.json_feed_missing_route",
            "publish.content_duplicate",
            "publish.output_file_missing",
            "publish.atom_feed_missing_route"
        };
        foreach (var code in contentQualityCodes)
        {
            Assert.DoesNotContain(report.Issues, x => x.Code == code && x.Route == "/tags/");
            Assert.DoesNotContain(report.Issues, x => x.Code == code && x.Route == "/blog/archive/");
        }
    }

    [Fact]
    public void Build_ReportsAtomFeedGapAndInventory()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav><main><article><h1>Post</h1><time datetime="2026-06-05">June 5</time><p>Body content for people and machines.</p></article></main><footer></footer></body>
            </html>
            """);
        Directory.CreateDirectory(Path.Combine(_outputDir, "feed"));
        File.WriteAllText(Path.Combine(_outputDir, "feed", "atom.xml"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom"></feed>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var result = MachineReadabilityTrustAuditBuilder.Build(ConfigWithAtomFeed(), _outputDir, index, models, ContentGraph());

        Assert.Contains(result.SeoReport.Issues, x => x.Code == "publish.atom_feed_missing_route" && x.Route == "/post/");
        var document = Assert.Single(result.PublishReport.Documents);
        Assert.False(document.AtomFeedIncluded);
        Assert.Contains(document.RepresentationKinds, x => x == "atom");
        Assert.DoesNotContain(document.RepresentationKinds, x => x == "feed");
        Assert.Contains(document.Representations, x => x.Kind == "atom" && x.Path == "feed/atom.xml" && x.Url == "/feed/atom.xml");
    }

    [Fact]
    public void Build_FeedAuditOnlyRequiresRoutesInsideCanonicalPublishWindow()
    {
        foreach (var slug in new[] { "oldest", "middle", "newest" })
        {
            WriteOutput($"{slug}/index.html", $"""
                <!doctype html><html><head><title>{slug}</title><link rel="canonical" href="https://example.com/{slug}/" /></head>
                <body><main><article><h1>{slug}</h1><time datetime="2026-06-05">June 5</time><p>Body.</p></article></main></body></html>
                """);
        }
        Directory.CreateDirectory(Path.Combine(_outputDir, "feed"));
        File.WriteAllText(Path.Combine(_outputDir, "rss.xml"), "<rss><channel></channel></rss>");
        File.WriteAllText(Path.Combine(_outputDir, "feed", "atom.xml"), "<feed xmlns=\"http://www.w3.org/2005/Atom\"></feed>");
        File.WriteAllText(Path.Combine(_outputDir, "feed", "feed.json"), "{\"version\":\"https://jsonfeed.org/version/1.1\",\"items\":[]}");

        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var slug in new[] { "oldest", "middle", "newest" })
        {
            var key = $"{slug}/index.html";
            var canonical = slug == "oldest"
                ? "https://example.com/newest/"
                : $"https://example.com/{slug}/";
            index[key] = new SeoIndexEntry(
                new RouteInfo($"/{slug}/", key, "pages/post.html"),
                canonical,
                Robots: null,
                Indexable: true,
                LastModified: DateTimeOffset.Parse("2030-01-01T00:00:00Z"),
                SourceItemId: slug,
                ContentType: "post",
                Collection: "post");
            models[key] = Model(slug, canonical);
        }

        var config = ConfigWithAllFeeds(limit: 2);
        var result = MachineReadabilityTrustAuditBuilder.Build(config, _outputDir, index, models, FeedWindowContentGraph());
        var report = result.SeoReport;

        foreach (var code in new[] { "publish.rss_missing_route", "publish.atom_feed_missing_route", "publish.json_feed_missing_route" })
        {
            Assert.Contains(report.Issues, issue => issue.Code == code && issue.Route == "/newest/");
            Assert.Contains(report.Issues, issue => issue.Code == code && issue.Route == "/middle/");
            Assert.DoesNotContain(report.Issues, issue => issue.Code == code && issue.Route == "/oldest/");
        }
        var oldest = Assert.Single(result.PublishReport.Documents, document => document.RouteUrl == "/oldest/");
        Assert.DoesNotContain(oldest.RepresentationKinds, kind => kind is "feed" or "atom" or "jsonfeed");
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
        TestCleanup.DeleteDirectory(_outputDir, recursive: true);
    }

    private void WriteOutput(string path, string html)
    {
        var fullPath = Path.Combine(_outputDir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, html);
    }

    private void WriteTrustAuditOutput()
        => WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav><main><article><h1>Post</h1><time datetime="2026-06-05">June 5</time><p>Body content.</p></article></main><footer></footer></body>
            </html>
            """);

    private void WriteHeadingAuditOutput(string primaryHtml)
        => WriteOutput("post/index.html", $$"""
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><header></header><nav></nav>{{primaryHtml}}<time datetime="2026-06-05">June 5</time><footer></footer></body>
            </html>
            """);

    private static Dictionary<string, SeoIndexEntry> TrustAuditIndex()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };

    private static Dictionary<string, SeoModel> TrustAuditModels()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

    private static Dictionary<string, SeoModel> HeadingAuditModels(string title)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model(title, "https://example.com/post/", $$"""
                { "@context": "https://schema.org", "@type": "Article", "headline": {{JsonSerializer.Serialize(title)}} }
                """)
        };

    private static SeoIndexEntry Entry(string url, string outputPath, string canonical)
        => new(new RouteInfo(url, outputPath, "pages/post.html"), canonical, Robots: null, Indexable: true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), SourceItemId: "post-1", ContentType: "post", Collection: "post");

    private static SeoIndexEntry GeneratedEntry(string url, string outputPath, string canonical, string sourceItemId, string contentType)
        => new(new RouteInfo(url, outputPath, "pages/tags.html"), canonical, Robots: null, Indexable: true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), IsDerived: true, SourceItemId: sourceItemId, ContentType: contentType);

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
                [new EntityRecord("company", "Bukit", "Bukit product")],
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

    private static CanonicalContentGraph ContentGraphWithPresenceValues(
        string? author,
        string? source,
        string? originalSource,
        bool includeEntity)
    {
        var entities = includeEntity
            ? new[] { new EntityRecord("company", "Bukit", "Bukit product") }
            : Array.Empty<EntityRecord>();
        return new CanonicalContentGraph(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>body</p></article>", "en", []),
                new ContentClassification("post", "post", [], []),
                new ContentOwnership(author, "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), DateTimeOffset.Parse("2026-06-05T01:00:00Z"), null, null),
                new ProvenanceRecord(source, originalSource, [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                entities,
                [],
                [])
        ], entities);
    }

    private static CanonicalContentGraph DuplicateContentGraph()
        => new(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Same", "<article><p>Repeated body.</p></article>", "en", []),
                new ContentClassification("post", "post", [], []),
                new ContentOwnership("Ali", "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), DateTimeOffset.Parse("2026-06-05T01:00:00Z"), null, null),
                new ProvenanceRecord("markdown", "https://example.com/a", [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                [new EntityRecord("company", "Bukit", "Bukit product")],
                [],
                []),
            new ContentRecord(
                new ContentIdentity("post-1", "other", "other", "post", "published"),
                new ContentPresentation("Other", "Same", "<article><p>Repeated body.</p></article>", "en", []),
                new ContentClassification("post", "post", [], []),
                new ContentOwnership("Ali", "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), DateTimeOffset.Parse("2026-06-05T01:00:00Z"), null, null),
                new ProvenanceRecord("markdown", "https://example.com/b", [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                [new EntityRecord("company", "Bukit", "Bukit product")],
                [],
                [])
        ], [new EntityRecord("company", "Bukit", "Bukit product")]);

    private static CanonicalContentGraph FeedWindowContentGraph()
        => new(
        [
            FeedRecord("newest", "2026-06-05T00:00:00Z"),
            FeedRecord("middle", "2026-06-04T00:00:00Z"),
            FeedRecord("oldest", "2026-06-05T00:00:00Z")
        ], []);

    private static ContentRecord FeedRecord(string id, string publishedAt)
        => new(
            new ContentIdentity(id, id, id, "post", "published"),
            new ContentPresentation(id, $"{id} description", "<article><p>body</p></article>", "en", []),
            new ContentClassification("post", "post", [], []),
            new ContentOwnership("Ali", null, null, null),
            new ContentLifecycle(DateTimeOffset.Parse(publishedAt), DateTimeOffset.Parse(publishedAt), null, null),
            new ProvenanceRecord("markdown", null, [], [], "synced"),
            new TrustMetadata(0.9, "approved", []),
            [],
            [],
            []);

    private static AppConfig Config()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com"
            },
            Content = TestContent.Markdown()
        };

    private static AppConfig ConfigWithModelSchema(ContentModelSchemaConfig modelSchema)
        => Config() with
        {
            Content = Config().Content with { ModelSchema = modelSchema }
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
            Content = TestContent.Markdown()
        };

    private static AppConfig ConfigWithJsonFeed()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Feed = new FeedConfig { Formats = ["json"] },
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["post"] = new() { Permalink = "/post/{slug}/", Output = new CollectionOutputConfig { Rss = true } }
                }
            },
            Content = TestContent.Markdown()
        };

    private static AppConfig ConfigWithAtomFeed()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Feed = new FeedConfig { Formats = ["atom"] },
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["post"] = new() { Permalink = "/post/{slug}/", Output = new CollectionOutputConfig { Rss = true } }
                }
            },
            Content = TestContent.Markdown()
        };

    private static AppConfig ConfigWithAllFeeds(int limit)
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Feed = new FeedConfig { Formats = ["rss", "atom", "json"], Limit = limit },
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["post"] = new() { Permalink = "/{slug}/", Output = new CollectionOutputConfig { Rss = true } }
                }
            },
            Content = TestContent.Markdown()
        };
}
