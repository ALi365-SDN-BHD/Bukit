namespace Bukit.Cli.Commands.Dev;

/// <summary>
/// Shared deny-list for internal build artifacts that local static servers (dev and
/// preview) must never serve. The <c>.bukit</c> directory and root-level state files
/// hold build reports, provenance tokens, and audit data: internal identity material,
/// not public site assets.
/// </summary>
internal static class StaticServerInternalPathPolicy
{
    /// <summary>
    /// Returns true when <paramref name="candidate"/> (an absolute path already
    /// confined to <paramref name="outputDir"/> by the caller) addresses an internal
    /// output artifact. Servers should answer 404 to avoid leaking existence.
    /// </summary>
    public static bool IsInternalOutputPath(string outputDir, string candidate)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(outputDir), candidate)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (string.Equals(segments[0], ".bukit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return segments.Length == 1 &&
            (string.Equals(segments[0], ".bukit-build-state.json", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(segments[0], ".bukit-output-marker", StringComparison.OrdinalIgnoreCase));
    }
}
