using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

/// <summary>
/// Worktrees only contain committed files, but a custom <c>content.media.downloadDir</c>
/// is often git-ignored. The clean check guarantees every non-ignored media file is
/// committed, so copying the files a worktree is missing gives it the same media as the
/// project: first from the project into the content worktree, then from the content
/// worktree (which may also hold freshly downloaded media) into each worker.
/// Existing files are never overwritten. The default media cache is shared with workers
/// through the build request and needs no overlay.
/// </summary>
internal static class WorktreeMediaOverlay
{
    private static readonly EnumerationOptions SkipLinks = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = false
    };

    /// <summary>
    /// Returns the custom media directory relative to the repository root, or null when
    /// media lives in the Bukit cache.
    /// </summary>
    internal static string? ResolveRelativeMediaDir(AppConfig config, string rootDir, GitRepository repository)
    {
        var mediaDir = (config.Content.Media.DownloadDir ?? string.Empty).Trim();
        if (mediaDir.Length == 0 || string.Equals(mediaDir, "assets/uploads", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return WorktreeBuildPaths.RelativeInside(
            repository.Root,
            BuildPathUtils.MakeAbsolute(rootDir, mediaDir),
            "Media directory");
    }

    /// <summary>Copies files missing under <paramref name="targetDir"/>; returns how many were copied.</summary>
    internal static int CopyMissing(string sourceDir, string targetDir, string worktreeRoot)
    {
        if (!Directory.Exists(sourceDir))
        {
            return 0;
        }

        var source = PathUtils.GetCanonicalFullPath(sourceDir);
        var target = PathUtils.GetCanonicalFullPath(targetDir);
        // A committed absolute symlink can make the worktree path resolve back to the
        // project directory; never write through it.
        if (string.Equals(source, target, PlatformPathHelper.PathComparison) ||
            !WorktreeBuildPaths.IsSameOrChild(worktreeRoot, target))
        {
            return 0;
        }

        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(source, "*", SkipLinks))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            if (File.Exists(destination))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
            copied++;
        }

        return copied;
    }
}
