using Bukit.Shared;

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
    /// output artifact, either lexically or through a symlink alias whose resolved
    /// physical target is internal. Servers should answer 404 to avoid leaking
    /// existence.
    /// </summary>
    public static bool IsInternalOutputPath(string outputDir, string candidate)
    {
        var fullOutputDir = Path.GetFullPath(outputDir);
        if (IsInternalRelativeTo(fullOutputDir, candidate))
        {
            return true;
        }

        // Confinement proves the resolved physical target stays inside the root, so a
        // root-internal symlink alias (e.g. public-reports -> .bukit) passes it even
        // though the served content is internal: the lexical first segment does not
        // describe what File.OpenRead will actually follow. Classify the resolved
        // physical target as well so such aliases are denied.
        var physicalCandidate = PathUtils.GetCanonicalFullPath(candidate);
        var physicalRoot = PathUtils.GetCanonicalFullPath(fullOutputDir);
        return IsInternalRelativeTo(physicalRoot, physicalCandidate);
    }

    private static bool IsInternalRelativeTo(string outputDir, string candidate)
    {
        var relative = Path.GetRelativePath(outputDir, candidate)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (segments.Any(segment => string.Equals(segment, ".bukit", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return segments.Length == 1 &&
            (string.Equals(segments[0], ".bukit-build-state.json", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(segments[0], ".bukit-output-marker", StringComparison.OrdinalIgnoreCase));
    }

}
