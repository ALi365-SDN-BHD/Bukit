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
    public void Build_DoesNotReportDuplicateDocumentTitleForMutualHreflangAlternates()
    {
        WriteOutput("en/index.html", "<html><head><title>Shared home</title></head><body></body></html>");
        WriteOutput("ms/index.html", "<html><head><title>Shared home</title></head><body></body></html>");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["en/index.html"] = Entry("/en/", "en/index.html", "https://example.com/en/"),
            ["ms/index.html"] = Entry("/ms/", "ms/index.html", "https://example.com/ms/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["en/index.html"] = Model("Shared home", "https://example.com/en/", "https://example.com/en/", "https://example.com/ms/"),
            ["ms/index.html"] = Model("Shared home", "https://example.com/ms/", "https://example.com/en/", "https://example.com/ms/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.DoesNotContain(report.Issues, issue => issue.Code == "seo.document_title_duplicate");
    }

    [Fact]
    public void Build_OutputFileMissingDoesNotCascadeDocumentTitleIssues()
    {
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["missing/index.html"] = Entry("/missing/", "missing/index.html", "https://example.com/missing/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["missing/index.html"] = Model("Missing", "https://example.com/missing/")
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, issue => issue.Code == "seo.output_file_missing" && issue.Route == "/missing/");
        Assert.DoesNotContain(report.Issues, issue =>
            issue.Route == "/missing/" && issue.Code.StartsWith("seo.document_title_", StringComparison.Ordinal));
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

    [Theory]
    [InlineData("inject", "error")]
    [InlineData("theme", "warning")]
    [InlineData("off", "warning")]
    public void Build_ReportsDocumentTitleMismatchWithModeSpecificSeverity(string renderMode, string severity)
    {
        WriteOutput("a/index.html", """
            <!doctype html><html><head>
            <title>Actual &amp; title</title>
            <link rel="canonical" href="https://example.com/a/" />
            </head><body></body></html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Model("Semantic", "https://example.com/a/") with { DocumentTitle = "Expected title" }
        };

        var report = SeoAuditReportWriter.Build(Config(renderMode), _outputDir, index, models);

        Assert.Contains(report.Issues, issue =>
            issue.Code == "seo.document_title_mismatch" &&
            issue.Route == "/a/" &&
            issue.Severity == severity);
    }

    [Fact]
    public void Build_ReportsMissingMultipleEmptyAndLongDocumentTitles()
    {
        var longTitle = new string('x', 61);
        WriteOutput("missing/index.html", "<html><head></head><body></body></html>");
        WriteOutput("multiple/index.html", "<html><head><title> </title><title>Second</title></head><body></body></html>");
        WriteOutput("long/index.html", $"<html><head><title>{longTitle}</title></head><body></body></html>");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["missing/index.html"] = Entry("/missing/", "missing/index.html", "https://example.com/missing/"),
            ["multiple/index.html"] = Entry("/multiple/", "multiple/index.html", "https://example.com/multiple/"),
            ["long/index.html"] = Entry("/long/", "long/index.html", "https://example.com/long/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["missing/index.html"] = Model("Missing", "https://example.com/missing/"),
            ["multiple/index.html"] = Model("Multiple", "https://example.com/multiple/"),
            ["long/index.html"] = Model(longTitle, "https://example.com/long/") with { DocumentTitle = longTitle }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, issue => issue.Code == "seo.document_title_missing" && issue.Route == "/missing/" && issue.Severity == "error");
        Assert.Contains(report.Issues, issue => issue.Code == "seo.document_title_multiple" && issue.Route == "/multiple/" && issue.Severity == "error");
        Assert.Contains(report.Issues, issue => issue.Code == "seo.document_title_empty" && issue.Route == "/multiple/" && issue.Severity == "error");
        Assert.Contains(report.Issues, issue => issue.Code == "seo.document_title_too_long" && issue.Route == "/long/" && issue.Severity == "warning");
    }

    [Fact]
    public void Build_ReportsDuplicateFinalDocumentTitlesWithoutConflatingSemanticTitles()
    {
        WriteOutput("a/index.html", "<html><head><title>Shared document</title></head><body></body></html>");
        WriteOutput("b/index.html", "<html><head><title>Shared document</title></head><body></body></html>");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/"),
            ["b/index.html"] = Entry("/b/", "b/index.html", "https://example.com/b/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Model("Semantic A", "https://example.com/a/") with { DocumentTitle = "Shared document" },
            ["b/index.html"] = Model("Semantic B", "https://example.com/b/") with { DocumentTitle = "Shared document" }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, issue => issue.Code == "seo.document_title_duplicate" && issue.Route == "/a/");
        Assert.Contains(report.Issues, issue => issue.Code == "seo.document_title_duplicate" && issue.Route == "/b/");
        Assert.DoesNotContain(report.Issues, issue => issue.Code == "seo.title_duplicate");
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
    public void Build_ReportsBreadcrumbListShapeViolations()
    {
        WriteOutput("breadcrumbs-invalid/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["breadcrumbs-invalid/index.html"] = Entry(
                "/breadcrumbs-invalid/",
                "breadcrumbs-invalid/index.html",
                "https://example.com/breadcrumbs-invalid/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["breadcrumbs-invalid/index.html"] = Model(
                "Breadcrumbs invalid",
                "https://example.com/breadcrumbs-invalid/") with
            {
                JsonLd =
                [
                    """{"@context":"https://schema.org","@type":"BreadcrumbList","itemListElement":[{"@type":"Thing","position":2,"name":" ","item":"not-a-url"}]}"""
                ]
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "seo.schema_breadcrumb_item_type_invalid" && x.Severity == "error");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_breadcrumb_position_invalid" && x.Severity == "error");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_breadcrumb_name_missing" && x.Severity == "error");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_breadcrumb_item_url_invalid" && x.Severity == "error");
    }

    [Fact]
    public void Build_ReportsEmptyBreadcrumbListAndWarnsForValidRelativeItemUrl()
    {
        WriteOutput("breadcrumbs-relative/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["breadcrumbs-relative/index.html"] = Entry(
                "/breadcrumbs-relative/",
                "breadcrumbs-relative/index.html",
                "https://example.com/breadcrumbs-relative/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["breadcrumbs-relative/index.html"] = Model(
                "Breadcrumbs relative",
                "https://example.com/breadcrumbs-relative/") with
            {
                JsonLd =
                [
                    """{"@context":"https://schema.org","@type":"BreadcrumbList","itemListElement":[]}""",
                    """{"@context":"https://schema.org","@type":"BreadcrumbList","itemListElement":[{"@type":"ListItem","position":1,"name":"Relative","item":"/breadcrumbs-relative/"}]}"""
                ]
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "seo.schema_breadcrumb_elements_missing" && x.Severity == "error");
        Assert.Contains(report.Issues, x => x.Code == "seo.schema_breadcrumb_item_url_not_absolute" && x.Severity == "warning");
        Assert.DoesNotContain(report.Issues, x => x.Code == "seo.schema_breadcrumb_item_url_invalid");
    }

    [Fact]
    public void Build_AcceptsWellShapedBreadcrumbList()
    {
        WriteOutput("breadcrumbs-valid/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["breadcrumbs-valid/index.html"] = Entry(
                "/breadcrumbs-valid/",
                "breadcrumbs-valid/index.html",
                "https://example.com/breadcrumbs-valid/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["breadcrumbs-valid/index.html"] = Model(
                "Breadcrumbs valid",
                "https://example.com/breadcrumbs-valid/") with
            {
                JsonLd =
                [
                    """{"@context":"https://schema.org","@type":"BreadcrumbList","itemListElement":[{"@type":"ListItem","position":1,"name":"Parent","item":"https://example.com/parent/"},{"@type":"ListItem","position":2,"name":"Current","item":"https://example.com/breadcrumbs-valid/"}]}"""
                ]
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.DoesNotContain(report.Issues, x => x.Code.StartsWith("seo.schema_breadcrumb_", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_DoesNotRequireSearchAction_WhenSearchRouteIsNotDeclared()
    {
        WriteOutput("search-contract/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["search-contract/index.html"] = Entry("/search-contract/", "search-contract/index.html", "https://example.com/search-contract/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["search-contract/index.html"] = new()
            {
                Title = "Search contract",
                Description = "Search contract description",
                Canonical = "https://example.com/search-contract/",
                JsonLd =
                [
                    """{"@context":"https://schema.org","@type":"WebSite","name":"Site","url":"https://example.com"}"""
                ]
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.DoesNotContain(report.Issues, x => x.Code == "seo.schema_website_searchaction_missing");
    }

    [Fact]
    public void Build_RequiresSearchAction_WhenSearchRouteIsDeclaredAndEnabled()
    {
        WriteOutput("search-contract-enabled/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["search-contract-enabled/index.html"] = Entry("/search-contract-enabled/", "search-contract-enabled/index.html", "https://example.com/search-contract-enabled/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["search-contract-enabled/index.html"] = new()
            {
                Title = "Search contract enabled",
                Description = "Search contract enabled description",
                Canonical = "https://example.com/search-contract-enabled/",
                JsonLd =
                [
                    """{"@context":"https://schema.org","@type":"WebSite","name":"Site","url":"https://example.com"}"""
                ]
            }
        };
        var config = Config() with
        {
            Site = Config().Site with
            {
                Search = new SearchDetailConfig { Route = "/search/" }
            }
        };

        var report = SeoAuditReportWriter.Build(config, _outputDir, index, models);

        Assert.Contains(report.Issues, x => x.Code == "seo.schema_website_searchaction_missing");
    }

    [Fact]
    public void Build_ValidatesOrganizationAuthorAcrossArticleSchemaTypes()
    {
        WriteOutput("author-types/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["author-types/index.html"] = Entry("/author-types/", "author-types/index.html", "https://example.com/author-types/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["author-types/index.html"] = new()
            {
                Title = "Author types",
                Description = "Author type validation",
                Canonical = "https://example.com/author-types/",
                JsonLd =
                [
                    """{"@context":"https://schema.org","@type":"BlogPosting","headline":"Org","datePublished":"2026-07-13T00:00:00Z","image":"https://example.com/org.png","author":{"@type":"Organization","name":"Editorial Desk"}}""",
                    """{"@context":"https://schema.org","@type":"Article","headline":"Missing name","datePublished":"2026-07-13T00:00:00Z","image":"https://example.com/article.png","author":{"@type":"Person","name":""}}""",
                    """{"@context":"https://schema.org","@type":"NewsArticle","headline":"Invalid type","datePublished":"2026-07-13T00:00:00Z","image":"https://example.com/news.png","author":{"@type":"Company","name":"Desk"}}"""
                ]
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.DoesNotContain(report.Issues, x =>
            x.Code.StartsWith("seo.schema_blogposting_author_", StringComparison.Ordinal));
        Assert.Contains(report.Issues, x =>
            x.Code == "seo.schema_article_author_name_missing" && x.Severity == "warning");
        Assert.Contains(report.Issues, x =>
            x.Code == "seo.schema_newsarticle_author_type_invalid" && x.Severity == "error");
    }

    [Fact]
    public void Build_GeoScore_CreditsOrganizationArticleAuthor()
    {
        WriteOutput("organization-author/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["organization-author/index.html"] = Entry("/organization-author/", "organization-author/index.html", "https://example.com/organization-author/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["organization-author/index.html"] = new()
            {
                Title = "Organization author",
                Description = "Organization author",
                Canonical = "https://example.com/organization-author/",
                Article = new SeoArticleModel
                {
                    Author = "Editorial Desk",
                    AuthorType = "Organization"
                },
                JsonLd =
                [
                    """{"@context":"https://schema.org","@type":"BlogPosting","headline":"Organization author","datePublished":"2026-07-13T00:00:00Z","image":"https://example.com/org.png","author":{"@type":"Organization","name":"Editorial Desk"}}"""
                ]
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Equal(1, report.Summary!.GeoEnhancedCount);
        Assert.Equal(35, report.Summary.GeoScore);
    }

    [Fact]
    public void Build_GeoScore_DoesNotDefaultMissingNormalizedAuthorTypeToPerson()
    {
        WriteOutput("invalid-author-type/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["invalid-author-type/index.html"] = Entry("/invalid-author-type/", "invalid-author-type/index.html", "https://example.com/invalid-author-type/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["invalid-author-type/index.html"] = new()
            {
                Title = "Invalid author type",
                Description = "Invalid author type",
                Canonical = "https://example.com/invalid-author-type/",
                Article = new SeoArticleModel
                {
                    Author = "Editorial Desk",
                    AuthorType = null
                },
                JsonLd =
                [
                    """{"@context":"https://schema.org","@type":"BlogPosting","headline":"Invalid author type","datePublished":"2026-07-13T00:00:00Z","image":"https://example.com/invalid.png"}"""
                ]
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Equal(1, report.Summary!.GeoEnhancedCount);
        Assert.Equal(25, report.Summary.GeoScore);
    }

    [Fact]
    public void Build_GeoScore_DoesNotTreatSiteOrganizationAsArticleAuthor()
    {
        WriteOutput("site-organization/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["site-organization/index.html"] = Entry("/site-organization/", "site-organization/index.html", "https://example.com/site-organization/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["site-organization/index.html"] = new()
            {
                Title = "Site organization",
                Description = "No article author",
                Canonical = "https://example.com/site-organization/",
                JsonLd =
                [
                    """{"@context":"https://schema.org","@type":"Organization","name":"Site Publisher"}""",
                    """{"@context":"https://schema.org","@type":"BlogPosting","headline":"Site organization","datePublished":"2026-07-13T00:00:00Z","image":"https://example.com/site.png"}"""
                ]
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.Equal(1, report.Summary!.GeoEnhancedCount);
        Assert.Equal(25, report.Summary.GeoScore);
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
        Assert.Contains(report.Issues, x => x.Code == "seo.document_title_missing" && x.Route == "/image/");
        Assert.Contains(report.Issues, x => x.Code == "seo.og_image_too_small" && x.Route == "/image/");
    }

    [Fact]
    public void Build_ReportsExternalOpenGraphAndTwitterImagesAsUnverifiedWithoutFetching()
    {
        WriteOutput("external-image/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["external-image/index.html"] = Entry(
                "/external-image/",
                "external-image/index.html",
                "https://example.com/external-image/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["external-image/index.html"] = new()
            {
                Title = "External image",
                Description = "External image description",
                Canonical = "https://example.com/external-image/",
                Og = new SeoOpenGraphModel { Image = "https://images.invalid/og.png" },
                Twitter = new SeoTwitterModel { Image = "https://images.invalid/twitter.png" }
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        var externalIssues = report.Issues
            .Where(issue => issue.Route == "/external-image/"
                            && issue.Code.EndsWith("_external_unverified", StringComparison.Ordinal))
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "seo.og_image_external_unverified",
                "seo.twitter_image_external_unverified"
            },
            externalIssues.Select(issue => issue.Code));
        Assert.All(externalIssues, issue => Assert.Equal("warning", issue.Severity));
        Assert.All(externalIssues, issue => Assert.Contains("was not fetched", issue.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Build_RecognizesSameSiteSvgOpenGraphImageFromViewBox()
    {
        WriteOutput("svg/index.html");
        var imagePath = Path.Combine(_outputDir, "assets", "og.svg");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        File.WriteAllText(imagePath, """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 630"></svg>""");

        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["svg/index.html"] = Entry("/svg/", "svg/index.html", "https://example.com/svg/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["svg/index.html"] = new()
            {
                Title = "SVG",
                Description = "SVG description",
                Canonical = "https://example.com/svg/",
                Og = new SeoOpenGraphModel { Image = "https://example.com/assets/og.svg" }
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.DoesNotContain(report.Issues, x => x.Code == "seo.og_image_mime_unknown" && x.Route == "/svg/");
        Assert.DoesNotContain(report.Issues, x => x.Code == "seo.og_image_too_small" && x.Route == "/svg/");
    }

    [Fact]
    public void Build_ReportsSvgImageSizeWithoutMimeUnknownWhenDimensionsMissing()
    {
        WriteOutput("svg-missing-size/index.html");
        var imagePath = Path.Combine(_outputDir, "assets", "missing-size.svg");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        File.WriteAllText(imagePath, """<svg xmlns="http://www.w3.org/2000/svg"></svg>""");

        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["svg-missing-size/index.html"] = Entry("/svg-missing-size/", "svg-missing-size/index.html", "https://example.com/svg-missing-size/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["svg-missing-size/index.html"] = new()
            {
                Title = "SVG Missing Size",
                Description = "SVG missing size description",
                Canonical = "https://example.com/svg-missing-size/",
                Og = new SeoOpenGraphModel { Image = "https://example.com/assets/missing-size.svg" }
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models);

        Assert.DoesNotContain(report.Issues, x => x.Code == "seo.og_image_mime_unknown" && x.Route == "/svg-missing-size/");
        Assert.Contains(report.Issues, x => x.Code == "seo.og_image_too_small" && x.Route == "/svg-missing-size/");
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
    public void Build_DoesNotRequireVisibleTimeForEvergreenContent()
    {
        WriteOutput("about/index.html", """
            <!doctype html>
            <html>
            <head><title>About</title><link rel="canonical" href="https://example.com/about/" /></head>
            <body><main><article><h1>About</h1><p>Body</p></article></main></body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["about/index.html"] = new(new RouteInfo("/about/", "about/index.html", "pages/about.html"), "https://example.com/about/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "about-1", "page")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["about/index.html"] = Model("About", "https://example.com/about/")
        };
        var lifecycle = new ContentLifecycle(
            DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
            DateTimeOffset.Parse("2026-06-06T00:00:00Z"), null, null)
        {
            Evergreen = true
        };
        var record = new ContentRecord(
            new ContentIdentity("about-1", "about", "about", "page", "published"),
            new ContentPresentation("About", "About description", "<p>Body</p>", "en", []),
            new ContentClassification("page", "about", [], []),
            new ContentOwnership("Ali", null, null, null),
            lifecycle,
            new ProvenanceRecord("notion", "https://example.com/about/", [], [], null),
            new TrustMetadata(null, "approved", []),
            [new EntityRecord("company", "Bukit", "Bukit company")], [], []);
        var graph = new CanonicalContentGraph([record], record.Entities);

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, graph);

        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.time_missing" && x.Route == "/about/");
        Assert.DoesNotContain(report.Issues, x => x.Code == "publish.updated_at_missing" && x.Route == "/about/");
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

        var config = Config() with
        {
            Content = Config().Content with
            {
                ModelSchema = new ContentModelSchemaConfig
                {
                    RequireAuthor = true,
                    RequireProvenance = true,
                    EntityMappings =
                    [
                        new EntityMappingConfig { RawKey = "companies", EntityType = "company", Required = true }
                    ]
                }
            }
        };
        var report = SeoAuditReportWriter.Build(config, _outputDir, index, models, graph);

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

    [Fact]
    public void Build_AcceptsPublishSafeEntitiesInNotionJsonProjection()
    {
        const string relatedNotionId = "aaaaaaaa-1111-4222-8333-bbbbbbbbbbbb";
        WriteOutput("post/index.html");
        WriteOutput("content/post.md", """
            # Post

            - Route: /post/
            - Language: en
            - Review Status: approved
            """);
        WriteOutput("content/post.json", """
            {
              "id": "post",
              "route": "/post/",
              "canonicalUrlKey": "post",
              "language": "en",
              "reviewStatus": "approved",
              "entities": [{"type":"company","name":"Bukit"}]
            }
            """);
        WriteOutput("agent-manifest.json", """
            {
              "documents": [{
                "id": "post",
                "canonicalId": "post",
                "route": "/post/",
                "language": "en",
                "reviewStatus": "approved",
                "entities": ["Bukit"],
                "representations": []
              }]
            }
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new() { Title = "Post", Canonical = "https://example.com/post/" }
        };
        var record = new ContentRecord(
            new ContentIdentity("post-1", "post", "post", "post", "published"),
            new ContentPresentation("Post", null, null, "en", []),
            new ContentClassification("post", "post", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), null, null, null),
            new ProvenanceRecord("notion", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [new EntityRecord("company", "Bukit"), new EntityRecord("page", relatedNotionId)],
            [],
            []);

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, new CanonicalContentGraph([record], [], [], []));

        Assert.DoesNotContain(report.Issues, issue => issue.Code == "publish.representation_json_mismatch");
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_outputDir, recursive: true);
    }

    [Fact]
    public void Build_ExcludedRoutePresentInLlmsOutput_ReportsLeakWarning()
    {
        WriteOutput("a/index.html");
        File.WriteAllText(Path.Combine(_outputDir, "llms.txt"), "- [A](https://example.com/a/)\n");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Model("A", "https://example.com/a/")
        };
        var documents = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = DocumentWithLlms(new Dictionary<string, object> { ["visibility"] = "exclude" })
        };

        var result = MachineReadabilityTrustAuditBuilder.BuildPublishAuditCore(
            Config(), _outputDir, index, models, documentsByOutputPath: documents);

        Assert.Contains(result.SeoReport.Issues, issue =>
            issue.Code == "publish.llms_excluded_route_present" &&
            issue.Severity == "warning" &&
            issue.Route == "/a/");
    }

    [Fact]
    public void Build_ExcludedRouteAbsentFromLlmsOutput_DoesNotReportLeak()
    {
        WriteOutput("a/index.html");
        File.WriteAllText(Path.Combine(_outputDir, "llms.txt"), "- [Other](https://example.com/other/)\n");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Entry("/a/", "a/index.html", "https://example.com/a/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = Model("A", "https://example.com/a/")
        };
        var documents = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["a/index.html"] = DocumentWithLlms(new Dictionary<string, object> { ["visibility"] = "exclude" })
        };

        var result = MachineReadabilityTrustAuditBuilder.BuildPublishAuditCore(
            Config(), _outputDir, index, models, documentsByOutputPath: documents);

        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.llms_excluded_route_present");
    }

    [Fact]
    public void Build_IncludeOnNonIndexableRoute_WarnsAndRouteStaysAbsent()
    {
        WriteOutput("hidden/index.html");
        File.WriteAllText(Path.Combine(_outputDir, "llms.txt"), "- [Other](https://example.com/other/)\n");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["hidden/index.html"] = new SeoIndexEntry(
                new RouteInfo("/hidden/", "hidden/index.html", "pages/page.html"),
                "https://example.com/hidden/", Robots: "noindex", Indexable: false,
                DateTimeOffset.UtcNow, SourceItemId: null, ContentType: "page")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["hidden/index.html"] = Model("Hidden", "https://example.com/hidden/")
        };
        var documents = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["hidden/index.html"] = DocumentWithLlms(new Dictionary<string, object> { ["visibility"] = "include" })
        };

        var result = MachineReadabilityTrustAuditBuilder.BuildPublishAuditCore(
            Config(), _outputDir, index, models, documentsByOutputPath: documents);

        Assert.Contains(result.SeoReport.Issues, issue =>
            issue.Code == "geo.llms_include_nonindexable" &&
            issue.Severity == "warning" &&
            issue.Route == "/hidden/");
        Assert.DoesNotContain(result.SeoReport.Issues, issue => issue.Code == "publish.llms_excluded_route_present");
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
            DocumentTitle = title,
            Description = title + " description",
            Canonical = canonical,
            Alternates = alternates.Select(href => new SeoAlternateModel(href.EndsWith("/en/", StringComparison.Ordinal) ? "en" : "ms", href)).ToArray()
        };

    private static AppConfig Config(string renderMode = "inject")
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Seo = new SeoConfig { RenderMode = renderMode }
            },
            Content = TestContent.Markdown()
        };

}
