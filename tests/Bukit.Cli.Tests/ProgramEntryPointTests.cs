using System.Reflection;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class ProgramEntryPointTests : IDisposable
{
    private readonly string _tempDir;

    public ProgramEntryPointTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-program-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Main_NoArgs_PrintsHelpAndReturnsZero()
    {
        var result = await InvokeEntryPointAsync([]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: bukit <command> [options]", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_Version_PrintsVersionAndRuntime()
    {
        var result = await InvokeEntryPointAsync(["version"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("bukit ", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("runtime: native-aot", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_VersionHelp_PrintsCommandUsage()
    {
        var result = await InvokeEntryPointAsync(["version", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("bukit version", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_DevHelp_PrintsLiveReloadDescription()
    {
        var result = await InvokeEntryPointAsync(["dev", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("bukit dev", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("LiveReload", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("HMR", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_UnknownCommand_WithJsonLogFormat_PrintsJsonDiagnostic()
    {
        var result = await InvokeEntryPointAsync(["missing-command", "--log-format=json"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("\"code\": \"unknown-command\"", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("\"command\": \"missing-command\"", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_BuildWithInvalidIntegerOption_ReturnsTwoAndPrintsUsage()
    {
        var result = await InvokeEntryPointAsync(["build", "--jobs", "NaN"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage:", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("bukit build", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_PublishAudit_WithoutReport_ReturnsOne()
    {
        var outputDir = Path.Combine(_tempDir, "dist");
        Directory.CreateDirectory(outputDir);

        var result = await InvokeEntryPointAsync(["publish", "audit", "--dir", outputDir]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Publish report not found under", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_PublishDiff_WithoutBaselineOrCurrent_ReturnsTwo()
    {
        var result = await InvokeEntryPointAsync(["publish", "diff"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: bukit publish diff --baseline old-report.json --current new-report.json", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_SeoAudit_WithValidReport_ReturnsZero()
    {
        var outputDir = Path.Combine(_tempDir, "seo-dist");
        var reportDir = Path.Combine(outputDir, ".bukit");
        Directory.CreateDirectory(reportDir);
        File.WriteAllText(Path.Combine(reportDir, "seo-report.json"), ValidSeoReportJson());

        var result = await InvokeEntryPointAsync(["seo", "audit", "--dir", outputDir]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SEO audit: routes=1 errors=0 warnings=0", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_GeoAudit_WithValidReport_ReturnsZero()
    {
        var outputDir = Path.Combine(_tempDir, "geo-dist");
        var reportDir = Path.Combine(outputDir, ".bukit");
        Directory.CreateDirectory(reportDir);
        File.WriteAllText(Path.Combine(reportDir, "geo-report.json"), ValidGeoReportJson());

        var result = await InvokeEntryPointAsync(["geo", "audit", "--dir", outputDir]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("=== GEO Audit ===", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("Schema types: Article", result.StdOut, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeEntryPointAsync(string[] args)
    {
        var entryPoint = typeof(VersionCommand).Assembly.EntryPoint ?? throw new InvalidOperationException("Missing Bukit.Cli entry point.");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var result = entryPoint.Invoke(null, [args]);
            var exitCode = result switch
            {
                Task<int> task => await task,
                Task task => await AwaitAndReturnZeroAsync(task),
                int code => code,
                _ => throw new InvalidOperationException($"Unsupported entry point return type: {result?.GetType().FullName ?? "null"}")
            };

            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static async Task<int> AwaitAndReturnZeroAsync(Task task)
    {
        await task;
        return 0;
    }

    private static string ValidSeoReportJson() => """
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
          "issues": [],
          "summary": {
            "routeCount": 1,
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
}
