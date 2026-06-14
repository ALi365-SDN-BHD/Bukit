using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class GeoCommandTests : IDisposable
{
    private readonly string _tempDir;

    public GeoCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-geo-command-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_Audit_WithValidReport_PrintsResourcesAndSchemaTypes()
    {
        var distDir = Path.Combine(_tempDir, "dist");
        var reportDir = Path.Combine(distDir, ".bukit");
        Directory.CreateDirectory(reportDir);
        File.WriteAllText(Path.Combine(reportDir, "geo-report.json"), ValidGeoReportJson(
            geoEnhancedCount: 2,
            llmsTxtGenerated: true,
            llmsFullTxtGenerated: true,
            routesJson: """
            [
              {
                "url": "/post/",
                "schemaTypes": ["FAQPage", "Article"]
              }
            ]
            """));
        File.WriteAllText(Path.Combine(distDir, "llms.txt"), "ok");
        File.WriteAllText(Path.Combine(distDir, "llms-full.txt"), "ok");
        File.WriteAllText(Path.Combine(distDir, "robots.txt"), "User-agent: *");

        var result = await InvokeAsync(() => GeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--dir"] = distDir },
            ["audit"])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("=== GEO Audit ===", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("llms.txt: present", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("llms-full.txt: present", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("robots.txt: present", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("Schema types: Article, FAQPage", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("GEO Score: 80/100", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task RunAsync_Audit_WhenGeoEnhancementsMissing_PrintsRecommendation()
    {
        var distDir = Path.Combine(_tempDir, "dist-empty");
        var reportDir = Path.Combine(distDir, ".bukit");
        Directory.CreateDirectory(reportDir);
        File.WriteAllText(Path.Combine(reportDir, "geo-report.json"), ValidGeoReportJson(
            geoEnhancedCount: 0,
            llmsTxtGenerated: false,
            llmsFullTxtGenerated: false,
            routesJson: "[]"));

        var result = await InvokeAsync(() => GeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--dir"] = distDir },
            ["audit"])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Recommendation: Enable site.seo.geo.llmsTxt to generate llms.txt.", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("Recommendation: Use geo.schema_type, geo.faq, or geo.steps in content front matter.", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Audit_WithInvalidJson_ReturnsOne()
    {
        var distDir = Path.Combine(_tempDir, "dist-invalid");
        var reportDir = Path.Combine(distDir, ".bukit");
        Directory.CreateDirectory(reportDir);
        File.WriteAllText(Path.Combine(reportDir, "geo-report.json"), "{ invalid");

        var result = await InvokeAsync(() => GeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--dir"] = distDir },
            ["audit"])));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Invalid GEO report JSON:", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenOutputDirectoryMissing_ReturnsTwo()
    {
        var result = await InvokeAsync(() => GeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--dir"] = Path.Combine(_tempDir, "missing") },
            ["audit"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Output directory not found:", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_PrintsUsageAndReturnsTwo()
    {
        var result = await InvokeAsync(() => GeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>(),
            ["verify"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: bukit geo audit [--dir dist]", result.StdErr, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(Func<Task<int>> action)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await action();
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static string ValidGeoReportJson(
        int geoEnhancedCount,
        bool llmsTxtGenerated,
        bool llmsFullTxtGenerated,
        string routesJson) => $$"""
        {
          "schema": "https://bukit.dev/schemas/geo-report.v1.json",
          "schemaVersion": "1.0",
          "generatedAt": "2026-01-01T00:00:00Z",
          "geoScore": 80,
          "llmsTxtGenerated": {{llmsTxtGenerated.ToString().ToLowerInvariant()}},
          "llmsFullTxtGenerated": {{llmsFullTxtGenerated.ToString().ToLowerInvariant()}},
          "geoEnhancedCount": {{geoEnhancedCount}},
          "geoEnhancedRoutes": {{routesJson}}
        }
        """;
}
