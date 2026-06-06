using System.Linq;
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
    public void Build_ReportsMissingSemanticMainAndArticle()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body><div><p>Body</p></div></body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "publish.semantic_main_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.semantic_article_missing" && x.Route == "/post/");
    }

    [Fact]
    public void Build_ReportsImagesMissingAltText()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body>
              <main>
                <article>
                  <img src="/a.png">
                  <img src="/b.png" alt="Described">
                  <img src="/c.png" alt=''>
                </article>
              </main>
            </body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "publish.image_alt_missing" && x.Route == "/post/");
    }

    [Fact]
    public void Build_ReportsMissingH1AndHeadingLevelSkips()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body>
              <main>
                <article>
                  <h2>Section</h2>
                  <h4>Skipped</h4>
                  <p>Body</p>
                </article>
              </main>
            </body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "publish.heading_h1_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.heading_level_skip" && x.Route == "/post/");
    }

    [Fact]
    public void Build_ReportsMissingTimeElementForDatedContent()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body>
              <main>
                <article>
                  <h1>Post</h1>
                  <p>Body</p>
                </article>
              </main>
            </body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };
        var graph = new CanonicalContentGraph(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>body</p></article>", "en", []),
                new ContentClassification("post", "post", [], []),
                new ContentOwnership("Ali", null, null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), DateTimeOffset.Parse("2026-06-06T00:00:00Z"), null, null),
                new ProvenanceRecord("notion", null, [], [], null),
                new TrustMetadata(null, "approved", []),
                [new EntityRecord("company", "Bukit")],
                [],
                [])
        ], [new EntityRecord("company", "Bukit")]);

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, graph);

        Assert.Contains(report.Issues, x => x.Code == "publish.time_missing" && x.Route == "/post/");
    }

    [Fact]
    public void Build_ReportsInitialHtmlUnreadableWhenMainContentIsScriptShell()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body>
              <main>
                <article>
                  <script type="application/json">{}</script>
                </article>
              </main>
            </body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "publish.initial_html_unreadable" && x.Route == "/post/");
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

    [Fact]
    public void Write_WritesPublishAuditReportButNotAgentManifest()
    {
        WriteOutput("a/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = new()
            {
                Title = "A",
                Description = "A description",
                Canonical = "https://example.com/a/",
                Article = new SeoArticleModel
                {
                    Author = "Ali"
                }
            }
        };

        SeoAuditReportWriter.Write(Config(), _outputDir, index, models, new ConsoleLogger(LogLevel.Error));

        Assert.True(File.Exists(Path.Combine(_outputDir, ".bukit", "publish-audit-report.json")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "agent-manifest.json")));
    }

    [Fact]
    public void Build_ReportsMachineReadabilityAndTrustGaps()
    {
        WriteOutput("post/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new()
            {
                Title = "Post",
                Description = "Post description",
                Canonical = "https://example.com/post/"
            }
        };
        var graph = new CanonicalContentGraph(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>body</p></article>", "en", []),
                new ContentClassification("post", "post", [], []),
                new ContentOwnership(null, null, null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), null, null, null),
                new ProvenanceRecord(null, null, [], [], null),
                new TrustMetadata(null, "", []),
                [],
                [],
                [])
        ], []);

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, graph);

        Assert.Contains(report.Issues, x => x.Code == "publish.author_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.source_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.review_status_missing" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.entity_missing" && x.Route == "/post/");
    }

    [Fact]
    public void Build_EnrichesRouteWithCanonicalContentMetadata()
    {
        WriteOutput("post/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new()
            {
                Title = "Post",
                Description = "Post description",
                Canonical = "https://example.com/post/"
            }
        };
        var graph = new CanonicalContentGraph(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>body</p></article>", "ms", []),
                new ContentClassification("post", "post", [], ["bukit"]),
                new ContentOwnership("Ali", "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), null, null, null),
                new ProvenanceRecord("notion", "https://example.com/original", [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                [new EntityRecord("company", "Bukit")],
                [],
                [])
        ], [new EntityRecord("company", "Bukit")]);

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, graph);
        var route = Assert.Single(report.Routes);

        Assert.Equal("ms", route.Language);
        Assert.Equal("Ali", route.Author);
        Assert.Equal("Bukit", route.Organization);
        Assert.Equal("notion", route.Source);
        Assert.Equal("approved", route.ReviewStatus);
        Assert.Contains("Bukit", route.EntityNames!);
        Assert.Contains("json", route.RepresentationKinds!);
    }

    [Fact]
    public void Build_ReportsMissingProjectionFilesForDeclaredRepresentations()
    {
        WriteOutput("post/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new()
            {
                Title = "Post",
                Description = "Post description",
                Canonical = "https://example.com/post/"
            }
        };
        var graph = new CanonicalContentGraph(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>body</p></article>", "en", []),
                new ContentClassification("post", "post", [], []),
                new ContentOwnership("Ali", "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), DateTimeOffset.Parse("2026-06-06T00:00:00Z"), null, null),
                new ProvenanceRecord("notion", "https://example.com/original", [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                [new EntityRecord("company", "Bukit")],
                [],
                [])
        ], [new EntityRecord("company", "Bukit")]);

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, graph);

        Assert.Contains(report.Issues, x => x.Code == "publish.representation_file_missing" && x.Route == "/post/" && x.Message.Contains("content/post.json", StringComparison.Ordinal));
        Assert.Contains(report.Issues, x => x.Code == "publish.representation_file_missing" && x.Route == "/post/" && x.Message.Contains("content/post.md", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ReportsProjectionContentMismatches()
    {
        WriteOutput("post/index.html");
        WriteOutput("content/post.md", """
            # Post

            - Route: /post/
            - Language: ms
            - Review Status: draft
            """);
        WriteOutput("content/post.json", """
            {
              "id": "post-1",
              "route": "/wrong/",
              "canonicalUrlKey": "post",
              "language": "ms",
              "reviewStatus": "draft",
              "source": "manual",
              "entities": []
            }
            """);
        WriteOutput("agent-manifest.json", """
            {
              "schema": "https://bukit.dev/schemas/agent-manifest.v1.json",
              "schemaVersion": "1.0",
              "generatedAt": "2026-06-06T00:00:00+00:00",
              "documents": [
                {
                  "id": "post-1",
                  "canonicalId": "post",
                  "route": "/post/",
                  "language": "ms",
                  "reviewStatus": "draft",
                  "source": "manual",
                  "entities": [],
                  "representations": []
                }
              ]
            }
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new()
            {
                Title = "Post",
                Description = "Post description",
                Canonical = "https://example.com/post/"
            }
        };
        var graph = new CanonicalContentGraph(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>body</p></article>", "en", []),
                new ContentClassification("post", "post", [], []),
                new ContentOwnership("Ali", "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), DateTimeOffset.Parse("2026-06-06T00:00:00Z"), null, null),
                new ProvenanceRecord("notion", "https://example.com/original", [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                [new EntityRecord("company", "Bukit")],
                [],
                [])
        ], [new EntityRecord("company", "Bukit")]);

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, graph);

        Assert.Contains(report.Issues, x => x.Code == "publish.representation_json_mismatch" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.representation_markdown_mismatch" && x.Route == "/post/");
        Assert.Contains(report.Issues, x => x.Code == "publish.manifest_mismatch" && x.Route == "/post/");
        Assert.True(report.Summary.RepresentationGapCount >= 3);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_outputDir, recursive: true);
    }

    private void WriteOutput(string path)
    {
        var fullPath = Path.Combine(_outputDir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "<!doctype html><html><head></head><body></body></html>");
    }

    private void WriteOutput(string path, string html)
    {
        var fullPath = Path.Combine(_outputDir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, html);
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
