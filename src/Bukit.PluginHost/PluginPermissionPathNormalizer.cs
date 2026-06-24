using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed class PluginPermissionPathNormalizer
{
    public string Normalize(string permissionName, string path, string? pluginId = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw Invalid(permissionName, path);
        }

        string normalized = path.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || IsWindowsRootedPath(normalized))
        {
            throw Invalid(permissionName, path);
        }

        var segments = new List<string>();
        foreach (string segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is ".")
            {
                continue;
            }

            if (segment is "..")
            {
                throw Invalid(permissionName, path);
            }

            segments.Add(segment);
        }

        if (segments.Count >= 1
            && string.Equals(segments[0], ".bukit", StringComparison.Ordinal)
            && !IsAllowedPluginBukitPath(segments, pluginId))
        {
            throw Invalid(permissionName, path);
        }

        return segments.Count == 0 ? "." : string.Join('/', segments);
    }

    private static bool IsWindowsRootedPath(string path)
        => path.Length >= 3
            && char.IsLetter(path[0])
            && path[1] == ':'
            && path[2] == '/';

    private static bool IsAllowedPluginBukitPath(IReadOnlyList<string> segments, string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        return IsPluginReportOutputPath(segments, pluginId)
            || IsPluginTempPath(segments, pluginId);
    }

    private static bool IsPluginReportOutputPath(IReadOnlyList<string> segments, string pluginId)
        => segments.Count >= 4
            && string.Equals(segments[1], "reports", StringComparison.Ordinal)
            && string.Equals(segments[2], "plugin-output", StringComparison.Ordinal)
            && string.Equals(segments[3], pluginId, StringComparison.Ordinal);

    private static bool IsPluginTempPath(IReadOnlyList<string> segments, string pluginId)
        => segments.Count >= 3
            && string.Equals(segments[1], "tmp", StringComparison.Ordinal)
            && string.Equals(segments[2], pluginId, StringComparison.Ordinal);

    private static ConfigException Invalid(string permissionName, string path)
        => new(
            $"{permissionName} permission path must be a safe project-relative path: {path}.",
            DiagnosticCode.ConfigInvalidValue);
}
