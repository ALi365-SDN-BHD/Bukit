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
}
