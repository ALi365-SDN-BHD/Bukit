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
        var physicalCandidate = ResolveExistingLinks(candidate);
        var physicalRoot = ResolveExistingLinks(fullOutputDir);
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

        if (string.Equals(segments[0], ".bukit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return segments.Length == 1 &&
            (string.Equals(segments[0], ".bukit-build-state.json", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(segments[0], ".bukit-output-marker", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Mirrors <c>PathUtils.ResolveExistingLinks</c> (private in Bukit.Shared): resolves
    /// every existing path component to its final link target and keeps a non-existing
    /// tail as-is, so classification of a missing path stays lexical.
    /// </summary>
    private static string ResolveExistingLinks(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return fullPath;
        }

        var current = root;
        var remainder = fullPath[root.Length..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < remainder.Length; i++)
        {
            var component = Path.Combine(current, remainder[i]);
            var resolved = ResolveLinkTarget(component);
            if (resolved is null)
            {
                for (var j = i; j < remainder.Length; j++)
                {
                    current = Path.Combine(current, remainder[j]);
                }

                return Path.GetFullPath(current);
            }

            current = resolved;
        }

        return Path.GetFullPath(current);
    }

    private static string? ResolveLinkTarget(string path)
    {
        FileSystemInfo info;
        if (Directory.Exists(path))
        {
            info = new DirectoryInfo(path);
        }
        else if (File.Exists(path))
        {
            info = new FileInfo(path);
        }
        else
        {
            return null;
        }

        var target = info.ResolveLinkTarget(returnFinalTarget: true);
        var resolved = Path.GetFullPath(target?.FullName ?? info.FullName);
        if (target is not null)
        {
            // Normalize link targets whose own prefix still contains a link (e.g.
            // /var/... on macOS) so physical classification matches the guard walk.
            resolved = ResolveExistingLinks(resolved);
        }

        return resolved.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
