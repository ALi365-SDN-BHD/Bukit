using System.Text.Json;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Parsing;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class SeoInsightsCommandTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-seo-insights-command-tests-" + Guid.NewGuid().ToString("N"));

    public SeoInsightsCommandTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => TestCleanup.DeleteDirectory(_tempDir, recursive: true);

    [Fact]
    public async Task RunAsync_ValidInputs_WritesConfiguredReportAndCountsFindingsOnce()
    {
        var inputs = WriteValidInputs();
        var outputPath = Path.Combine(_tempDir, "reports", "custom-insights.json");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            $"  {inputs.Gsc} , {inputs.Ga4}  ",
            inputs.Rules,
            outputPath)));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            $"SEO insights: sourceRows=4 matched=4 unmatched=0 ambiguous=0 findings=3{Environment.NewLine}" +
            $"SEO insights report: {Path.GetFullPath(outputPath)}{Environment.NewLine}" +
            $"SEO insights classification: complete{Environment.NewLine}",
            result.StdOut);
        Assert.Empty(result.StdErr);
        Assert.True(File.Exists(outputPath));
        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.Equal(2, document.RootElement.GetProperty("sources").GetArrayLength());
        Assert.Equal(3, document.RootElement.GetProperty("routes")
            .EnumerateArray()
            .Sum(route => route.GetProperty("findings").GetArrayLength()));
    }

    [Fact]
    public async Task RunAsync_JoinGapWithoutStrict_WritesValidReportAndReturnsZero()
    {
        var inputs = WriteValidInputs(gscUrl: "https://example.com/missing/");
        var outputPath = Path.Combine(_tempDir, "allowed-gaps.json");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            $"{inputs.Gsc},{inputs.Ga4}",
            inputs.Rules,
            outputPath)));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("sourceRows=4 matched=3 unmatched=1 ambiguous=0", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("SEO insights classification: join-gaps-allowed", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_JoinGapWithStrict_WritesValidReportBeforeReturningOne()
    {
        var inputs = WriteValidInputs(gscUrl: "https://example.com/missing/");
        var outputPath = Path.Combine(_tempDir, "strict-gaps.json");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            $"{inputs.Gsc},{inputs.Ga4}",
            inputs.Rules,
            outputPath,
            strictJoin: true)));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("SEO insights classification: strict-join-failed", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
        Assert.True(File.Exists(outputPath));
        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.Equal(1, document.RootElement.GetProperty("joinQuality").GetProperty("overall").GetProperty("unmatched").GetInt64());
    }

    [Fact]
    public async Task RunAsync_SemanticallyDuplicateObservationPaths_ReturnsTwoWithoutLeakingPaths()
    {
        var inputs = WriteValidInputs();
        var duplicate = Path.Combine(Path.GetDirectoryName(inputs.Gsc)!, ".", Path.GetFileName(inputs.Gsc));

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            $"{inputs.Gsc},{duplicate}",
            inputs.Rules,
            Path.Combine(_tempDir, "duplicate.json"))));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal($"SEO insights failed: observations_duplicate.{Environment.NewLine}", result.StdErr);
        Assert.Empty(result.StdOut);
        Assert.DoesNotContain(_tempDir, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ParentDirectorySymlinkDuplicateObservations_ReturnsTwoWithoutChangingSource()
    {
        var inputs = WriteValidInputs();
        var alias = Path.Combine(_tempDir, "source-alias");
        CreateDirectorySymlinkOrSkip(alias, _tempDir);
        var sourceBytes = File.ReadAllBytes(inputs.Gsc);
        try
        {
            var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
                inputs.RouteMap,
                $"{inputs.Gsc},{Path.Combine(alias, Path.GetFileName(inputs.Gsc))}",
                inputs.Rules,
                Path.Combine(_tempDir, "duplicate-parent-alias.json"))));

            Assert.Equal(2, result.ExitCode);
            Assert.Equal($"SEO insights failed: observations_duplicate.{Environment.NewLine}", result.StdErr);
            Assert.Empty(result.StdOut);
            Assert.Equal(sourceBytes, File.ReadAllBytes(inputs.Gsc));
        }
        finally
        {
            DeleteDirectoryLinkIfExists(alias);
        }
    }

    [Theory]
    [InlineData("routes")]
    [InlineData("rules")]
    [InlineData("observations")]
    public async Task RunAsync_ParentDirectorySymlinkOutputCollision_PreservesEverySourceType(string sourceType)
    {
        var inputs = WriteValidInputs();
        var alias = Path.Combine(_tempDir, "output-alias");
        CreateDirectorySymlinkOrSkip(alias, _tempDir);
        var sourcePath = sourceType switch
        {
            "routes" => inputs.RouteMap,
            "rules" => inputs.Rules,
            "observations" => inputs.Gsc,
            _ => throw new InvalidOperationException("Unexpected source kind.")
        };
        var sourceBytes = File.ReadAllBytes(sourcePath);
        try
        {
            var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
                inputs.RouteMap,
                inputs.Gsc,
                inputs.Rules,
                Path.Combine(alias, Path.GetFileName(sourcePath)))));

            Assert.Equal(2, result.ExitCode);
            Assert.Equal($"SEO insights failed: output_conflict.{Environment.NewLine}", result.StdErr);
            Assert.Empty(result.StdOut);
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
        }
        finally
        {
            DeleteDirectoryLinkIfExists(alias);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("one.json,")]
    [InlineData("https://secret.example/observation.json")]
    [InlineData("file:///secret/observation.json")]
    [InlineData("//secret-host/share/observation.json")]
    public async Task RunAsync_InvalidObservationList_ReturnsStableNonLeakingError(string observations)
    {
        var inputs = WriteValidInputs();

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            observations,
            inputs.Rules,
            Path.Combine(_tempDir, "invalid-observations.json"))));

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("SEO insights failed: observations_", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.StdErr, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task RunAsync_MoreThanTenObservationFiles_ReturnsTwo()
    {
        var inputs = WriteValidInputs();
        var observations = string.Join(',', Enumerable.Range(0, 11).Select(index => Path.Combine(_tempDir, $"observation-{index}.json")));

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            observations,
            inputs.Rules,
            Path.Combine(_tempDir, "too-many.json"))));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal($"SEO insights failed: observations_count_invalid.{Environment.NewLine}", result.StdErr);
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task RunAsync_InvalidRouteMap_ReturnsStableErrorAndDoesNotWriteReport()
    {
        var inputs = WriteValidInputs();
        File.WriteAllText(inputs.RouteMap, ValidRouteMapJson().Replace(
            "\"routes\": [",
            "\"unexpectedSecretField\": \"secret payload\", \"routes\": [",
            StringComparison.Ordinal));
        var outputPath = Path.Combine(_tempDir, "invalid-route-map.json");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Gsc,
            inputs.Rules,
            outputPath)));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal($"SEO insights failed: route_map_invalid.{Environment.NewLine}", result.StdErr);
        Assert.DoesNotContain("secret", result.StdErr, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.StdOut);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_RouteMapAllowsEmptySiteUrlRelativeCanonicalAndDuplicateCanonicals()
    {
        var inputs = WriteValidInputs(
            routeMapJson: ValidRouteMapJson(siteUrl: string.Empty, includeDuplicateCanonical: true),
            gscUrl: "https://example.com/shared/");
        var outputPath = Path.Combine(_tempDir, "ambiguous.json");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Gsc,
            inputs.Rules,
            outputPath)));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("sourceRows=2 matched=1 unmatched=0 ambiguous=1", result.StdOut, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.Equal(2, document.RootElement.GetProperty("joinQuality").GetProperty("overall").GetProperty("total").GetInt64());
        Assert.Equal(2, document.RootElement.GetProperty("ambiguous")[0].GetProperty("candidates").GetArrayLength());
    }

    [Fact]
    public async Task RunAsync_MismatchedWindowsAndInvalidRules_ReturnTwoWithoutSuccessOutput()
    {
        var inputs = WriteValidInputs(ga4EndDate: "2026-08-03");
        var mismatch = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            $"{inputs.Gsc},{inputs.Ga4}",
            inputs.Rules,
            Path.Combine(_tempDir, "mismatch.json"))));

        File.WriteAllText(inputs.Rules, "{}");
        var invalidRules = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Gsc,
            inputs.Rules,
            Path.Combine(_tempDir, "invalid-rules.json"))));

        Assert.Equal(2, mismatch.ExitCode);
        Assert.Equal($"SEO insights failed: report_window_mismatch.{Environment.NewLine}", mismatch.StdErr);
        Assert.Empty(mismatch.StdOut);
        Assert.Equal(2, invalidRules.ExitCode);
        Assert.StartsWith("SEO insights failed: rules_", invalidRules.StdErr, StringComparison.Ordinal);
        Assert.Empty(invalidRules.StdOut);
    }

    [Fact]
    public async Task RunAsync_MissingRequiredValuesAndOutputFailure_ReturnTwoWithoutClaimingSuccess()
    {
        var inputs = WriteValidInputs();
        var missingRules = await InvokeAsync(() => SeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>
            {
                ["--routes"] = inputs.RouteMap,
                ["--observations"] = inputs.Gsc,
                ["--out"] = Path.Combine(_tempDir, "missing-rules.json")
            },
            ["insights"])));
        var outputFailure = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Gsc,
            inputs.Rules,
            _tempDir)));

        Assert.Equal(2, missingRules.ExitCode);
        Assert.Equal($"SEO insights failed: rules_required.{Environment.NewLine}", missingRules.StdErr);
        Assert.Empty(missingRules.StdOut);
        Assert.Equal(2, outputFailure.ExitCode);
        Assert.Equal($"SEO insights failed: output_unavailable.{Environment.NewLine}", outputFailure.StdErr);
        Assert.Empty(outputFailure.StdOut);
    }

    [Fact]
    public async Task Dispatch_ExtraPositionalArgument_ReturnsUsageFailureBeforeReadsOrWrites()
    {
        var outputPath = Path.Combine(_tempDir, "must-not-exist.json");
        var descriptor = BukitCliDescriptors.CreateDescriptors().Single(value => value.Spec.Name == "seo");
        var parsed = CliParser.Parse(descriptor.Spec,
        [
            "insights",
            "--routes", Path.Combine(_tempDir, "missing-routes.json"),
            "--observations", Path.Combine(_tempDir, "missing-observations.json"),
            "--rules", Path.Combine(_tempDir, "missing-rules.json"),
            "--out", outputPath,
            "stray-input"
        ]);
        Assert.True(parsed.IsSuccess);

        var result = await InvokeAsync(() => descriptor.DispatchAsync(parsed));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal($"SEO insights failed: usage_invalid.{Environment.NewLine}", result.StdErr);
        Assert.Empty(result.StdOut);
        Assert.False(File.Exists(outputPath));
    }

    private CliBoundCommand Command(
        string routes,
        string observations,
        string rules,
        string output,
        bool strictJoin = false)
    {
        var options = new Dictionary<string, string?>
        {
            ["--dir"] = Path.Combine(_tempDir, "dist"),
            ["--routes"] = routes,
            ["--observations"] = observations,
            ["--rules"] = rules,
            ["--out"] = output
        };
        if (strictJoin)
        {
            options["--strict-join"] = string.Empty;
        }

        return new CliBoundCommand(options, ["insights"]);
    }

    private (string RouteMap, string Gsc, string Ga4, string Rules) WriteValidInputs(
        string? routeMapJson = null,
        string gscUrl = "https://example.com/article/",
        string ga4EndDate = "2026-08-02")
    {
        var routeMap = Path.Combine(_tempDir, "seo-route-map.json");
        var gsc = Path.Combine(_tempDir, "gsc.json");
        var ga4 = Path.Combine(_tempDir, "ga4.json");
        var rules = Path.Combine(_tempDir, "rules.json");
        File.WriteAllText(routeMap, routeMapJson ?? ValidRouteMapJson());
        File.WriteAllText(gsc, ObservationJson(
            "google-search-console",
            gscUrl,
            "\"impressions\": 100, \"clicks\": 5, \"averagePosition\": 5",
            "https://example.com/second/",
            "\"impressions\": 5, \"clicks\": 0, \"averagePosition\": 20",
            "2026-08-02"));
        File.WriteAllText(ga4, ObservationJson(
            "google-analytics-4",
            "https://example.com/article/",
            "\"sessions\": 10, \"engagedSessions\": 8, \"keyEvents\": 1",
            "https://example.com/second/",
            "\"sessions\": 10, \"engagedSessions\": 8, \"keyEvents\": 2",
            ga4EndDate));
        File.WriteAllText(rules, ValidRulesJson());
        return (routeMap, gsc, ga4, rules);
    }

    private static string ValidRouteMapJson(string siteUrl = "https://example.com", bool includeDuplicateCanonical = false)
    {
        var duplicate = includeDuplicateCanonical
            ? "," + Environment.NewLine + """
                {
                  "routeKey": "route:sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                  "contentKey": null,
                  "route": "/legacy/",
                  "canonical": "/shared/",
                  "language": null,
                  "contentType": null,
                  "collection": null,
                  "indexable": true,
                  "publishedAt": null,
                  "updatedAt": null
                },
                {
                  "routeKey": "route:sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                  "contentKey": null,
                  "route": "/current/",
                  "canonical": "https://canonical.example/shared/",
                  "language": null,
                  "contentType": null,
                  "collection": null,
                  "indexable": true,
                  "publishedAt": null,
                  "updatedAt": null
                }
                """
            : string.Empty;
        return $$"""
            {
              "schema": "https://bukit.dev/schemas/seo-route-map.v1.json",
              "schemaVersion": "1.0",
              "generatedAt": "2026-08-03T00:00:00Z",
              "siteUrl": "{{siteUrl}}",
              "baseUrl": "/",
              "routes": [
                {
                  "routeKey": "route:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "contentKey": "content:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "route": "/article/",
                  "canonical": "/article/",
                  "language": "en",
                  "contentType": "article",
                  "collection": "posts",
                  "indexable": true,
                  "publishedAt": "2026-08-01T00:00:00Z",
                  "updatedAt": null
                },
                {
                  "routeKey": "route:sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                  "contentKey": null,
                  "route": "/second/",
                  "canonical": "https://example.com/second/",
                  "language": null,
                  "contentType": null,
                  "collection": null,
                  "indexable": true,
                  "publishedAt": null,
                  "updatedAt": null
                }{{duplicate}}
              ]
            }
            """;
    }

    private static string ObservationJson(
        string provider,
        string firstUrl,
        string firstMetrics,
        string secondUrl,
        string secondMetrics,
        string endDate)
        => $$"""
            {
              "schema": "https://bukit.dev/schemas/seo-observation.v1.json",
              "schemaVersion": "1.0",
              "provider": "{{provider}}",
              "scope": "google-organic",
              "collectedAt": "2026-08-03T02:00:00Z",
              "window": { "startDate": "2026-08-01", "endDate": "{{endDate}}", "timeZone": "Asia/Kuala_Lumpur" },
              "rows": [
                { "url": "{{firstUrl}}", {{firstMetrics}} },
                { "url": "{{secondUrl}}", {{secondMetrics}} }
              ]
            }
            """;

    private static string ValidRulesJson() => """
        {
          "schema": "https://bukit.dev/schemas/seo-insights-rules.v1.json",
          "schemaVersion": "1.0",
          "siteHost": "example.com",
          "hostAliases": [],
          "ignoredQueryParameters": ["utm_source"],
          "thresholds": {
            "minimumSearchImpressions": 10,
            "maximumLowImpressions": 10,
            "minimumAnalyticsSessions": 5,
            "lowCtr": 0.1,
            "lowEngagementRate": 0.4,
            "highEngagementRate": 0.7,
            "opportunityPositionMinimum": 4,
            "opportunityPositionMaximum": 10
          },
          "priorities": {
            "snippetMismatch": "P1",
            "landingQuality": "P0",
            "discoverability": "P2",
            "positionOpportunity": "P2"
          }
        }
        """;

    private static void CreateDirectorySymlinkOrSkip(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Directory symlinks are unavailable: {exception.GetType().Name}");
        }
    }

    private static void DeleteDirectoryLinkIfExists(string linkPath)
    {
        try
        {
            Directory.Delete(linkPath);
        }
        catch (DirectoryNotFoundException)
        {
        }
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
            return (await action(), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
