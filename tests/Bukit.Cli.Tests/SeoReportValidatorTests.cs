using System.Text.Json;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoReportValidatorTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static string ValidReportJson() => """
    {
      "schema": "https://bukit.dev/schemas/seo-report.v1.json",
      "schemaVersion": "1.0",
      "generatedAt": "2026-01-01T00:00:00Z",
      "siteName": "test",
      "baseUrl": "https://example.com",
      "routes": [],
      "issues": [],
      "summary": {
        "routeCount": 0,
        "indexableCount": 0,
        "nonIndexableCount": 0,
        "errorCount": 0,
        "warningCount": 0
      }
    }
    """;

    private static string ValidReportWithRoutesJson() => """
    {
      "schema": "https://bukit.dev/schemas/seo-report.v1.json",
      "schemaVersion": "1.0",
      "generatedAt": "2026-01-01T00:00:00Z",
      "siteName": "test",
      "baseUrl": "https://example.com",
      "routes": [
        {
          "url": "/",
          "outputPath": "index.html",
          "title": "Home",
          "description": "Welcome",
          "canonical": "https://example.com/",
          "indexable": true,
          "lastModified": "2026-01-01",
          "sitemapIncluded": true,
          "searchIncluded": true,
          "rssIncluded": false,
          "alternates": [],
          "schemaTypes": ["WebPage"]
        }
      ],
      "issues": [
        {
          "severity": "error",
          "code": "SEO001",
          "message": "Missing description",
          "route": "/"
        }
      ],
      "summary": {
        "routeCount": 1,
        "indexableCount": 1,
        "nonIndexableCount": 0,
        "errorCount": 1,
        "warningCount": 0
      }
    }
    """;

    private static string ValidPublishReportJson() => """
    {
      "schema": "https://bukit.dev/schemas/publish-audit-report.v1.json",
      "schemaVersion": "1.0",
      "generatedAt": "2026-01-01T00:00:00Z",
      "siteName": "test",
      "baseUrl": "/",
      "documents": [
        {
          "routeUrl": "/post/",
          "outputPath": "post/index.html",
          "canonical": "https://example.com/post/",
          "indexable": true,
          "lastModified": "2026-01-01T00:00:00Z",
          "representationKinds": ["html", "semantic-html", "json", "markdown", "llms-full"],
          "representations": [],
          "schemaTypes": [],
          "structuredDataTypes": [],
          "semanticOutline": [],
          "sitemapIncluded": true,
          "searchIncluded": true,
          "rssIncluded": false,
          "atomFeedIncluded": false,
          "jsonFeedIncluded": false,
          "llmsIncluded": true,
          "llmsFullIncluded": true,
          "robotsIncluded": true,
          "manifestIncluded": true
        }
      ],
      "issues": [],
      "summary": {
        "documentCount": 1,
        "indexableCount": 1,
        "nonIndexableCount": 0,
        "errorCount": 0,
        "warningCount": 0
      }
    }
    """;

    private static string ValidGeoReportJson() => """
    {
      "schema": "https://bukit.dev/schemas/geo-report.v1.json",
      "schemaVersion": "1.0",
      "generatedAt": "2026-01-01T00:00:00Z",
      "geoScore": 80,
      "llmsTxtGenerated": true,
      "llmsFullTxtGenerated": false,
      "geoEnhancedCount": 1,
      "geoEnhancedRoutes": [
        {
          "url": "/post/",
          "schemaTypes": ["Article"]
        }
      ]
    }
    """;

    [Fact]
    public void ValidateReportContract_ValidReport_Passes()
    {
        var root = Parse(ValidReportJson());
        AuditReportContractValidator.ValidateReportContract(root, SeoReportValidator.AuditReportContract.SeoOnly);
    }

    [Fact]
    public void SpecializedValidators_ValidateTheirOwnReportContracts()
    {
        SeoAuditReportContractValidator.Validate(Parse(ValidReportJson()));
        PublishAuditReportContractValidator.Validate(Parse(ValidPublishReportJson()));
    }

    [Fact]
    public void ValidateReportContract_PublishReportWithLlmsFullIncluded_Passes()
    {
        AuditReportContractValidator.ValidateReportContract(Parse(ValidPublishReportJson()), SeoReportValidator.AuditReportContract.PublishOnly);
    }

    [Fact]
    public void ValidateReportContract_PublishRepresentationWithoutGeneratedFile_Passes()
    {
        var json = ValidPublishReportJson().Replace(
            "\"representations\": []",
            """
            "representations": [
              {
                "kind": "jsonld",
                "url": "https://example.com/post/",
                "path": "",
                "generated": false,
                "indexable": true
              }
            ]
            """,
            StringComparison.Ordinal);

        AuditReportContractValidator.ValidateReportContract(Parse(json), SeoReportValidator.AuditReportContract.PublishOnly);
    }

    [Fact]
    public void ValidateReportContract_PublishGeneratedRepresentationWithoutPath_Throws()
    {
        var json = ValidPublishReportJson().Replace(
            "\"representations\": []",
            """
            "representations": [
              {
                "kind": "json",
                "url": "/content/post.json",
                "path": "",
                "generated": true,
                "indexable": true
              }
            ]
            """,
            StringComparison.Ordinal);

        var ex = Assert.Throws<InvalidDataException>(() =>
            AuditReportContractValidator.ValidateReportContract(Parse(json), SeoReportValidator.AuditReportContract.PublishOnly));

        Assert.Contains("path must be a non-empty string when generated is true", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateReportContract_PublishReport_WhenSeoOnly_Throws()
    {
        var ex = Assert.Throws<InvalidDataException>(() => AuditReportContractValidator.ValidateReportContract(
            Parse(ValidPublishReportJson()),
            SeoReportValidator.AuditReportContract.SeoOnly));

        Assert.Contains("Expected 'https://bukit.dev/schemas/seo-report.v1.json'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateReportContract_SeoReport_WhenPublishOnly_Throws()
    {
        var ex = Assert.Throws<InvalidDataException>(() => AuditReportContractValidator.ValidateReportContract(
            Parse(ValidReportJson()),
            SeoReportValidator.AuditReportContract.PublishOnly));

        Assert.Contains("Expected 'https://bukit.dev/schemas/publish-audit-report.v1.json'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateReportContract_CompatibleContract_AcceptsBoth()
    {
        AuditReportContractValidator.ValidateReportContract(Parse(ValidReportJson()), SeoReportValidator.AuditReportContract.SeoOrPublish);
        AuditReportContractValidator.ValidateReportContract(Parse(ValidPublishReportJson()), SeoReportValidator.AuditReportContract.SeoOrPublish);
    }

    [Fact]
    public void SeoResolveAuditReportPath_DoesNotFallbackToPublishReport()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-seo-resolve-" + Guid.NewGuid().ToString("N"));
        try
        {
            var reportDir = Path.Combine(root, ".bukit");
            Directory.CreateDirectory(reportDir);
            File.WriteAllText(Path.Combine(reportDir, "publish-audit-report.json"), ValidPublishReportJson());

            Assert.Null(SeoCommand.ResolveAuditReportPath(root));

            var seoPath = Path.Combine(reportDir, "seo-report.json");
            File.WriteAllText(seoPath, ValidReportJson());
            Assert.Equal(seoPath, SeoCommand.ResolveAuditReportPath(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GeoResolveReportPath_DoesNotFallbackToPublishOrSeoReport()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-geo-resolve-" + Guid.NewGuid().ToString("N"));
        try
        {
            var reportDir = Path.Combine(root, ".bukit");
            Directory.CreateDirectory(reportDir);
            File.WriteAllText(Path.Combine(reportDir, "seo-report.json"), ValidReportJson());
            File.WriteAllText(Path.Combine(reportDir, "publish-audit-report.json"), ValidPublishReportJson());

            Assert.Null(GeoCommand.ResolveGeoReportPath(root));

            var geoPath = Path.Combine(reportDir, "geo-report.json");
            File.WriteAllText(geoPath, ValidGeoReportJson());
            Assert.Equal(geoPath, GeoCommand.ResolveGeoReportPath(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GeoValidateReportContract_ValidGeoReport_Passes()
    {
        GeoCommand.ValidateGeoReportContract(Parse(ValidGeoReportJson()));
    }

    [Fact]
    public void GeoValidateReportContract_SeoReport_Throws()
    {
        var ex = Assert.Throws<InvalidDataException>(() => GeoCommand.ValidateGeoReportContract(Parse(ValidReportJson())));

        Assert.Contains("geo-report.v1.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateReportContract_MissingRoutes_Throws()
    {
        var json = """
        {
          "schema": "https://bukit.dev/schemas/seo-report.v1.json",
          "schemaVersion": "1.0",
          "generatedAt": "2026-01-01T00:00:00Z",
          "siteName": "test",
          "baseUrl": "https://example.com",
          "issues": [],
          "summary": {
            "routeCount": 0,
            "indexableCount": 0,
            "nonIndexableCount": 0,
            "errorCount": 0,
            "warningCount": 0
          }
        }
        """;

        var ex = Assert.Throws<InvalidDataException>(() => AuditReportContractValidator.ValidateReportContract(Parse(json), SeoReportValidator.AuditReportContract.SeoOnly));
        Assert.Contains("routes", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateReportContract_InvalidSchemaVersion_Throws()
    {
        var json = """
        {
          "schema": "https://bukit.dev/schemas/seo-report.v1.json",
          "schemaVersion": "2.0",
          "generatedAt": "2026-01-01T00:00:00Z",
          "siteName": "test",
          "baseUrl": "https://example.com",
          "routes": [],
          "issues": [],
          "summary": {
            "routeCount": 0,
            "indexableCount": 0,
            "nonIndexableCount": 0,
            "errorCount": 0,
            "warningCount": 0
          }
        }
        """;

        var ex = Assert.Throws<InvalidDataException>(() => AuditReportContractValidator.ValidateReportContract(Parse(json), SeoReportValidator.AuditReportContract.SeoOnly));
        Assert.Contains("schemaVersion", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadRequiredInt_Valid_Returns() => Assert.Equal(5, SeoReportValidator.ReadRequiredInt(Parse("{\"x\":5}"), "p", "x"));

    [Fact]
    public void ReadRequiredInt_Missing_Throws()
    {
        var ex = Assert.Throws<InvalidDataException>(() => SeoReportValidator.ReadRequiredInt(Parse("{}"), "p", "x"));
        Assert.Contains("p.x must be an integer", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadRequiredString_Valid_Returns()
    {
        var result = SeoReportValidator.ReadRequiredString(Parse("{\"name\":\"hello\"}"), "p", "name");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ReadRequiredBool_Valid_Returns()
    {
        Assert.True(SeoReportValidator.ReadRequiredBool(Parse("{\"flag\":true}"), "p", "flag"));
        Assert.False(SeoReportValidator.ReadRequiredBool(Parse("{\"flag\":false}"), "p", "flag"));
    }

    [Fact]
    public void SeoReportSnapshot_From_ValidReport_ReturnsSnapshot()
    {
        var root = Parse(ValidReportWithRoutesJson());

        var snapshot = AuditReportContractValidator.ReadDiffSnapshot(root);

        Assert.Single(snapshot.Routes);
        Assert.True(snapshot.Routes.ContainsKey("/"));
        Assert.True(snapshot.Routes["/"].Indexable);
        Assert.Single(snapshot.Issues);
        Assert.Equal("error", snapshot.Issues[0].Severity);
        Assert.Equal("SEO001", snapshot.Issues[0].Code);
        Assert.Equal("/", snapshot.Issues[0].Route);
        Assert.Equal("Missing description", snapshot.Issues[0].Message);
    }
}
