using Bukit.Cli;
using Bukit.Cli.Commands;
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

        var exitCode = await SeoCommand.RunAsync(new ArgReader(new[] { "seo", "audit", "--dir", _root }));

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

        var exitCode = await SeoCommand.RunAsync(new ArgReader(new[] { "seo", "audit", "--dir", _root, "--strict" }));

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

        var exitCode = await SeoCommand.RunAsync(new ArgReader(new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsTwoWhenSchemaVersionIsUnsupported()
    {
        WriteReport(0, 0, "[]", schemaVersion: "2.0");

        var exitCode = await SeoCommand.RunAsync(new ArgReader(new[] { "seo", "audit", "--dir", _root }));

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

        var exitCode = await SeoCommand.RunAsync(new ArgReader(new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(2, exitCode);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void WriteReport(int errorCount, int warningCount, string issuesJson, string schemaVersion = "1.0")
    {
        File.WriteAllText(Path.Combine(_root, "seo-report.json"), $$"""
            {
              "schema": "https://bukit.dev/schemas/seo-report.v1.json",
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
                  "lastModified": null,
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
                "errorCount": {{errorCount}},
                "warningCount": {{warningCount}}
              }
            }
            """);
    }
}
