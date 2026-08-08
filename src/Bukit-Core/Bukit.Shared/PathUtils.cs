namespace Bukit.Shared;

public static class PathUtils
{
    public static bool IsSubPathOf(string child, string parent)
    {
        var childPath = NormalizeFullPath(child, resolveSymlinks: true);
        var parentPath = NormalizeFullPath(parent, resolveSymlinks: true);
        var parentWithSeparator = EnsureTrailingSeparator(parentPath);

        return childPath.StartsWith(parentWithSeparator, PlatformPathHelper.PathComparison);
    }

    public static bool IsSameOrSubPathOf(string child, string parent)
    {
        var childPath = NormalizeFullPath(child, resolveSymlinks: true);
        var parentPath = NormalizeFullPath(parent, resolveSymlinks: true);

        return string.Equals(childPath, parentPath, PlatformPathHelper.PathComparison)
            || childPath.StartsWith(EnsureTrailingSeparator(parentPath), PlatformPathHelper.PathComparison);
    }

    private static string NormalizeFullPath(string path, bool resolveSymlinks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = TrimTrailingSeparators(Path.GetFullPath(path));
        return resolveSymlinks ? ResolveExistingLinks(fullPath) : fullPath;
    }

    private static string ResolveExistingLinks(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return fullPath;
        }

        var current = TrimTrailingSeparators(root);
        var remainder = fullPath[root.Length..]
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < remainder.Length; i++)
        {
            var candidate = Path.Combine(current, remainder[i]);
            var resolved = ResolveLinkTarget(candidate);
            if (resolved is null)
            {
                for (var j = i; j < remainder.Length; j++)
                {
                    current = Path.Combine(current, remainder[j]);
                }

                return TrimTrailingSeparators(Path.GetFullPath(current));
            }

            current = resolved;
        }

        return TrimTrailingSeparators(Path.GetFullPath(current));
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
            // Link targets are returned verbatim: an absolute target whose own prefix
            // contains a link (e.g. /var/... on macOS, where /var -> /private/var)
            // stays unnormalized while component walks from the volume root resolve
            // that prefix. Re-normalize the target so both directions agree; target
            // components are already final targets, so the recursion converges.
            resolved = ResolveExistingLinks(resolved);
        }

        return TrimTrailingSeparators(resolved);
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string TrimTrailingSeparators(string path)
    {
        var root = Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root) &&
            string.Equals(path, root, PlatformPathHelper.PathComparison))
        {
            return root;
        }

        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.IsNullOrEmpty(trimmed) && !string.IsNullOrEmpty(root) ? root : trimmed;
    }
}
