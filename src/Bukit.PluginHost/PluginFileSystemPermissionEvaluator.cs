using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed class PluginFileSystemPermissionEvaluator
{
    private readonly PluginPermissionPathNormalizer _pathNormalizer;

    public PluginFileSystemPermissionEvaluator(PluginPermissionPathNormalizer? pathNormalizer = null)
    {
        _pathNormalizer = pathNormalizer ?? new PluginPermissionPathNormalizer();
    }

    public void ValidatePaths(string pluginId, string permissionName, IReadOnlyList<string> paths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(paths);

        foreach (string path in paths)
        {
            _ = _pathNormalizer.Normalize(permissionName, path, pluginId);
        }
    }

    public void ValidateSubset(
        string pluginId,
        string permissionName,
        IReadOnlyList<string> granted,
        IReadOnlyList<string> required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(granted);
        ArgumentNullException.ThrowIfNull(required);

        string[] normalizedGranted = granted
            .Select(path => _pathNormalizer.Normalize(permissionName, path, pluginId))
            .ToArray();

        foreach (string requiredPath in required)
        {
            string normalizedRequired = _pathNormalizer.Normalize(permissionName, requiredPath, pluginId);
            if (!normalizedGranted.Any(grantedPath => Covers(grantedPath, normalizedRequired)))
            {
                throw new ConfigException(
                    $"Plugin {pluginId} requires {permissionName} permission: {requiredPath}.",
                    DiagnosticCode.PluginCapabilityMissing);
            }
        }
    }

    private static bool Covers(string grantedPath, string requiredPath)
    {
        if (string.Equals(grantedPath, ".", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(grantedPath, requiredPath, StringComparison.Ordinal)
            || requiredPath.StartsWith($"{grantedPath}/", StringComparison.Ordinal);
    }
}
