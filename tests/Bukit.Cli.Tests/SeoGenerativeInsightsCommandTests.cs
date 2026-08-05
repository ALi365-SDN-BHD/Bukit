using System.Text.Json;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class SeoGenerativeInsightsCommandTests : IDisposable
{
    private const string QuestionKey = "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string AnswerHash = "answer:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string RouteKeyA = "route:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-seo-generative-insights-command-tests-" + Guid.NewGuid().ToString("N"));

    public SeoGenerativeInsightsCommandTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => TestCleanup.DeleteDirectory(_tempDir, recursive: true);

    [Fact]
    public async Task RunAsync_ValidInputs_WritesDefaultReportAndReturnsZero()
    {
        var inputs = WriteValidInputs();

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Observations,
            inputs.Rules,
            output: null)));

        Assert.Equal(0, result.ExitCode);
        var expectedOutput = Path.GetFullPath(Path.Combine(_tempDir, "dist", ".bukit", "generative-citation-report.json"));
        Assert.Contains($"SEO generative insights report: {expectedOutput}", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("SEO generative insights classification: complete", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
        Assert.True(File.Exists(expectedOutput));
        using var document = JsonDocument.Parse(File.ReadAllText(expectedOutput));
        Assert.Equal("https://bukit.dev/schemas/generative-citation-report.v1.json",
            document.RootElement.GetProperty("schema").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("questions").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("overall").GetProperty("runs").GetInt32());
    }

    [Fact]
    public async Task RunAsync_RepeatableObservations_AreCountedTogether()
    {
        var inputs = WriteValidInputs();
        var second = Path.Combine(_tempDir, "generative-2.json");
        File.Copy(inputs.Observations, second);

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            $"{inputs.Observations},{second}",
            inputs.Rules,
            Path.Combine(_tempDir, "combined.json"))));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("sourceRows=1 matched=1", result.StdOut, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "combined.json")));
        Assert.Equal(2, document.RootElement.GetProperty("overall").GetProperty("runs").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("sources").GetArrayLength());
    }

    [Fact]
    public async Task RunAsync_IgnoredQueryParametersFromRules_AppliedToUrlMatching()
    {
        var inputs = WriteValidInputs("[\"https://example.com/article/?utm_source=newsletter\"]");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Observations,
            inputs.Rules,
            Path.Combine(_tempDir, "normalized.json"))));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("sourceRows=1 matched=1 unmatched=0 ambiguous=0", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_JoinGapWithoutStrict_ReturnsZeroWithGapClassification()
    {
        var inputs = WriteValidInputs(citedUrls: "[\"https://example.com/article/\", \"https://example.com/unknown/\"]");
        var outputPath = Path.Combine(_tempDir, "gaps.json");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Observations,
            inputs.Rules,
            outputPath)));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SEO generative insights classification: join-gaps-allowed", result.StdOut, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.Equal(1, document.RootElement.GetProperty("unmatchedCitedUrls").GetArrayLength());
    }

    [Fact]
    public async Task RunAsync_JoinGapWithStrict_ReturnsOneAfterWritingReport()
    {
        var inputs = WriteValidInputs(citedUrls: "[\"https://example.com/article/\", \"https://example.com/unknown/\"]");
        var outputPath = Path.Combine(_tempDir, "strict.json");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Observations,
            inputs.Rules,
            outputPath,
            strictJoin: true)));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("SEO generative insights classification: strict-join-failed", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_InvalidCitedUrlWithStrict_ReturnsOneAfterWritingReport()
    {
        var inputs = WriteValidInputs("[\"https://example.com/article/\", \"ftp://example.com/file\"]");
        var outputPath = Path.Combine(_tempDir, "strict-invalid.json");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Observations,
            inputs.Rules,
            outputPath,
            strictJoin: true)));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("unmatched=1", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("SEO generative insights classification: strict-join-failed", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath));
        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.Equal(1, document.RootElement.GetProperty("unmatchedCitedUrls").GetArrayLength());
    }

    [Fact]
    public async Task RunAsync_RemoteObservationPath_ReturnsTwoBeforeAnyNetworkAccess()
    {
        var inputs = WriteValidInputs();

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            "https://attacker.example/generative.json",
            inputs.Rules,
            Path.Combine(_tempDir, "remote.json"))));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("observations_path_invalid", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_RemoteRouteMapPath_ReturnsTwo()
    {
        var inputs = WriteValidInputs();

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            "https://attacker.example/seo-route-map.json",
            inputs.Observations,
            inputs.Rules,
            Path.Combine(_tempDir, "remote-routes.json"))));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("routes_path_invalid", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_MissingObservations_ReturnsTwoWithStableCode()
    {
        var inputs = WriteValidInputs();

        var result = await InvokeAsync(() => SeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>
            {
                ["--dir"] = Path.Combine(_tempDir, "dist"),
                ["--routes"] = inputs.RouteMap,
                ["--rules"] = inputs.Rules,
                ["--out"] = Path.Combine(_tempDir, "no-observations.json")
            },
            ["generative-insights"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal($"SEO generative insights failed: observations_required.{Environment.NewLine}", result.StdErr);
    }

    [Fact]
    public async Task RunAsync_InvalidObservation_ReturnsTwoWithStableDataCode()
    {
        var inputs = WriteValidInputs();
        File.WriteAllText(inputs.Observations, "{ not json");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Observations,
            inputs.Rules,
            Path.Combine(_tempDir, "invalid-observations.json"))));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("generative_observation_json_invalid", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatch_ExtraPositionalArgument_ReturnsUsageFailure()
    {
        var inputs = WriteValidInputs();

        var result = await InvokeAsync(() => SeoCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>
            {
                ["--dir"] = Path.Combine(_tempDir, "dist"),
                ["--routes"] = inputs.RouteMap,
                ["--observations"] = inputs.Observations,
                ["--rules"] = inputs.Rules,
                ["--out"] = Path.Combine(_tempDir, "stray.json")
            },
            ["generative-insights", "stray-input"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal($"SEO generative insights failed: usage_invalid.{Environment.NewLine}", result.StdErr);
    }

    private CliBoundCommand Command(
        string routes,
        string observations,
        string rules,
        string? output,
        bool strictJoin = false)
    {
        var options = new Dictionary<string, string?>
        {
            ["--dir"] = Path.Combine(_tempDir, "dist"),
            ["--routes"] = routes,
            ["--observations"] = observations,
            ["--rules"] = rules
        };
        if (output is not null)
        {
            options["--out"] = output;
        }

        if (strictJoin)
        {
            options["--strict-join"] = string.Empty;
        }

        return new CliBoundCommand(options, ["generative-insights"]);
    }

    private (string RouteMap, string Observations, string Rules) WriteValidInputs(
        string citedUrls = "[\"https://example.com/article/\"]")
    {
        var routeMap = Path.Combine(_tempDir, "seo-route-map.json");
        var observations = Path.Combine(_tempDir, "generative-runs.json");
        var rules = Path.Combine(_tempDir, "rules.json");
        File.WriteAllText(routeMap, RouteMapJson());
        File.WriteAllText(observations, ObservationJson(citedUrls));
        File.WriteAllText(rules, RulesJson());
        return (routeMap, observations, rules);
    }

    private static string RouteMapJson() => """
        {
          "schema": "https://bukit.dev/schemas/seo-route-map.v1.json",
          "schemaVersion": "1.0",
          "generatedAt": "2026-08-03T00:00:00Z",
          "siteUrl": "https://example.com",
          "baseUrl": "/",
          "routes": [
            {
              "routeKey": "route:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "contentKey": null,
              "route": "/article/",
              "canonical": "https://example.com/article/",
              "language": "en",
              "contentType": "article",
              "collection": "posts",
              "indexable": true,
              "publishedAt": "2026-08-01T00:00:00Z",
              "updatedAt": null
            }
          ]
        }
        """;

    private static string ObservationJson(string citedUrls) => $$"""
        {
          "schema": "https://bukit.dev/schemas/generative-answer-observation.v1.json",
          "schemaVersion": "1.0",
          "engine": "engine-one",
          "promptSetVersion": "v1",
          "locale": "zh-CN",
          "collectedAt": "2026-08-04T00:00:00Z",
          "collectionMethod": "api",
          "rows": [
            {
              "questionKey": "{{QuestionKey}}",
              "promptVariant": 0,
              "runIndex": 0,
              "brandMentioned": true,
              "siteCited": true,
              "citedUrls": {{citedUrls}},
              "citationPosition": 1,
              "answerHash": "{{AnswerHash}}"
            }
          ]
        }
        """;

    private static string RulesJson() => """
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
