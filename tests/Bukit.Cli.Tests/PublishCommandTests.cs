using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class PublishCommandTests : IDisposable
{
    private readonly string _tempDir;

    public PublishCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-publish-command-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void ResolveAuditReportPath_WhenFileExists_ReturnsPath()
    {
        var reportDir = Path.Combine(_tempDir, "dist", ".bukit");
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "publish-audit-report.json");
        File.WriteAllText(reportPath, ValidPublishReportJson());

        var resolved = PublishCommand.ResolveAuditReportPath(Path.Combine(_tempDir, "dist"));

        Assert.Equal(reportPath, resolved);
    }

    [Fact]
    public async Task RunAsync_Audit_WithExplicitValidReport_ReturnsZero()
    {
        var reportPath = Path.Combine(_tempDir, "publish-audit-report.json");
        File.WriteAllText(reportPath, ValidPublishReportJson());

        var result = await InvokeAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--report"] = reportPath },
            ["audit"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Publish audit: routes=1 errors=0 warnings=0", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Audit_ExternalWithPublishReport_AuditsDocuments()
    {
        var dist = Path.Combine(_tempDir, "dist");
        var bookkeeping = Path.Combine(dist, ".bukit");
        Directory.CreateDirectory(Path.Combine(dist, "post"));
        Directory.CreateDirectory(bookkeeping);
        File.WriteAllText(Path.Combine(dist, "post", "index.html"),
            "<html><body><a href=\"http://127.0.0.1:9/about\">About</a></body></html>");
        File.WriteAllText(Path.Combine(dist, ".bukit-build-state.json"),
            "{\"version\":1,\"status\":\"completed\"}");
        var reportPath = Path.Combine(bookkeeping, "publish-audit-report.json");
        File.WriteAllText(reportPath, ValidPublishReportJson("http://127.0.0.1:9/post/"));

        var result = await InvokeAsync(new CliBoundCommand(
            new Dictionary<string, string?>
            {
                ["--dir"] = dist,
                ["--report"] = reportPath,
                ["--external"] = "true"
            },
            ["audit"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("external error seo.external_fetch_failed - canonical /post/", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("external warning seo.external_fetch_failed - link", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:9/about", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Diff_MissingBaselineAndCurrent_ReturnsTwo()
    {
        var result = await InvokeAsync(new CliBoundCommand(
            new Dictionary<string, string?>(),
            ["diff"]));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: bukit publish diff --baseline old-report.json --current new-report.json", result.StdErr, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(CliBoundCommand command)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await PublishCommand.RunAsync(command);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static string ValidPublishReportJson(string canonical = "https://example.com/post/") => """
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
              "canonical": "__CANONICAL__",
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
        """.Replace("__CANONICAL__", canonical, StringComparison.Ordinal);
}
