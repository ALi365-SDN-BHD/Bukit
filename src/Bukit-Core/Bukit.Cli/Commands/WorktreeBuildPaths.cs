using System.Security.Cryptography;
using System.Text;
using Bukit.Engine;
using Bukit.Engine.Incremental;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal static class WorktreeBuildPaths
{
    internal static void PrepareStaging(string source, string staging, bool copyExisting)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(staging)!);
        Directory.CreateDirectory(staging);
        if (copyExisting && Directory.Exists(source))
        {
            foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
            {
                var destination = Path.Combine(staging, entry.Name);
                if (entry.LinkTarget is { } target)
                {
                    if (entry is DirectoryInfo)
                        Directory.CreateSymbolicLink(destination, target);
                    else
                        File.CreateSymbolicLink(destination, target);
                }
                else if (entry is DirectoryInfo)
                {
                    PrepareStaging(entry.FullName, destination, copyExisting: true);
                }
                else
                {
                    File.Copy(entry.FullName, destination);
                }
            }
        }
    }

    internal static void CopyVariantManifest(string stagingCache, string workerCache, string language, string outputDir)
    {
        var fileName = $"build-manifest.{BuildPathUtils.SanitizeFileSegment(language)}.json";
        var source = Path.Combine(stagingCache, fileName);
        if (!File.Exists(source))
        {
            return;
        }
        var manifest = BuildManifest.Load(source);
        manifest.OutputRoot = Path.GetFullPath(outputDir);
        manifest.Save(Path.Combine(workerCache, fileName));
    }

    internal static void RewriteManifestRoots(
        string cacheDir,
        string oldOutputRoot,
        string newOutputRoot,
        IReadOnlyList<string> languages)
    {
        RewriteManifest(Path.Combine(cacheDir, "build-manifest.json"), oldOutputRoot, newOutputRoot);
        foreach (var language in languages)
        {
            var fileName = $"build-manifest.{BuildPathUtils.SanitizeFileSegment(language)}.json";
            RewriteManifest(
                Path.Combine(cacheDir, fileName),
                Path.Combine(oldOutputRoot, language),
                Path.Combine(newOutputRoot, language));
        }
    }

    private static void RewriteManifest(string path, string oldRoot, string newRoot)
    {
        if (!File.Exists(path))
        {
            return;
        }
        var manifest = BuildManifest.Load(path);
        if (string.IsNullOrEmpty(manifest.OutputRoot) ||
            string.Equals(manifest.OutputRoot, Path.GetFullPath(oldRoot), PlatformPathHelper.PathComparison))
        {
            manifest.OutputRoot = Path.GetFullPath(newRoot);
            manifest.Save(path);
        }
    }

    internal static MetricsPaths ResolveMetricsPaths(
        string? configuredPath,
        string projectRoot,
        string finalOutput,
        string stagingOutput,
        string runRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new MetricsPaths(null, []);
        }
        var finalPath = Path.GetFullPath(Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(projectRoot, configuredPath));
        if (IsSameOrChild(finalOutput, finalPath))
        {
            return new MetricsPaths(Path.Combine(stagingOutput, Path.GetRelativePath(finalOutput, finalPath)), []);
        }
        var stagingPath = Path.Combine(runRoot, "metrics", Path.GetFileName(finalPath));
        return new MetricsPaths(stagingPath,
        [
            (stagingPath, finalPath, Sibling(finalPath, "bukit-backup")),
            (Path.ChangeExtension(stagingPath, ".html"), Path.ChangeExtension(finalPath, ".html"), Sibling(Path.ChangeExtension(finalPath, ".html"), "bukit-backup"))
        ]);
    }

    internal static string MapIntoWorktree(GitRepository repository, string path, string worktreeRoot)
        => Path.GetFullPath(Path.Combine(worktreeRoot, RelativeInside(repository.Root, path, "Site path")));

    internal static string RelativeInside(string root, string path, string description)
    {
        var fullRoot = PathUtils.GetCanonicalFullPath(root);
        var fullPath = PathUtils.GetCanonicalFullPath(path);
        if (!IsSameOrChild(fullRoot, fullPath))
        {
            throw new WorktreeBuildException("i18n-worktree-input-untracked", $"{description} must be inside the Git repository: {fullPath}");
        }
        return Path.GetRelativePath(fullRoot, fullPath);
    }

    internal static bool IsSameOrChild(string root, string path)
        => PathUtils.IsSameOrSubPathOf(path, root);

    internal static void EnsureDisjoint(string left, string right)
    {
        if (IsSameOrChild(left, right) || IsSameOrChild(right, left))
        {
            throw new WorktreeBuildException("i18n-worktree-input-untracked", "Build output and cache directories must not overlap for a multilingual Worktree build.");
        }
    }

    internal static string Sibling(string path, string suffix)
    {
        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new WorktreeBuildException("i18n-worktree-result-invalid", $"Path has no parent directory: {fullPath}");
        var name = Path.GetFileName(fullPath);
        if (name.Length == 0)
        {
            throw new WorktreeBuildException("i18n-worktree-result-invalid", $"Unsafe transaction path: {fullPath}");
        }
        return Path.Combine(parent, $".{name}.{suffix}");
    }

    internal static string RunsRoot(string repositoryRoot)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(repositoryRoot))))[..16];
        return Path.Combine(Path.GetTempPath(), "bukit-i18n-worktrees", hash);
    }

    internal static FileStream AcquireRunLock(string repositoryRoot)
    {
        var runsRoot = RunsRoot(repositoryRoot);
        Directory.CreateDirectory(runsRoot);
        RestrictDirectory(runsRoot);
        // ponytail: serialize builds per checkout; use per-output locks only if concurrent builds become necessary.
        // Keep the lock file: deleting it could let another process lock a different inode.
        try
        {
            return new FileStream(Path.Combine(runsRoot, "build.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex)
        {
            throw new WorktreeBuildException("i18n-worktree-worker-failed", "Cannot acquire the multilingual build lock; another build may still be running.", ex);
        }
    }

    internal static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    internal static void RestrictDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    internal static void RestrictFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}

internal sealed record MetricsPaths(
    string? StagingPath,
    IReadOnlyList<(string Staging, string Final, string Backup)> ExternalCommit);
