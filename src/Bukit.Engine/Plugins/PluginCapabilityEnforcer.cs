using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine.Plugins;

using Bukit.Engine.Abstractions.Plugins;

public static class PluginCapabilityEnforcer
{
    public static void Enforce(ExternalPluginConfig plugin, string hook)
    {
        if (plugin.Capabilities is null or { Count: 0 })
        {
            return;
        }

        var requiredCapability = GetRequiredCapability(hook);
        if (requiredCapability is null)
        {
            return;
        }

        var hasCapability = false;
        foreach (var cap in plugin.Capabilities)
        {
            if (string.Equals(cap, requiredCapability, StringComparison.OrdinalIgnoreCase))
            {
                hasCapability = true;
                break;
            }
        }

        if (!hasCapability)
        {
            throw new ConfigException(
                $"Plugin '{plugin.Entry}' is missing required capability '{requiredCapability}' for hook '{hook}'. " +
                $"Declared capabilities: [{string.Join(", ", plugin.Capabilities)}]. " +
                $"How to fix: add '{requiredCapability}' to the plugin's capabilities list in site.yaml.",
                DiagnosticCode.PluginExecutionFailed);
        }
    }

    private static string? GetRequiredCapability(string hook)
    {
        return hook.Trim().ToLowerInvariant() switch
        {
            "derive-pages" => PluginCapability.DerivePages,
            "after-build" => PluginCapability.EmitOutputs,
            _ => null
        };
    }
}
