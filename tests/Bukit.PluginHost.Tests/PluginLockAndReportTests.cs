using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Plugin.Abstractions.Security;
using Bukit.PluginHost;
using Bukit.Shared;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginLockAndReportTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public async Task PluginLockFileWriter_WritesResolvedLockYaml()
    {
        using var directory = TestDirectory.Create();
        var writer = new PluginLockFileWriter();

        await writer.WriteAsync(
            directory.Path,
            [
                new PluginLockEntry(
                    Id: "echo",
                    Version: "1.0.0",
                    Source: "plugins/echo",
                    ManifestVersion: "1.0.0",
                    Protocol: "bukit-plugin-v1",
                    Entry: "plugins/echo/bin/osx-arm64/bukit-plugin-echo",
                    Platform: "osx-arm64",
                    Sha256: new string('a', 64),
                    Commands: ["echo"],
                    ResolvedAt: DateTimeOffset.Parse("2026-06-24T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    Sha256Verified: true)
            ],
            CancellationToken.None);

        string lockText = File.ReadAllText(Path.Combine(directory.Path, ".bukit", "plugins.lock.yaml"));
        Assert.Contains("resolved:", lockText, StringComparison.Ordinal);
        Assert.DoesNotContain("plugins:", lockText, StringComparison.Ordinal);
        Assert.Contains("source: plugins/echo", lockText, StringComparison.Ordinal);
        Assert.Contains("manifestVersion: 1.0.0", lockText, StringComparison.Ordinal);
        Assert.Contains("protocol: bukit-plugin-v1", lockText, StringComparison.Ordinal);
        Assert.Contains("entry: plugins/echo/bin/osx-arm64/bukit-plugin-echo", lockText, StringComparison.Ordinal);
        Assert.Contains("commands:", lockText, StringComparison.Ordinal);
        Assert.Contains("- echo", lockText, StringComparison.Ordinal);
        Assert.Contains("resolvedAt: 2026-06-24T00:00:00", lockText, StringComparison.Ordinal);
        Assert.Contains("sha256Verified: true", lockText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PluginExecutionReporter_WritesReportAndMasksSecrets()
    {
        using var directory = TestDirectory.Create();
        var reporter = new PluginExecutionReporter();

        string path = await reporter.WriteAsync(
            directory.Path,
            new PluginExecutionReport(
                PluginId: "echo",
                Operation: "invoke",
                RequestId: "req-1",
                ProcessExitCode: 0,
                Success: true,
                TimedOut: false,
                OutputLimitExceeded: false,
                StdoutBytes: 10,
                StderrBytes: 5,
                Stderr: "plugin stderr leaked https://example.invalid/file?token=secret-token",
                PluginVersion: "0.1.0",
                Protocol: "bukit-plugin-v1",
                Platform: "osx-arm64",
                Command: "echo",
                Entry: "plugins/echo/bin/osx-arm64/bukit-plugin-echo",
                StartedAt: DateTimeOffset.Parse("2026-06-24T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                DurationMs: 12,
                ResponseExitCode: 2,
                Sha256Verified: true,
                Permissions: new PluginPermissionSet(
                    FileSystem: new PluginFileSystemPermission(Read: ["content"], Write: ["public"]),
                    Network: true,
                    Environment: new PluginEnvironmentPermission(Read: ["NOTION_TOKEN"])),
                Diagnostics:
                [
                    new PluginDiagnostic("plugin.input.invalid", "error", "Invalid input secret-token", "content/secret-token.md")
                ],
                Artifacts:
                [
                    new PluginArtifact("file", "out/result.json", "Result secret-token")
                ],
                ResponseSummary: new PluginExecutionResponseSummary(
                    Success: false,
                    ExitCode: 2,
                    DiagnosticCodes: ["plugin.input.invalid"],
                    ArtifactCount: 1),
                Environment: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["NOTION_TOKEN"] = "secret-token",
                    ["PUBLIC_VALUE"] = "visible"
                }),
            CancellationToken.None);

        string json = File.ReadAllText(path);
        string goldenPath = Path.Combine(
            RepoRoot,
            "tests",
            "fixtures",
            "plugin-contracts",
            "plugin-execution-report.v1.json");
        string schemaPath = Path.Combine(
            RepoRoot,
            "docs",
            "schemas",
            "plugin-execution-report.v1.schema.json");
        JsonNode actual = JsonNode.Parse(json)!;
        JsonNode golden = JsonNode.Parse(File.ReadAllText(goldenPath))!;
        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        using var actualDocument = JsonDocument.Parse(json);
        using var goldenDocument = JsonDocument.Parse(File.ReadAllText(goldenPath));

        Assert.True(
            JsonNode.DeepEquals(golden, actual),
            $"Execution report did not match {Path.GetRelativePath(RepoRoot, goldenPath)}.");
        AssertJsonSchema(schema.RootElement, actualDocument.RootElement);
        AssertJsonSchema(schema.RootElement, goldenDocument.RootElement);
        Assert.False(actual.AsObject().ContainsKey("stdout"));
        Assert.Contains("\"pluginId\": \"echo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pluginVersion\": \"0.1.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"protocol\": \"bukit-plugin-v1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"platform\": \"osx-arm64\"", json, StringComparison.Ordinal);
        Assert.Contains("\"command\": \"echo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"entry\": \"plugins/echo/bin/osx-arm64/bukit-plugin-echo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"startedAt\": \"2026-06-24T00:00:00", json, StringComparison.Ordinal);
        Assert.Contains("\"durationMs\": 12", json, StringComparison.Ordinal);
        Assert.Contains("\"responseExitCode\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"sha256Verified\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"permissions\"", json, StringComparison.Ordinal);
        Assert.Contains("\"read\"", json, StringComparison.Ordinal);
        Assert.Contains("\"diagnostics\"", json, StringComparison.Ordinal);
        Assert.Contains("\"plugin.input.invalid\"", json, StringComparison.Ordinal);
        Assert.Contains("\"message\": \"Invalid input ***\"", json, StringComparison.Ordinal);
        Assert.Contains("\"path\": \"content/***.md\"", json, StringComparison.Ordinal);
        Assert.Contains("\"artifacts\"", json, StringComparison.Ordinal);
        Assert.Contains("\"out/result.json\"", json, StringComparison.Ordinal);
        Assert.Contains("\"description\": \"Result ***\"", json, StringComparison.Ordinal);
        Assert.Contains("\"responseSummary\"", json, StringComparison.Ordinal);
        Assert.Contains("\"success\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"exitCode\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"diagnosticCodes\"", json, StringComparison.Ordinal);
        Assert.Contains("\"artifactCount\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"stderr\": \"plugin stderr leaked https://example.invalid/file?token=***\"", json, StringComparison.Ordinal);
        Assert.Contains("\"NOTION_TOKEN\": \"***\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
        Assert.Contains("token=***", json, StringComparison.Ordinal);
        Assert.Contains("\"PUBLIC_VALUE\": \"visible\"", json, StringComparison.Ordinal);
    }

    private static void AssertJsonSchema(
        JsonElement schema,
        JsonElement instance,
        string instancePath = "$")
    {
        if (schema.TryGetProperty("type", out JsonElement type))
        {
            string[] allowedTypes = type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Select(item => item.GetString()!).ToArray()
                : [type.GetString()!];
            Assert.True(
                allowedTypes.Any(candidate => MatchesType(candidate, instance)),
                $"{instancePath} has JSON kind {instance.ValueKind}, expected {string.Join(" or ", allowedTypes)}.");
        }

        if (instance.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            JsonElement properties = schema.TryGetProperty("properties", out JsonElement declaredProperties)
                ? declaredProperties
                : default;
            if (schema.TryGetProperty("required", out JsonElement required))
            {
                foreach (JsonElement propertyName in required.EnumerateArray())
                {
                    string name = propertyName.GetString()!;
                    Assert.True(
                        instance.TryGetProperty(name, out _),
                        $"{instancePath} is missing required property '{name}'.");
                }
            }

            foreach (JsonProperty property in instance.EnumerateObject())
            {
                if (properties.ValueKind == JsonValueKind.Object &&
                    properties.TryGetProperty(property.Name, out JsonElement propertySchema))
                {
                    AssertJsonSchema(
                        propertySchema,
                        property.Value,
                        $"{instancePath}.{property.Name}");
                    continue;
                }

                if (!schema.TryGetProperty("additionalProperties", out JsonElement additionalProperties))
                {
                    continue;
                }

                Assert.False(
                    additionalProperties.ValueKind == JsonValueKind.False,
                    $"{instancePath} contains undeclared property '{property.Name}'.");
                if (additionalProperties.ValueKind == JsonValueKind.Object)
                {
                    AssertJsonSchema(
                        additionalProperties,
                        property.Value,
                        $"{instancePath}.{property.Name}");
                }
            }
        }

        if (instance.ValueKind == JsonValueKind.Array &&
            schema.TryGetProperty("items", out JsonElement itemSchema))
        {
            int index = 0;
            foreach (JsonElement item in instance.EnumerateArray())
            {
                AssertJsonSchema(itemSchema, item, $"{instancePath}[{index}]");
                index++;
            }
        }
    }

    private static bool MatchesType(string type, JsonElement value)
        => type switch
        {
            "null" => value.ValueKind == JsonValueKind.Null,
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => throw new InvalidOperationException($"Unsupported schema type '{type}'.")
        };

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Bukit repository root.");
    }

    [Fact]
    public void PluginCiPolicy_RejectsPluginWhenAllowInCiIsFalse()
    {
        var policy = new PluginCiPolicy();
        var entry = new PluginConfigEntry(
            Enabled: true,
            Source: "plugins/echo",
            PermissionsExplicit: true);
        var platform = new PluginPlatformEntry("bin/osx-arm64/bukit-plugin-echo", new string('a', 64));

        ConfigException exception = Assert.Throws<ConfigException>(
            () => policy.Validate("echo", entry, platform, sha256Verified: true, isCi: true));

        Assert.Contains("allowInCi=true", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginCiPolicy_AllowsPluginWhenCiRequirementsAreMet()
    {
        var policy = new PluginCiPolicy();
        var entry = new PluginConfigEntry(
            Enabled: true,
            Source: "plugins/echo",
            AllowInCi: true,
            PermissionsExplicit: true);
        var platform = new PluginPlatformEntry("bin/osx-arm64/bukit-plugin-echo", new string('a', 64));

        policy.Validate("echo", entry, platform, sha256Verified: true, isCi: true);
    }
}
