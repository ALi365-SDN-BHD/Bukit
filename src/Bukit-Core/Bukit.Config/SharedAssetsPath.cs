namespace Bukit.Config;

/// <summary>
/// Normalization shared by shared-asset config validation and the runtime scope.
/// A prefix is always relative to the output root and ends with '/'.
/// </summary>
internal static class SharedAssetsPath
{
    internal static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        return normalized.Length == 0 || normalized == "." ? string.Empty : normalized + "/";
    }

    internal static bool IsRoot(string normalized) => normalized.Length == 0;

    internal static bool IsAbsolute(string value)
        => Path.IsPathRooted(value) ||
           (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':');

    internal static bool IsInside(string normalizedPath, string prefix)
        => normalizedPath.StartsWith(prefix, StringComparison.Ordinal);

    internal static bool ContainsParentSegment(string normalizedPath)
        => normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
}
