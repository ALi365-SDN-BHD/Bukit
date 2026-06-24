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
                Stderr: "log",
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
                    new PluginDiagnostic("plugin.input.invalid", "error", "Invalid input", "content/index.md")
                ],
                Artifacts:
                [
                    new PluginArtifact("file", "out/result.json", "Result")
                ],
                Environment: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["NOTION_TOKEN"] = "secret-token",
                    ["PUBLIC_VALUE"] = "visible"
                }),
            CancellationToken.None);

        string json = File.ReadAllText(path);
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
        Assert.Contains("\"artifacts\"", json, StringComparison.Ordinal);
        Assert.Contains("\"out/result.json\"", json, StringComparison.Ordinal);
        Assert.Contains("\"NOTION_TOKEN\": \"***\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
        Assert.Contains("\"PUBLIC_VALUE\": \"visible\"", json, StringComparison.Ordinal);
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
