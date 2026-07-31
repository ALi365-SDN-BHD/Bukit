using Xunit;
using Bukit.PluginHost;
using Bukit.Plugin.Abstractions.Manifest;

namespace Bukit.PluginHost.Tests;

/// <summary>
/// Plugin protocol version compatibility tests.
/// Verifies backward compatibility of the plugin protocol across schema versions.
/// </summary>
public sealed class PluginProtocolCompatibilityTests
{
    [Fact]
    public void ManifestVersion_Default_IsV1()
    {
        var manifest = new PluginManifest(
            Id: "test.plugin",
            Name: "Test Plugin",
            Version: "1.0.0",
            Protocol: "bukit-plugin-v1",
            Kind: "process",
            Distribution: "self-contained");
        Assert.Equal(1, manifest.ManifestVersion);
    }

    [Fact]
    public void ManifestVersion_ExplicitV1()
    {
        var manifest = new PluginManifest(
            Id: "test.plugin",
            Name: "Test Plugin",
            Version: "1.0.0",
            Protocol: "bukit-plugin-v1",
            Kind: "process",
            Distribution: "self-contained",
            ManifestVersion: 1);
        Assert.Equal(1, manifest.ManifestVersion);
    }

    [Fact]
    public void PluginManifestMigrator_SameVersion_NoOp()
    {
        // When fromVersion == toVersion, Migrate should return the same node
        // This is a no-op migration
        var root = new YamlDotNet.RepresentationModel.YamlMappingNode();
        var result = PluginManifestMigrator.Migrate(root, 1, 1);
        Assert.Same(root, result);
    }

    [Fact]
    public void PluginManifestMigrator_UnsupportedVersion_Throws()
    {
        var root = new YamlDotNet.RepresentationModel.YamlMappingNode();
        Assert.Throws<InvalidOperationException>(() =>
            PluginManifestMigrator.Migrate(root, 1, 99));
    }

    [Fact]
    public void SupportedManifestVersion_Is1()
    {
        Assert.Equal(1, PluginManifestLoader.SupportedManifestVersion);
    }

    [Fact]
    public void PluginProcessResult_DefaultResourceLimitIsNull()
    {
        var result = new PluginProcessResult(
            ExitCode: 0,
            StdoutJson: "{}",
            Stderr: "",
            TimedOut: false,
            OutputLimitExceeded: false);
        Assert.Null(result.ResourceLimitExceeded);
    }

    [Fact]
    public void PluginProcessResult_WithResourceLimit()
    {
        var result = new PluginProcessResult(
            ExitCode: -1,
            StdoutJson: "",
            Stderr: "",
            TimedOut: false,
            OutputLimitExceeded: false,
            ResourceLimitExceeded: "CPU time exceeded");
        Assert.Equal("CPU time exceeded", result.ResourceLimitExceeded);
    }

    [Fact]
    public void ProcessRunRequest_DefaultResourceLimitsAreNull()
    {
        var request = new ProcessRunRequest(
            ExecutablePath: "/bin/echo",
            Arguments: ["hello"],
            StandardInput: "",
            WorkingDirectory: "/tmp",
            Timeout: TimeSpan.FromSeconds(10),
            StdoutMaxBytes: 1024,
            StderrMaxBytes: 1024);
        Assert.Null(request.MaxCpuTime);
        Assert.Null(request.MaxMemoryBytes);
    }

    [Fact]
    public void ProcessRunRequest_WithResourceLimits()
    {
        var request = new ProcessRunRequest(
            ExecutablePath: "/bin/echo",
            Arguments: ["hello"],
            StandardInput: "",
            WorkingDirectory: "/tmp",
            Timeout: TimeSpan.FromSeconds(10),
            StdoutMaxBytes: 1024,
            StderrMaxBytes: 1024,
            MaxCpuTime: TimeSpan.FromSeconds(5),
            MaxMemoryBytes: 256 * 1024 * 1024);
        Assert.Equal(TimeSpan.FromSeconds(5), request.MaxCpuTime);
        Assert.Equal(256 * 1024 * 1024, request.MaxMemoryBytes);
    }

    [Fact]
    public void ProcessRunResult_DefaultResourceLimitIsNull()
    {
        var result = new ProcessRunResult(
            ExitCode: 0,
            Stdout: "ok",
            Stderr: "",
            TimedOut: false,
            OutputLimitExceeded: false);
        Assert.Null(result.ResourceLimitExceeded);
    }

    [Fact]
    public void PluginProcessRequest_DefaultResourceLimitsAreNull()
    {
        var request = new PluginProcessRequest(
            ExecutablePath: "/bin/echo",
            Arguments: ["hello"],
            StandardInputJson: "{}",
            WorkingDirectory: "/tmp",
            Timeout: TimeSpan.FromSeconds(10),
            StdoutMaxBytes: 1024,
            StderrMaxBytes: 1024);
        Assert.Null(request.MaxCpuTime);
        Assert.Null(request.MaxMemoryBytes);
    }

    [Fact]
    public void PluginHostErrorCodes_ResourceLimitExceeded_Defined()
    {
        Assert.Equal("plugin.resourceLimitExceeded", PluginHostErrorCodes.ResourceLimitExceeded);
    }
}
