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

    [Fact]
    public void ValidateReportContract_ValidReport_Passes()
    {
        var root = Parse(ValidReportJson());
        SeoReportValidator.ValidateReportContract(root);
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

        var ex = Assert.Throws<InvalidDataException>(() => SeoReportValidator.ValidateReportContract(Parse(json)));
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

        var ex = Assert.Throws<InvalidDataException>(() => SeoReportValidator.ValidateReportContract(Parse(json)));
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

        var snapshot = SeoReportValidator.SeoReportSnapshot.From(root);

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
