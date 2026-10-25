using Bukit.Shared;

namespace Bukit.Engine.Output;

internal class SafePathResolver : IOutputPathPolicy
{
    private static readonly char[] PathSeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    public string ResolveSafePath(string outputRoot, string relativePath)
    {
        var fullRoot = TrimTrailingSeparators(Path.GetFullPath(outputRoot));
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var safeRoot = EnsureTrailingSeparator(fullRoot);

        if (!fullPath.StartsWith(safeRoot, PlatformPathHelper.PathComparison))
        {
            throw new OutputPathSecurityException(
                $"Path traversal detected: resolved path '{fullPath}' escapes output root '{safeRoot}'.");
        }

        RejectEscapingSymlinkSegments(fullRoot, fullPath);

        return fullPath;
    }

    private static void RejectEscapingSymlinkSegments(string fullRoot, string fullPath)
    {
        var relativePath = Path.GetRelativePath(fullRoot, fullPath);
        if (relativePath == ".")
        {
            return;
        }

        var current = fullRoot;
        foreach (var segment in relativePath.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var info = GetExistingFileSystemInfo(current);
            if (info is null)
            {
                return;
            }

            if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
            {
                continue;
            }

            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            if (target is null)
            {
                throw new OutputPathSecurityException(
                    $"Path traversal detected: symlink path '{current}' cannot be resolved inside output root '{fullRoot}'.");
            }

            var resolvedTarget = TrimTrailingSeparators(Path.GetFullPath(target.FullName));
            if (!PathUtils.IsSameOrSubPathOf(resolvedTarget, fullRoot))
            {
                throw new OutputPathSecurityException(
                    $"Path traversal detected: symlink path '{current}' resolves outside output root '{fullRoot}'.");
            }
        }
    }

    private static FileSystemInfo? GetExistingFileSystemInfo(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Directory) != 0
                ? new DirectoryInfo(path)
                : new FileInfo(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
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
