using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-seo-command-tests-" + Guid.NewGuid().ToString("N"));

    public SeoCommandTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsOneWhenReportHasErrors()
    {
        WriteReport(1, 0, """
            [
              { "severity": "error", "code": "seo.head_missing", "route": "/", "message": "missing head" }
            ]
            """);

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditStrictReturnsOneWhenReportHasWarnings()
    {
        WriteReport(0, 1, """
            [
              { "severity": "warning", "code": "seo.title_too_long", "route": "/", "message": "title too long" }
            ]
            """);

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root, "--strict" }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsZeroWhenReportHasNoBlockingIssues()
    {
        WriteReport(0, 1, """
            [
              { "severity": "warning", "code": "seo.title_too_long", "route": "/", "message": "title too long" }
            ]
            """);

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditDoesNotReadPublishAuditReportByDefault()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".bukit"));
        WriteReportFile(Path.Combine(_root, ".bukit", "publish-audit-report.json"), 0, 0, "[]", schema: "https://bukit.dev/schemas/publish-audit-report.v1.json");

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReadsPublishAuditReportWhenExplicitReportProvided()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".bukit"));
        var reportPath = Path.Combine(_root, ".bukit", "publish-audit-report.json");
        WriteReportFile(reportPath, 0, 0, "[]", schema: "https://bukit.dev/schemas/publish-audit-report.v1.json");

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root, "--report", reportPath }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task PublishCommand_AuditUsesSameReportContract()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".bukit"));
        WriteReportFile(Path.Combine(_root, ".bukit", "publish-audit-report.json"), 0, 0, "[]", schema: "https://bukit.dev/schemas/publish-audit-report.v1.json");

        var exitCode = await PublishCommand.RunAsync(CliTestHelper.CreateCommand("publish", new[] { "publish", "audit", "--dir", _root }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task PublishCommand_DiffUsesSameBudgetContract()
    {
        var baseline = Path.Combine(_root, "baseline.json");
        var current = Path.Combine(_root, "current.json");
        WriteReportFile(baseline, 0, 0, "[]", schema: "https://bukit.dev/schemas/publish-audit-report.v1.json");
        WriteReportFile(current, 0, 1, """
            [
              { "severity": "warning", "code": "publish.source_missing", "route": "/", "message": "missing source" }
            ]
            """, schema: "https://bukit.dev/schemas/publish-audit-report.v1.json");

        var exitCode = await PublishCommand.RunAsync(CliTestHelper.CreateCommand("publish", new[] { "publish", "diff", "--baseline", baseline, "--current", current, "--fail-on-new-code", "publish.source_missing" }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditAcceptsGeoSummaryFields()
    {
        WriteReport(0, 0, "[]", summaryExtraProperties: """
                "llmsTxtGenerated": true,
                "llmsFullTxtGenerated": false,
                "geoEnhancedCount": 2,
                "geoScore": 75,
            """);

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsTwoWhenSchemaVersionIsUnsupported()
    {
        WriteReport(0, 0, "[]", schemaVersion: "2.0");

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsTwoWhenRoutesAreMissing()
    {
        File.WriteAllText(Path.Combine(_root, "seo-report.json"), """
            {
              "schema": "https://bukit.dev/schemas/seo-report.v1.json",
              "schemaVersion": "1.0",
              "issues": [],
              "summary": {
                "routeCount": 0,
                "indexableCount": 0,
                "nonIndexableCount": 0,
                "errorCount": 0,
                "warningCount": 0
              }
            }
            """);

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsTwoWhenReportHasSchemaExtraProperty()
    {
        WriteReport(0, 0, "[]", extraRootProperty: """
              "unexpected": true,
            """);

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_DiffReturnsOneWhenNewIssueExceedsBudget()
    {
        var baseline = Path.Combine(_root, "baseline.json");
        var current = Path.Combine(_root, "current.json");
        WriteReportFile(baseline, 0, 0, "[]");
        WriteReportFile(current, 1, 0, """
            [
              { "severity": "error", "code": "seo.title_missing", "route": "/", "message": "missing title" }
            ]
            """);

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "diff", "--baseline", baseline, "--current", current, "--max-new-errors", "0" }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_DiffReturnsOneWhenNewIssueCodeIsBlocked()
    {
        var baseline = Path.Combine(_root, "baseline.json");
        var current = Path.Combine(_root, "current.json");
        WriteReportFile(baseline, 0, 0, "[]");
        WriteReportFile(current, 0, 1, """
            [
              { "severity": "warning", "code": "seo.description_missing", "route": "/", "message": "missing description" }
            ]
            """);

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "diff", "--baseline", baseline, "--current", current, "--fail-on-new-code", "seo.description_missing" }));

        Assert.Equal(1, exitCode);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void WriteReport(int errorCount, int warningCount, string issuesJson, string schemaVersion = "1.0", string extraRootProperty = "", string summaryExtraProperties = "")
        => WriteReportFile(Path.Combine(_root, "seo-report.json"), errorCount, warningCount, issuesJson, schemaVersion, extraRootProperty, summaryExtraProperties);

    private static void WriteReportFile(string path, int errorCount, int warningCount, string issuesJson, string schemaVersion = "1.0", string extraRootProperty = "", string summaryExtraProperties = "", string schema = "https://bukit.dev/schemas/seo-report.v1.json")
    {
        if (schema == "https://bukit.dev/schemas/publish-audit-report.v1.json")
        {
            File.WriteAllText(path, $$"""
                {
                  {{extraRootProperty}}
                  "schema": "{{schema}}",
                  "schemaVersion": "{{schemaVersion}}",
                  "generatedAt": "2026-05-14T00:00:00+00:00",
                  "siteName": "Test",
                  "siteUrl": "https://example.com",
                  "baseUrl": "/",
                  "documents": [
                    {
                      "routeUrl": "/",
                      "outputPath": "index.html",
                      "canonical": "https://example.com/",
                      "indexable": true,
                      "lastModified": "2026-05-14T00:00:00+00:00",
                      "contentType": "page",
                      "sourceItemId": null,
                      "title": "Home",
                      "description": "Home description",
                      "language": "en",
                      "author": "Ali",
                      "organization": "Bukit",
                      "source": "markdown",
                      "originalSource": null,
                      "reviewStatus": "approved",
                      "entityNames": [ "Bukit" ],
                      "representationKinds": [ "html", "json", "markdown" ],
                      "schemaTypes": [ "WebPage" ],
                      "sitemapIncluded": true,
                      "searchIncluded": true,
                      "rssIncluded": false
                    }
                  ],
                  "issues": {{issuesJson}},
                  "summary": {
                    "documentCount": 1,
                    "indexableCount": 1,
                    "nonIndexableCount": 0,
                    "errorCount": {{errorCount}},
                    "warningCount": {{warningCount}},
                    "publishIssueCount": {{warningCount + errorCount}},
                    "machineReadabilityIssueCount": {{warningCount}},
                    "trustIssueCount": {{errorCount}},
                    "representationGapCount": 0
                  }
                }
                """);
            return;
        }

        File.WriteAllText(path, $$"""
            {
              {{extraRootProperty}}
              "schema": "{{schema}}",
              "schemaVersion": "{{schemaVersion}}",
              "generatedAt": "2026-05-14T00:00:00+00:00",
              "siteName": "Test",
              "siteUrl": "https://example.com",
              "baseUrl": "/",
              "routes": [
                {
                  "url": "/",
                  "outputPath": "index.html",
                  "title": "Home",
                  "description": "Home description",
                  "canonical": "https://example.com/",
                  "robots": null,
                  "indexable": true,
                  "lastModified": "2026-05-14T00:00:00+00:00",
                  "contentType": "page",
                  "sourceItemId": null,
                  "sitemapIncluded": true,
                  "searchIncluded": true,
                  "rssIncluded": false,
                  "alternates": [],
                  "schemaTypes": [ "WebSite", "WebPage" ]
                }
              ],
              "issues": {{issuesJson}},
              "summary": {
                "routeCount": 1,
                "indexableCount": 1,
                "nonIndexableCount": 0,
                {{summaryExtraProperties}}
                "errorCount": {{errorCount}},
                "warningCount": {{warningCount}},
                "publishIssueCount": {{warningCount + errorCount}},
                "machineReadabilityIssueCount": {{warningCount}},
                "trustIssueCount": {{errorCount}},
                "representationGapCount": 0
              }
            }
            """);
    }
}
