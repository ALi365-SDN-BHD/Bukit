using Bukit.Plugin.Abstractions.Security;
using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed class PluginPermissionEvaluator
{
    private readonly PluginFileSystemPermissionEvaluator _fileSystemEvaluator;

    public PluginPermissionEvaluator(PluginFileSystemPermissionEvaluator? fileSystemEvaluator = null)
    {
        _fileSystemEvaluator = fileSystemEvaluator ?? new PluginFileSystemPermissionEvaluator();
    }

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
        ValidateFileSystemPermissionPaths(granted);
        ValidateFileSystemPermissionPaths(required);

        if (required.Network && !granted.Network)
        {
            throw new ConfigException($"Plugin {pluginId} requires network permission.", DiagnosticCode.PluginCapabilityMissing);
        }

        _fileSystemEvaluator.ValidateSubset(pluginId, "fileSystem.read", granted.FileSystem.Read, required.FileSystem.Read);
        _fileSystemEvaluator.ValidateSubset(pluginId, "fileSystem.write", granted.FileSystem.Write, required.FileSystem.Write);
        ValidateSubset(pluginId, "environment.read", granted.Environment.Read, required.Environment.Read);
    }

    public static void ValidateFileSystemPermissionPaths(PluginPermissionSet permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var evaluator = new PluginFileSystemPermissionEvaluator();
        evaluator.ValidatePaths("fileSystem.read", permissions.FileSystem.Read);
        evaluator.ValidatePaths("fileSystem.write", permissions.FileSystem.Write);
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
