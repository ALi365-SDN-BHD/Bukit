using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Abstractions.Tests;

public sealed class PluginConfigDtoTests
{
    [Fact]
    public void PluginHostConfig_DefaultsToEmptyPluginMap()
    {
        var config = new PluginHostConfig(Version: 1);

        Assert.Equal(1, config.Version);
        Assert.Empty(config.Plugins);
    }

    [Fact]
    public void PluginConfigEntry_CarriesSourcePermissionsTimeoutAndOutputLimits()
    {
        var entry = new PluginConfigEntry(
            Enabled: true,
            Source: "plugins/echo",
            ExposeCommands: ["echo"],
            Permissions: new PluginPermissionSet(),
            Timeout: new PluginTimeoutOptions(HandshakeMs: 1000, ManifestMs: 1000, InvokeMs: 2000),
            Output: new PluginOutputLimitOptions(StdoutMaxBytes: 1024, StderrMaxBytes: 1024, ResponseMaxBytes: 1024),
            FailMode: "strict",
            AllowInCi: true);

        Assert.True(entry.Enabled);
        Assert.Equal("plugins/echo", entry.Source);
        Assert.Equal(["echo"], entry.ExposeCommands);
        Assert.True(entry.AllowInCi);
        Assert.Equal(2000, entry.Timeout.InvokeMs);
        Assert.Equal(1024, entry.Output.ResponseMaxBytes);
    }

    [Fact]
    public void PluginConfigEntry_LegacyConstructor_DeconstructsToTwelveValues()
    {
        var exposeCommands = new[] { "echo" };
        var permissions = new PluginPermissionSet();
        var timeout = new PluginTimeoutOptions(1000, 2000, 3000);
        var output = new PluginOutputLimitOptions(1024, 2048, 4096);
        var entry = new PluginConfigEntry(
            true,
            "plugins/echo",
            exposeCommands,
            permissions,
            timeout,
            output,
            "strict",
            true,
            "Echo plugin",
            true,
            true,
            "static");

        var (
            enabled,
            source,
            deconstructedCommands,
            deconstructedPermissions,
            deconstructedTimeout,
            deconstructedOutput,
            failMode,
            allowInCi,
            description,
            permissionsExplicit,
            exposeCommandsDeclared,
            manifestPolicy) = entry;

        Assert.True(enabled);
        Assert.Equal("plugins/echo", source);
        Assert.Same(exposeCommands, deconstructedCommands);
        Assert.Same(permissions, deconstructedPermissions);
        Assert.Same(timeout, deconstructedTimeout);
        Assert.Same(output, deconstructedOutput);
        Assert.Equal("strict", failMode);
        Assert.True(allowInCi);
        Assert.Equal("Echo plugin", description);
        Assert.True(permissionsExplicit);
        Assert.True(exposeCommandsDeclared);
        Assert.Equal("static", manifestPolicy);
    }

    [Fact]
    public void PluginConfigEntry_LegacySourceGeneratedJson_OmitsNullResources()
    {
        var entry = new PluginConfigEntry(
            Enabled: true,
            Source: "plugins/echo");

        string json = JsonSerializer.Serialize(
            entry,
            PluginJsonSerializerContext.Default.PluginConfigEntry);

        Assert.Equal(
            "{\"enabled\":true,\"source\":\"plugins/echo\",\"failMode\":\"strict\",\"allowInCi\":false,\"description\":null,\"permissionsExplicit\":false,\"exposeCommandsDeclared\":false,\"manifestPolicy\":\"static\",\"exposeCommands\":[],\"permissions\":{\"network\":false,\"fileSystem\":{\"read\":[],\"write\":[]},\"environment\":{\"read\":[]}},\"timeout\":{\"handshakeMs\":5000,\"manifestMs\":5000,\"invokeMs\":120000},\"output\":{\"stdoutMaxBytes\":4194304,\"stderrMaxBytes\":4194304,\"responseMaxBytes\":4194304}}",
            json);
    }

    [Fact]
    public void PluginConfigEntry_Resources_DefaultsToNull()
    {
        var entry = new PluginConfigEntry(
            Enabled: true,
            Source: "plugins/echo");

        Assert.Null(entry.Resources);
    }

    [Fact]
    public void PluginConfigEntry_Resources_CarriesConfiguredLimits()
    {
        var entry = new PluginConfigEntry(
            Enabled: true,
            Source: "plugins/echo")
        {
            Resources = new PluginResourceLimitOptions(
                MaxCpuTimeMs: 5000,
                MaxMemoryBytes: 268435456L)
        };

        Assert.NotNull(entry.Resources);
        Assert.Equal(5000, entry.Resources!.MaxCpuTimeMs);
        Assert.Equal(268435456L, entry.Resources.MaxMemoryBytes);
    }

    [Fact]
    public void PluginConfigEntry_Resources_SourceGeneratedJsonRoundTripsConfiguredLimits()
    {
        var entry = new PluginConfigEntry(
            Enabled: true,
            Source: "plugins/echo")
        {
            Resources = new PluginResourceLimitOptions(
                MaxCpuTimeMs: 5000,
                MaxMemoryBytes: 268435456L)
        };

        string json = JsonSerializer.Serialize(
            entry,
            PluginJsonSerializerContext.Default.PluginConfigEntry);
        PluginConfigEntry? roundTripped = JsonSerializer.Deserialize(
            json,
            PluginJsonSerializerContext.Default.PluginConfigEntry);

        Assert.Contains("\"resources\":{\"maxCpuTimeMs\":5000,\"maxMemoryBytes\":268435456}", json);
        Assert.NotNull(roundTripped?.Resources);
        Assert.Equal(5000, roundTripped.Resources!.MaxCpuTimeMs);
        Assert.Equal(268435456L, roundTripped.Resources.MaxMemoryBytes);
    }

    [Fact]
    public void PluginResourceLimitOptions_NullFieldsRemainNull()
    {
        var options = new PluginResourceLimitOptions();

        Assert.Null(options.MaxCpuTimeMs);
        Assert.Null(options.MaxMemoryBytes);
    }
}
