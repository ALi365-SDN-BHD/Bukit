using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Manifest;
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
                Environment: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["NOTION_TOKEN"] = "secret-token",
                    ["PUBLIC_VALUE"] = "visible"
                }),
            CancellationToken.None);

        string json = File.ReadAllText(path);
        Assert.Contains("\"pluginId\": \"echo\"", json, StringComparison.Ordinal);
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
