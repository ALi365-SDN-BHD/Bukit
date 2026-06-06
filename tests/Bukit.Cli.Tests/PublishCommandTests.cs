using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class PublishCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-publish-command-tests-" + Guid.NewGuid().ToString("N"));

    public PublishCommandTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task RunAsync_AuditReadsPublishAuditReportByDefault()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".bukit"));
        WritePublishReport(Path.Combine(_root, ".bukit", "publish-audit-report.json"), 0, 0, "[]");

        var exitCode = await PublishCommand.RunAsync(CliTestHelper.CreateCommand("publish", new[] { "publish", "audit", "--dir", _root }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditDoesNotFallBackToSeoReportByDefault()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".bukit"));
        WriteSeoReport(Path.Combine(_root, ".bukit", "seo-report.json"), 0, 0, "[]");

        var exitCode = await PublishCommand.RunAsync(CliTestHelper.CreateCommand("publish", new[] { "publish", "audit", "--dir", _root }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task SeoCommand_AuditPrefersPublishReportWhenBothReportsExist()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".bukit"));
        WriteSeoReport(Path.Combine(_root, ".bukit", "seo-report.json"), 0, 0, "[]");
        WritePublishReport(Path.Combine(_root, ".bukit", "publish-audit-report.json"), 1, 0, """
            [
              { "severity": "error", "code": "publish.source_missing", "route": "/", "message": "missing source" }
            ]
            """);

        var exitCode = await SeoCommand.RunAsync(CliTestHelper.CreateCommand("seo", new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_DiffReadsPublishDocumentsContract()
    {
        var baseline = Path.Combine(_root, "baseline.json");
        var current = Path.Combine(_root, "current.json");
        WritePublishReport(baseline, 0, 0, "[]");
        WritePublishReport(current, 0, 1, """
            [
              { "severity": "warning", "code": "publish.source_missing", "route": "/", "message": "missing source" }
            ]
            """);

        var exitCode = await PublishCommand.RunAsync(CliTestHelper.CreateCommand("publish", new[] { "publish", "diff", "--baseline", baseline, "--current", current, "--fail-on-new-code", "publish.source_missing" }));

        Assert.Equal(1, exitCode);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private static void WritePublishReport(string path, int errorCount, int warningCount, string issuesJson)
    {
        File.WriteAllText(path, $$"""
            {
              "schema": "https://bukit.dev/schemas/publish-audit-report.v1.json",
              "schemaVersion": "1.0",
              "generatedAt": "2026-06-05T00:00:00+00:00",
              "siteName": "Test",
              "siteUrl": "https://example.com",
              "baseUrl": "/",
              "documents": [
                {
                  "routeUrl": "/",
                  "outputPath": "index.html",
                  "canonical": "https://example.com/",
                  "indexable": true,
                  "lastModified": "2026-06-05T00:00:00+00:00",
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
    }

    private static void WriteSeoReport(string path, int errorCount, int warningCount, string issuesJson)
    {
        File.WriteAllText(path, $$"""
            {
              "schema": "https://bukit.dev/schemas/seo-report.v1.json",
              "schemaVersion": "1.0",
              "generatedAt": "2026-06-05T00:00:00+00:00",
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
                  "lastModified": "2026-06-05T00:00:00+00:00",
                  "contentType": "page",
                  "sourceItemId": null,
                  "sitemapIncluded": true,
                  "searchIncluded": true,
                  "rssIncluded": false,
                  "alternates": [],
                  "schemaTypes": [ "WebPage" ]
                }
              ],
              "issues": {{issuesJson}},
              "summary": {
                "routeCount": 1,
                "indexableCount": 1,
                "nonIndexableCount": 0,
                "errorCount": {{errorCount}},
                "warningCount": {{warningCount}}
              }
            }
            """);
    }
}
