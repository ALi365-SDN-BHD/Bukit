using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginCapabilityTests
{
    [Fact]
    public void ExternalPluginConfig_HasCapabilitiesField()
    {
        var config = new ExternalPluginConfig
        {
            Runtime = "process",
            Entry = "./plugin",
            Capabilities = new List<string> { "emit-outputs" }
        };

        Assert.NotNull(config.Capabilities);
        Assert.Single(config.Capabilities);
        Assert.Contains("emit-outputs", config.Capabilities);
    }

    [Fact]
    public void ExternalPluginConfig_Capabilities_DefaultsToNull()
    {
        var config = new ExternalPluginConfig
        {
            Runtime = "process",
            Entry = "./plugin"
        };

        Assert.Null(config.Capabilities);
    }

    [Fact]
    public void PluginCapability_KnownCapabilities_AreDefined()
    {
        Assert.Equal("emit-outputs", PluginCapability.EmitOutputs);
        Assert.Equal("derive-pages", PluginCapability.DerivePages);
    }

    [Fact]
    public void PluginCapability_AllCapabilities_AreDistinct()
    {
        var all = PluginCapability.AllCapabilities;
        var distinct = new HashSet<string>(all);
        Assert.Equal(distinct.Count, all.Count);
    }

    [Fact]
    public void PluginCapability_AllCapabilities_ContainsExpectedValues()
    {
        var all = PluginCapability.AllCapabilities;
        Assert.Contains(PluginCapability.EmitOutputs, all);
        Assert.Contains(PluginCapability.DerivePages, all);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void PluginCapability_IsKnown_AcceptsValidValues()
    {
        Assert.True(PluginCapability.IsKnown("emit-outputs"));
        Assert.True(PluginCapability.IsKnown("derive-pages"));
    }

    [Fact]
    public void PluginCapability_IsKnown_RejectsInvalidValues()
    {
        Assert.False(PluginCapability.IsKnown("read-files"));
        Assert.False(PluginCapability.IsKnown("network"));
        Assert.False(PluginCapability.IsKnown(""));
        Assert.False(PluginCapability.IsKnown(null!));
    }
}
