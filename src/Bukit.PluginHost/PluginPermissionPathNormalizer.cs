using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed class PluginPermissionPathNormalizer
{
    public string Normalize(string permissionName, string path)
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

        if (segments.Count >= 2
            && string.Equals(segments[0], ".bukit", StringComparison.Ordinal)
            && IsExecutableBukitArea(segments[1]))
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

    private static bool IsExecutableBukitArea(string segment)
        => segment is "bin" or "plugins" or "tools" or "plugin-executables";

    private static ConfigException Invalid(string permissionName, string path)
        => new(
            $"{permissionName} permission path must be a safe project-relative path: {path}.",
            DiagnosticCode.ConfigInvalidValue);
}
