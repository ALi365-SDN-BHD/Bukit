namespace Bukit.Config;

/// <summary>
/// Normalization shared by shared-asset config validation and the runtime scope.
/// A prefix is always relative to the output root and ends with '/'.
/// </summary>
internal static class SharedAssetsPath
{
    internal static string Normalize(string? value)
    {
        var segments = (value ?? string.Empty)
            .Trim()
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !string.Equals(segment, ".", StringComparison.Ordinal));
        var normalized = string.Join('/', segments);
        return normalized.Length == 0 ? string.Empty : normalized + "/";
    }

    internal static bool IsRoot(string normalized) => normalized.Length == 0;

    internal static bool IsAbsolute(string value)
    {
        var normalized = (value ?? string.Empty).Trim().Replace('\\', '/');
        return normalized.StartsWith('/') ||
               (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':');
    }

    internal static bool IsInside(string normalizedPath, string prefix)
        => normalizedPath.StartsWith(prefix, StringComparison.Ordinal);

    internal static bool ContainsParentSegment(string normalizedPath)
        => normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
}
