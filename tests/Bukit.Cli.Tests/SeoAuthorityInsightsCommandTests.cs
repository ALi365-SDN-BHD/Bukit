using System.Text.Json;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class SeoAuthorityInsightsCommandTests : IDisposable
{
    private const string QuestionKey = "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ContextHash = "context:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-seo-authority-insights-command-tests-" + Guid.NewGuid().ToString("N"));

    public SeoAuthorityInsightsCommandTests() => Directory.CreateDirectory(_tempDir);

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
        var expectedOutput = Path.GetFullPath(Path.Combine(_tempDir, "dist", ".bukit", "external-authority-report.json"));
        Assert.Contains($"SEO authority insights report: {expectedOutput}", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("SEO authority insights classification: complete", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
        Assert.True(File.Exists(expectedOutput));
        using var document = JsonDocument.Parse(File.ReadAllText(expectedOutput));
        Assert.Equal("https://bukit.dev/schemas/external-authority-report.v1.json",
            document.RootElement.GetProperty("schema").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("sources").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("overall").GetProperty("activeSources").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("overall").GetProperty("activeCitedRoutes").GetInt32());
    }

    [Fact]
    public async Task RunAsync_RepeatableObservations_AreCountedTogether()
    {
        var inputs = WriteValidInputs();
        var second = Path.Combine(_tempDir, "authority-2.json");
        File.WriteAllText(second, File.ReadAllText(inputs.Observations)
            .Replace("https://source.example/discussion/1", "https://source.example/discussion/2", StringComparison.Ordinal));

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            $"{inputs.Observations},{second}",
            inputs.Rules,
            Path.Combine(_tempDir, "combined.json"))));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("sourceRows=1 matched=1", result.StdOut, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "combined.json")));
        Assert.Equal(2, document.RootElement.GetProperty("sources").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("routes").GetArrayLength());
        Assert.Equal(2, document.RootElement.GetProperty("routes")[0].GetProperty("activeSources").GetInt32());
    }

    [Fact]
    public async Task RunAsync_DeletedSource_IsEvidenceWithoutActiveCount()
    {
        var inputs = WriteValidInputs(status: "deleted", observedAt: "2026-08-02T00:00:00Z");

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            inputs.Observations,
            inputs.Rules,
            Path.Combine(_tempDir, "lifecycle.json"))));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SEO authority insights classification: complete", result.StdOut, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "lifecycle.json")));
        Assert.Equal(1, document.RootElement.GetProperty("sources").GetArrayLength());
        Assert.Equal("deleted", document.RootElement.GetProperty("sources")[0].GetProperty("status").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("overall").GetProperty("activeSources").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("routes").GetArrayLength());
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
        Assert.Contains("SEO authority insights classification: join-gaps-allowed", result.StdOut, StringComparison.Ordinal);
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
        Assert.Contains("SEO authority insights classification: strict-join-failed", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_RemoteObservationPath_ReturnsTwoBeforeAnyNetworkAccess()
    {
        var inputs = WriteValidInputs();

        var result = await InvokeAsync(() => SeoCommand.RunAsync(Command(
            inputs.RouteMap,
            "https://attacker.example/authority.json",
            inputs.Rules,
            Path.Combine(_tempDir, "remote.json"))));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("observations_path_invalid", result.StdErr, StringComparison.Ordinal);
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
            ["authority-insights"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal($"SEO authority insights failed: observations_required.{Environment.NewLine}", result.StdErr);
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
        Assert.Contains("external_authority_observation_json_invalid", result.StdErr, StringComparison.Ordinal);
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
            ["authority-insights", "stray-input"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal($"SEO authority insights failed: usage_invalid.{Environment.NewLine}", result.StdErr);
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

        return new CliBoundCommand(options, ["authority-insights"]);
    }

    private (string RouteMap, string Observations, string Rules) WriteValidInputs(
        string citedUrls = "[\"https://example.com/article/\"]",
        string status = "active",
        string observedAt = "2026-08-05T00:00:00Z")
    {
        var routeMap = Path.Combine(_tempDir, "seo-route-map.json");
        var observations = Path.Combine(_tempDir, "external-authority.json");
        var rules = Path.Combine(_tempDir, "rules.json");
        File.WriteAllText(routeMap, RouteMapJson());
        File.WriteAllText(observations, ObservationJson(citedUrls, status, observedAt));
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

    private static string ObservationJson(string citedUrls, string status, string observedAt) => $$"""
        {
          "schema": "https://bukit.dev/schemas/external-authority-observation.v1.json",
          "schemaVersion": "1.0",
          "provider": "approved-provider",
          "collectedAt": "2026-08-05T00:00:00Z",
          "collectionMethod": "api",
          "rows": [
            {
              "sourceUrl": "https://source.example/discussion/1",
              "sourceType": "forum",
              "observedAt": "{{observedAt}}",
              "status": "{{status}}",
              "questionKey": "{{QuestionKey}}",
              "topicKey": null,
              "entityKey": null,
              "contextHash": "{{ContextHash}}",
              "citedUrls": {{citedUrls}}
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
