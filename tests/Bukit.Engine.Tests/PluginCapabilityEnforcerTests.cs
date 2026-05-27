using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginCapabilityEnforcerTests
{
    [Fact]
    public void Enforce_NoCapabilitiesDeclared_AllowsAnyHook()
    {
        var plugin = CreatePlugin(capabilities: null);

        PluginCapabilityEnforcer.Enforce(plugin, "derive-pages");
        PluginCapabilityEnforcer.Enforce(plugin, "after-build");
    }

    [Fact]
    public void Enforce_DerivePagesHook_HasDerivePagesCapability_Passes()
    {
        var plugin = CreatePlugin(capabilities: ["derive-pages"]);

        PluginCapabilityEnforcer.Enforce(plugin, "derive-pages");
    }

    [Fact]
    public void Enforce_DerivePagesHook_MissingCapability_Throws()
    {
        var plugin = CreatePlugin(capabilities: ["emit-outputs"]);

        var ex = Assert.Throws<ConfigException>(() =>
            PluginCapabilityEnforcer.Enforce(plugin, "derive-pages"));

        Assert.Contains("derive-pages", ex.Message);
        Assert.Contains("capabilities", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enforce_AfterBuildHook_HasEmitOutputs_Passes()
    {
        var plugin = CreatePlugin(capabilities: ["emit-outputs"]);

        PluginCapabilityEnforcer.Enforce(plugin, "after-build");
    }

    [Fact]
    public void Enforce_AfterBuildHook_MissingCapability_Throws()
    {
        var plugin = CreatePlugin(capabilities: ["derive-pages"]);

        var ex = Assert.Throws<ConfigException>(() =>
            PluginCapabilityEnforcer.Enforce(plugin, "after-build"));

        Assert.Contains("after-build", ex.Message);
        Assert.Contains("capabilities", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enforce_BothCapabilities_AllowsBothHooks()
    {
        var plugin = CreatePlugin(capabilities: ["emit-outputs", "derive-pages"]);

        PluginCapabilityEnforcer.Enforce(plugin, "derive-pages");
        PluginCapabilityEnforcer.Enforce(plugin, "after-build");
    }

    [Fact]
    public void Enforce_DuplicateCapabilities_IsSafe()
    {
        var plugin = CreatePlugin(capabilities: ["emit-outputs", "emit-outputs"]);

        PluginCapabilityEnforcer.Enforce(plugin, "after-build");
    }

    [Fact]
    public void Enforce_HasCapability_IgnoresUnknownCapabilityStrings()
    {
        var plugin = CreatePlugin(capabilities: ["emit-outputs", "unknown-cap", "derive-pages"]);

        PluginCapabilityEnforcer.Enforce(plugin, "after-build");
        PluginCapabilityEnforcer.Enforce(plugin, "derive-pages");
    }

    private static ExternalPluginConfig CreatePlugin(IReadOnlyList<string>? capabilities)
    {
        return new ExternalPluginConfig
        {
            Runtime = "process",
            Entry = "./plugin",
            Hooks = new List<string> { "derive-pages", "after-build" },
            Capabilities = capabilities
        };
    }
}
