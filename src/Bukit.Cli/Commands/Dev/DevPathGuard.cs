using Bukit.Shared;
using System.Text;

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

        var relative = DecodePath(relativeUrlPath ?? string.Empty);
        if (relative is null)
        {
            return null;
        }

        relative = relative
            .Normalize(NormalizationForm.FormKC)
            .Replace('\\', '/');

        if (relative.IndexOf('\0') >= 0)
        {
            return null;
        }

        var segments = relative
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(static segment => segment != ".")
            .ToArray();

        if (segments.Any(static segment => segment == ".."))
        {
            return null;
        }

        var fullRoot = Path.GetFullPath(rootDir);

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(new[] { fullRoot }.Concat(segments).ToArray()));
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

    private static string? DecodePath(string path)
    {
        var current = path;
        for (var i = 0; i < 3; i++)
        {
            if (current.IndexOf('\0') >= 0)
            {
                return null;
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(current);
            }
            catch (UriFormatException)
            {
                return null;
            }

            if (string.Equals(decoded, current, StringComparison.Ordinal))
            {
                return decoded;
            }

            current = decoded;
        }

        return current.IndexOf('\0') >= 0 ? null : current;
    }
}
