using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class SeoCommandTests : IDisposable
{
    private readonly string _tempDir;

    public SeoCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-seo-command-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_Audit_WithResolvedReportAndStrictWarnings_ReturnsOne()
    {
        var distDir = Path.Combine(_tempDir, "dist");
        var reportDir = Path.Combine(distDir, ".bukit");
        Directory.CreateDirectory(reportDir);
        File.WriteAllText(Path.Combine(reportDir, "seo-report.json"), ValidSeoReportJson(
            issuesJson: """
            [
              {
                "severity": "warning",
                "code": "SEO201",
                "message": "Missing alternate",
                "route": "/"
              }
            ]
            """,
            warningCount: 1,
            includeSummaryBuckets: true));

        var result = await InvokeAsync(() => SeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--dir"] = distDir, ["--strict"] = string.Empty },
            ["audit"])));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("SEO audit: routes=1 errors=0 warnings=1", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("SEO summary: publishIssues=2 machineReadability=3 trust=4 representationGaps=5", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("warning SEO201 / Missing alternate", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task AuditAsync_GroupsSeoPublishAndGeoIssues()
    {
        var reportPath = Path.Combine(_tempDir, "mixed-seo-report.json");
        File.WriteAllText(reportPath, ValidSeoReportJson(
            issuesJson: """
            [
              {
                "severity": "error",
                "code": "seo.title_missing",
                "message": "Missing title",
                "route": "/"
              },
              {
                "severity": "warning",
                "code": "publish.author_missing",
                "message": "Missing author",
                "route": "/post/"
              },
              {
                "severity": "warning",
                "code": "geo.llms_txt_missing",
                "message": "Missing llms.txt",
                "route": "-"
              }
            ]
            """,
            errorCount: 1,
            warningCount: 2));

        var result = await InvokeAsync(() => SeoCommand.AuditAsync(
            reportPath,
            _tempDir,
            strict: false,
            external: false,
            label: "SEO"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("SEO issues by group:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("  seo: errors=1 warnings=0", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("  publish: errors=0 warnings=1", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("  geo: errors=0 warnings=1", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("=== SEO Issues ===", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("=== Publish Issues ===", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("=== GEO Issues ===", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("warning publish.author_missing /post/ Missing author", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Audit_WhenReportMissing_ReturnsOne()
    {
        var distDir = Path.Combine(_tempDir, "dist");
        Directory.CreateDirectory(distDir);

        var result = await InvokeAsync(() => SeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--dir"] = distDir },
            ["audit"])));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("SEO report not found under", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Diff_WithArgumentsAndNoChanges_ReturnsZero()
    {
        var baselinePath = Path.Combine(_tempDir, "baseline.json");
        var currentPath = Path.Combine(_tempDir, "current.json");
        var report = ValidSeoReportJson();
        File.WriteAllText(baselinePath, report);
        File.WriteAllText(currentPath, report);

        var result = await InvokeAsync(() => SeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>(),
            ["diff", baselinePath, currentPath])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SEO diff: newIssues=0 newErrors=0 newWarnings=0", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_PrintsUsageAndReturnsTwo()
    {
        var result = await InvokeAsync(() => SeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>(),
            ["verify"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: bukit seo audit", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("bukit seo diff", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditAsync_InvalidJson_ReturnsTwo()
    {
        var reportPath = Path.Combine(_tempDir, "invalid-seo-report.json");
        File.WriteAllText(reportPath, "{ this-is-not-json");

        var result = await InvokeAsync(() => SeoCommand.AuditAsync(
            reportPath,
            _tempDir,
            strict: false,
            external: false,
            label: "SEO"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid SEO report JSON:", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_WithMissingFile_ReturnsTwo()
    {
        var reportPath = Path.Combine(_tempDir, "missing-seo-report.json");

        var result = Invoke(() => SeoCommand.Audit(reportPath, strict: false));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("SEO report not found:", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_WhenNewIssuesExceedBudgetsAndCodeGate_ReturnsOne()
    {
        var baselinePath = Path.Combine(_tempDir, "baseline-budget.json");
        var currentPath = Path.Combine(_tempDir, "current-budget.json");
        File.WriteAllText(baselinePath, ValidSeoReportJson());
        File.WriteAllText(currentPath, ValidSeoReportJson(
            issuesJson: """
            [
              {
                "severity": "error",
                "code": "SEO999",
                "message": "Broken canonical",
                "route": "/"
              },
              {
                "severity": "warning",
                "code": "SEO201",
                "message": "Missing alternate",
                "route": "/"
              }
            ]
            """,
            errorCount: 1,
            warningCount: 1));

        var result = Invoke(() => SeoCommand.Diff(
            baselinePath,
            currentPath,
            maxNewErrors: 0,
            maxNewWarnings: 0,
            maxNewIssues: 1,
            failOnNewCodes: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "seo999" },
            failOnRouteRemoved: false,
            failOnIndexableDrop: false,
            contract: SeoReportValidator.AuditReportContract.SeoOnly,
            label: "SEO",
            commandName: "seo"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("SEO diff: newIssues=2 newErrors=1 newWarnings=1", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("+ error SEO999 / Broken canonical", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("+ warning SEO201 / Missing alternate", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("SEO diff budget exceeded: new errors 1 > 0.", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("SEO diff budget exceeded: new warnings 1 > 0.", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("SEO diff budget exceeded: new issues 2 > 1.", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("SEO diff budget exceeded: new issue code seo999.", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Diff_WhenRouteRemovedAndIndexableDropFailFlagsAreSet_ReturnsOne()
    {
        var baselinePath = Path.Combine(_tempDir, "baseline-routes.json");
        var currentPath = Path.Combine(_tempDir, "current-routes.json");
        File.WriteAllText(baselinePath, ValidSeoReportJson(
            routesJson: """
            [
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
              },
              {
                "url": "/post/",
                "outputPath": "post/index.html",
                "title": "Post",
                "description": "Post",
                "canonical": "https://example.com/post/",
                "indexable": true,
                "lastModified": "2026-01-01",
                "sitemapIncluded": true,
                "searchIncluded": true,
                "rssIncluded": false,
                "alternates": [],
                "schemaTypes": ["Article"]
              }
            ]
            """,
            routeCount: 2,
            indexableCount: 2));
        File.WriteAllText(currentPath, ValidSeoReportJson(
            routesJson: """
            [
              {
                "url": "/",
                "outputPath": "index.html",
                "title": "Home",
                "description": "Welcome",
                "canonical": "https://example.com/",
                "indexable": false,
                "lastModified": "2026-01-01",
                "sitemapIncluded": true,
                "searchIncluded": true,
                "rssIncluded": false,
                "alternates": [],
                "schemaTypes": ["WebPage"]
              }
            ]
            """,
            indexableCount: 0,
            nonIndexableCount: 1));

        var result = Invoke(() => SeoCommand.Diff(
            baselinePath,
            currentPath,
            maxNewErrors: null,
            maxNewWarnings: null,
            maxNewIssues: null,
            failOnNewCodes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            failOnRouteRemoved: true,
            failOnIndexableDrop: true,
            contract: SeoReportValidator.AuditReportContract.SeoOnly,
            label: "SEO",
            commandName: "seo"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("- route /post/", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("! indexable-drop /", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("SEO diff budget exceeded: routes were removed.", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("SEO diff budget exceeded: indexable routes became non-indexable.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_WhenContractRequiresPublishButSeoReportProvided_ReturnsTwo()
    {
        var baselinePath = Path.Combine(_tempDir, "baseline-seo.json");
        var currentPath = Path.Combine(_tempDir, "current-publish.json");
        File.WriteAllText(baselinePath, ValidSeoReportJson());
        File.WriteAllText(currentPath, ValidPublishReportJson());

        var result = Invoke(() => SeoCommand.Diff(
            baselinePath,
            currentPath,
            maxNewErrors: null,
            maxNewWarnings: null,
            maxNewIssues: null,
            failOnNewCodes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            failOnRouteRemoved: false,
            failOnIndexableDrop: false,
            contract: SeoReportValidator.AuditReportContract.PublishOnly,
            label: "Publish",
            commandName: "publish"));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid Publish report:", result.StdErr, StringComparison.Ordinal);
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

    private static (int ExitCode, string StdOut, string StdErr) Invoke(Func<int> action)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = action();
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static string ValidSeoReportJson(
        string? routesJson = null,
        string? issuesJson = null,
        int routeCount = 1,
        int indexableCount = 1,
        int nonIndexableCount = 0,
        int errorCount = 0,
        int warningCount = 0,
        bool includeSummaryBuckets = false)
    {
        routesJson ??= """
        [
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
        ]
        """;
        issuesJson ??= "[]";
        var summaryBuckets = includeSummaryBuckets
            ? """
              ,
                  "publishIssueCount": 2,
                  "machineReadabilityIssueCount": 3,
                  "trustIssueCount": 4,
                  "representationGapCount": 5
              """
            : string.Empty;

        return $$"""
        {
          "schema": "https://bukit.dev/schemas/seo-report.v1.json",
          "schemaVersion": "1.0",
          "generatedAt": "2026-01-01T00:00:00Z",
          "siteName": "test",
          "baseUrl": "https://example.com",
          "routes": {{routesJson}},
          "issues": {{issuesJson}},
          "summary": {
            "routeCount": {{routeCount}},
            "indexableCount": {{indexableCount}},
            "nonIndexableCount": {{nonIndexableCount}},
            "errorCount": {{errorCount}},
            "warningCount": {{warningCount}}{{summaryBuckets}}
          }
        }
        """;
    }

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
}
