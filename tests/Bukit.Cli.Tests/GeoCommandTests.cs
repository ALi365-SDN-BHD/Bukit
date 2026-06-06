using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class GeoCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-geo-command-tests-" + Guid.NewGuid().ToString("N"));

    public GeoCommandTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task RunAsync_AuditReturnsTwoWhenDirNotFound()
    {
        var exitCode = await GeoCommand.RunAsync(CliTestHelper.CreateCommand("geo", new[] { "geo", "audit", "--dir", Path.Combine(_root, "nonexistent") }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsOneWhenReportNotFound()
    {
        var exitCode = await GeoCommand.RunAsync(CliTestHelper.CreateCommand("geo", new[] { "geo", "audit", "--dir", _root }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsZeroOnSuccess()
    {
        WriteGeoReport(geoEnhancedCount: 3, geoSchemaTypes: new[] { "FAQPage", "HowTo", "WebPage" },
            llmsTxtGenerated: true, llmsFullTxtGenerated: false, geoScore: 75);

        var exitCode = await GeoCommand.RunAsync(CliTestHelper.CreateCommand("geo", new[] { "geo", "audit", "--dir", _root }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsZeroWhenGeoEnhancedIsZero()
    {
        WriteGeoReport(geoEnhancedCount: 0, geoSchemaTypes: new[] { "WebPage", "WebSite" },
            llmsTxtGenerated: false, llmsFullTxtGenerated: false, geoScore: 0);

        var exitCode = await GeoCommand.RunAsync(CliTestHelper.CreateCommand("geo", new[] { "geo", "audit", "--dir", _root }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditDisplaysGeoScore()
    {
        WriteGeoReport(geoEnhancedCount: 2, geoSchemaTypes: new[] { "Article", "Person", "WebPage" },
            llmsTxtGenerated: true, llmsFullTxtGenerated: true, geoScore: 90);

        var exitCode = await GeoCommand.RunAsync(CliTestHelper.CreateCommand("geo", new[] { "geo", "audit", "--dir", _root }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReadsPublishDocumentsSchemaTypes()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".bukit"));
        WritePublishAuditReport(new[] { "Article", "FAQPage", "WebPage" });

        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await GeoCommand.RunAsync(CliTestHelper.CreateCommand("geo", new[] { "geo", "audit", "--dir", _root }));

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains("Geo-enhanced routes: 1", output, StringComparison.Ordinal);
        Assert.Contains("Article", output, StringComparison.Ordinal);
        Assert.Contains("FAQPage", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AuditReportsInvalidAuditReportJson()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".bukit"));
        File.WriteAllText(Path.Combine(_root, ".bukit", "publish-audit-report.json"), "{");

        using var writer = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(writer);
        try
        {
            var exitCode = await GeoCommand.RunAsync(CliTestHelper.CreateCommand("geo", new[] { "geo", "audit", "--dir", _root }));

            Assert.Equal(1, exitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }

        var output = writer.ToString();
        Assert.Contains("Invalid audit report JSON", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Invalid SEO report JSON", output, StringComparison.Ordinal);
    }

    private void WriteGeoReport(int geoEnhancedCount, IReadOnlyList<string> geoSchemaTypes,
        bool llmsTxtGenerated, bool llmsFullTxtGenerated, int geoScore)
    {
        if (llmsTxtGenerated)
        {
            File.WriteAllText(Path.Combine(_root, "llms.txt"), "# Test\n> Description\n\n- [Home](/)\n");
        }

        if (llmsFullTxtGenerated)
        {
            File.WriteAllText(Path.Combine(_root, "llms-full.txt"), "# Test\n\nURL: /\n\nContent\n---\n");
        }

        File.WriteAllText(Path.Combine(_root, "robots.txt"), "User-agent: *\nAllow: /\n");

        var schemaTypesJson = string.Join(", ", geoSchemaTypes.Select(t => $"\"{t}\""));

        File.WriteAllText(Path.Combine(_root, "seo-report.json"), $$"""
            {
              "schema": "https://bukit.dev/schemas/seo-report.v1.json",
              "schemaVersion": "1.0",
              "generatedAt": "2026-05-19T00:00:00+00:00",
              "siteName": "GeoTest",
              "siteUrl": "https://example.com",
              "baseUrl": "/",
              "routes": [
                {
                  "url": "/",
                  "outputPath": "index.html",
                  "title": "Home",
                  "description": "Home page",
                  "canonical": "https://example.com/",
                  "robots": null,
                  "indexable": true,
                  "lastModified": "2026-05-19T00:00:00+00:00",
                  "contentType": "page",
                  "sourceItemId": null,
                  "sitemapIncluded": true,
                  "searchIncluded": true,
                  "rssIncluded": false,
                  "alternates": [],
                  "schemaTypes": [ {{schemaTypesJson}} ]
                }
              ],
              "issues": [],
              "summary": {
                "routeCount": 1,
                "indexableCount": 1,
                "nonIndexableCount": 0,
                "errorCount": 0,
                "warningCount": 0,
                "llmsTxtGenerated": {{(llmsTxtGenerated ? "true" : "false")}},
                "llmsFullTxtGenerated": {{(llmsFullTxtGenerated ? "true" : "false")}},
                "geoEnhancedCount": {{geoEnhancedCount}},
                "geoScore": {{geoScore}}
              }
            }
            """);
    }

    private void WritePublishAuditReport(IReadOnlyList<string> geoSchemaTypes)
    {
        File.WriteAllText(Path.Combine(_root, "llms.txt"), "# Test\n> Description\n\n- [Home](/)\n");
        File.WriteAllText(Path.Combine(_root, "robots.txt"), "User-agent: *\nAllow: /\n");

        var schemaTypesJson = string.Join(", ", geoSchemaTypes.Select(t => $"\"{t}\""));

        File.WriteAllText(Path.Combine(_root, ".bukit", "publish-audit-report.json"), $$"""
            {
              "schema": "https://bukit.dev/schemas/publish-audit-report.v1.json",
              "schemaVersion": "1.0",
              "generatedAt": "2026-06-05T00:00:00+00:00",
              "siteName": "GeoTest",
              "siteUrl": "https://example.com",
              "baseUrl": "/",
              "documents": [
                {
                  "routeUrl": "/",
                  "outputPath": "index.html",
                  "canonical": "https://example.com/",
                  "indexable": true,
                  "lastModified": "2026-06-05T00:00:00+00:00",
                  "contentType": "post",
                  "sourceItemId": "post-1",
                  "title": "Home",
                  "description": "Home page",
                  "language": "en",
                  "author": "Ali",
                  "organization": "Bukit",
                  "source": "markdown",
                  "originalSource": null,
                  "reviewStatus": "approved",
                  "entityNames": [ "Bukit" ],
                  "representationKinds": [ "html", "json", "markdown" ],
                  "schemaTypes": [ {{schemaTypesJson}} ],
                  "sitemapIncluded": true,
                  "searchIncluded": true,
                  "rssIncluded": true
                }
              ],
              "issues": [],
              "summary": {
                "documentCount": 1,
                "indexableCount": 1,
                "nonIndexableCount": 0,
                "errorCount": 0,
                "warningCount": 0,
                "publishIssueCount": 0,
                "machineReadabilityIssueCount": 0,
                "trustIssueCount": 0,
                "representationGapCount": 0
              }
            }
            """);
    }
}
