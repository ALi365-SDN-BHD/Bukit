namespace Bukit.Engine;

internal static class StaticFilePathPolicy
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".git", ".github", ".svn", ".hg", ".DS_Store", "Thumbs.db",
        ".npmrc", ".yarnrc"
    };

    private static readonly string[] SensitiveExtensions = [".pem", ".key", ".pfx", ".p12"];

    internal static bool IsDefaultAllowedDotfileSegment(string segment)
        => segment.Equals(".well-known", StringComparison.OrdinalIgnoreCase);

    internal static bool IsSensitiveSegment(string segment)
    {
        if (SensitiveNames.Contains(segment)
            || segment.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SensitiveExtensions.Any(extension =>
            segment.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool HasSensitiveSegment(string relativePath)
        => EnumerateSegments(relativePath).Any(IsSensitiveSegment);

    internal static bool HasDisallowedDotPrefixedSegment(string relativePath)
        => EnumerateSegments(relativePath).Any(segment =>
            segment.StartsWith('.') && !IsDefaultAllowedDotfileSegment(segment));

    private static IEnumerable<string> EnumerateSegments(string relativePath)
        => relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
}
