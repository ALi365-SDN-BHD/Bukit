using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal static class DevPathGuard
{
    /// <summary>
    /// Resolves <paramref name="relativeUrlPath"/> against <paramref name="rootDir"/> and ensures
    /// the resulting absolute path stays inside the root directory.
    /// </summary>
    /// <returns>
    /// The safe absolute path, or <c>null</c> when the resolved path escapes the root.
    /// Callers should translate <c>null</c> into a 403 response (no exception is thrown).
    /// </returns>
    public static string? TryResolveWithinRoot(string rootDir, string relativeUrlPath)
    {
        if (string.IsNullOrEmpty(rootDir))
        {
            return null;
        }

        var relative = (relativeUrlPath ?? string.Empty)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var fullRoot = Path.GetFullPath(rootDir);

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(fullRoot, relative));
        }
        catch (Exception)
        {
            return null;
        }

        if (!PathUtils.IsSameOrSubPathOf(candidate, fullRoot))
        {
            return null;
        }

        return candidate;
    }
}
