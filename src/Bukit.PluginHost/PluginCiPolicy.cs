using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed class PluginCiPolicy
{
    public void Validate(
        string pluginId,
        PluginConfigEntry entry,
        PluginPlatformEntry platform,
        bool sha256Verified,
        bool isCi)
    {
        if (!isCi)
        {
            return;
        }

        if (!entry.AllowInCi)
        {
            throw new ConfigException($"Plugin {pluginId} cannot run in CI unless allowInCi=true.", DiagnosticCode.PluginExecutionFailed);
        }

        if (string.IsNullOrWhiteSpace(platform.Sha256) || !sha256Verified)
        {
            throw new ConfigException($"Plugin {pluginId} cannot run in CI without verified sha256.", DiagnosticCode.PluginExecutionFailed);
        }

        if (!entry.PermissionsExplicit)
        {
            throw new ConfigException($"Plugin {pluginId} cannot run in CI without explicit permissions.", DiagnosticCode.PluginExecutionFailed);
        }
    }
}
