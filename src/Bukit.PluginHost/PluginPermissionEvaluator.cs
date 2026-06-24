using Bukit.Plugin.Abstractions.Security;
using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed class PluginPermissionEvaluator
{
    public void ValidateGrantedPermissions(
        string pluginId,
        PluginPermissionSet granted,
        PluginPermissionSet required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(granted);
        ArgumentNullException.ThrowIfNull(required);

        ValidateNoWildcard(granted.Environment.Read);
        ValidateNoWildcard(required.Environment.Read);

        if (required.Network && !granted.Network)
        {
            throw new ConfigException($"Plugin {pluginId} requires network permission.", DiagnosticCode.PluginCapabilityMissing);
        }

        ValidateSubset(pluginId, "fileSystem.read", granted.FileSystem.Read, required.FileSystem.Read);
        ValidateSubset(pluginId, "fileSystem.write", granted.FileSystem.Write, required.FileSystem.Write);
        ValidateSubset(pluginId, "environment.read", granted.Environment.Read, required.Environment.Read);
    }

    public static void ValidateNoEnvironmentWildcard(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        ValidateNoWildcard(names);
    }

    private static void ValidateNoWildcard(IReadOnlyList<string> names)
    {
        if (names.Any(name => string.Equals(name, "*", StringComparison.Ordinal)))
        {
            throw new ConfigException("environment.read cannot contain '*'.", DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static void ValidateSubset(
        string pluginId,
        string permissionName,
        IReadOnlyList<string> granted,
        IReadOnlyList<string> required)
    {
        var allowed = new HashSet<string>(granted, StringComparer.Ordinal);
        foreach (string value in required)
        {
            if (!allowed.Contains(value))
            {
                throw new ConfigException(
                    $"Plugin {pluginId} requires {permissionName} permission: {value}.",
                    DiagnosticCode.PluginCapabilityMissing);
            }
        }
    }
}
