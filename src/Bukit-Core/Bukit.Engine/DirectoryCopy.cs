using System.Security.Cryptography;
using Bukit.Engine.Output;
using Bukit.Shared;

namespace Bukit.Engine;

public sealed record DirectoryCopyOptions
{
    public string HashMode { get; init; } = "size-time";
    public bool Prune { get; init; }
    public bool IgnoreDotPrefixedFiles { get; init; } = true;
    public bool AlwaysDenySensitiveDotfiles { get; init; } = true;
    public bool FollowSymlinks { get; init; }
    public IReadOnlySet<string>? DotfileAllowList { get; init; }
    public IReadOnlySet<string>? DotfileDenyList { get; init; }
}

internal sealed record DirectoryCopyItem(
    string SourcePath,
    string RelativePath,
    string PhysicalSourceRoot);

public static class DirectoryCopy
{
    public static void Copy(string sourceDir, string destinationDir, string? outputRoot = null, IOutputPathPolicy? pathPolicy = null)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (ShouldSkipDotfile(name))
            {
                continue;
            }

            if (IsSymlink(file))
            {
                Console.Error.WriteLine($"[warn] Skipping symlink: {file}");
                continue;
            }

            var dest = Path.Combine(destinationDir, name);
            if (outputRoot is not null)
            {
                FileWriter.GetSafeFullPath(outputRoot, Path.GetRelativePath(outputRoot, dest), pathPolicy);
            }
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (ShouldSkipDotfile(name))
            {
                continue;
            }

            if (IsSymlink(dir))
            {
                Console.Error.WriteLine($"[warn] Skipping symlink directory: {dir}");
                continue;
            }

            var dest = Path.Combine(destinationDir, name);
            Copy(dir, dest, outputRoot, pathPolicy);
        }
    }

    public static void Sync(string sourceDir, string destinationDir, bool prune = false, string? outputRoot = null, IOutputPathPolicy? pathPolicy = null)
        => Sync(sourceDir, destinationDir, new DirectoryCopyOptions { Prune = prune, IgnoreDotPrefixedFiles = true }, outputRoot, pathPolicy);

    public static void Sync(string sourceDir, string destinationDir, DirectoryCopyOptions options, string? outputRoot = null, IOutputPathPolicy? pathPolicy = null)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (ShouldSkipDotfile(name, options))
            {
                continue;
            }

            if (!options.FollowSymlinks && IsSymlink(file))
            {
                Console.Error.WriteLine($"[warn] Skipping symlink: {file}");
                continue;
            }

            if (options.FollowSymlinks && IsSymlink(file) &&
                !TryResolveSafeTarget(file, ResolvePhysicalPath(sourceDir) ?? sourceDir, options, out _))
            {
                Console.Error.WriteLine($"[warn] Skipping symlink outside source directory: {file}");
                continue;
            }

            SyncFile(file, destinationDir, options.HashMode, outputRoot, pathPolicy);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (ShouldSkipDotfile(name, options))
            {
                continue;
            }

            if (!options.FollowSymlinks && IsSymlink(dir))
            {
                Console.Error.WriteLine($"[warn] Skipping symlink directory: {dir}");
                continue;
            }

            if (options.FollowSymlinks && IsSymlink(dir) &&
                !TryResolveSafeTarget(dir, ResolvePhysicalPath(sourceDir) ?? sourceDir, options, out _))
            {
                Console.Error.WriteLine($"[warn] Skipping symlink directory outside source directory: {dir}");
                continue;
            }

            var dest = Path.Combine(destinationDir, name);
            Sync(dir, dest, options, outputRoot, pathPolicy);
        }

        if (options.Prune)
        {
            PruneDestination(sourceDir, destinationDir);
        }
    }

    internal static IReadOnlyList<DirectoryCopyItem> EnumerateFilesForSync(
        string sourceDir,
        DirectoryCopyOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceDir))
        {
            return Array.Empty<DirectoryCopyItem>();
        }

        var results = new List<DirectoryCopyItem>();
        var sourceRoot = Path.GetFullPath(sourceDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var physicalSourceRoot = ResolvePhysicalPath(sourceRoot) ?? sourceRoot;
        var ancestors = new HashSet<string>(PathComparer)
        {
            physicalSourceRoot
        };
        EnumerateDirectory(
            sourceRoot,
            string.Empty,
            physicalSourceRoot,
            options,
            ancestors,
            results,
            cancellationToken);
        return results
            .OrderBy(item => item.RelativePath, PathComparer)
            .ToArray();
    }

    internal static void SyncPlannedFile(
        string sourceFile,
        string destinationFile,
        string hashMode,
        string outputRoot,
        string expectedPhysicalSourceRoot,
        DirectoryCopyOptions options,
        IOutputPathPolicy? pathPolicy = null)
    {
        var capturedSourceRoot = Path.GetFullPath(expectedPhysicalSourceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var currentSourceRoot = ResolvePhysicalPath(capturedSourceRoot);
        if (currentSourceRoot is null || !PathComparer.Equals(capturedSourceRoot, currentSourceRoot))
        {
            throw new IOException($"Planned asset source root changed after validation: '{expectedPhysicalSourceRoot}'.");
        }

        if (!TryResolveSafeTarget(sourceFile, capturedSourceRoot, options, out var validatedSource) ||
            !options.FollowSymlinks && !PathComparer.Equals(Path.GetFullPath(sourceFile), validatedSource))
        {
            throw new IOException($"Planned asset source changed after validation: '{sourceFile}'.");
        }

        var destinationDir = Path.GetDirectoryName(destinationFile)!;
        Directory.CreateDirectory(destinationDir);
        SyncFileToPath(validatedSource, destinationFile, hashMode, outputRoot, pathPolicy);
    }

    public static void SyncFiles(string sourceDir, string destinationDir, bool ignoreDotPrefixedFiles = false, string? outputRoot = null, IOutputPathPolicy? pathPolicy = null)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        var skipOptions = new DirectoryCopyOptions { IgnoreDotPrefixedFiles = ignoreDotPrefixedFiles };

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (ShouldSkipDotfile(name, skipOptions))
            {
                continue;
            }

            if (IsSymlink(file))
            {
                Console.Error.WriteLine($"[warn] Skipping symlink: {file}");
                continue;
            }

            SyncFile(file, destinationDir, "size-time", outputRoot, pathPolicy);
        }
    }

    public static void SyncFilesRecursive(string sourceDir, string destinationDir, bool ignoreDotPrefixedFiles = false, string? outputRoot = null, IOutputPathPolicy? pathPolicy = null)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        var skipOptions = new DirectoryCopyOptions { IgnoreDotPrefixedFiles = ignoreDotPrefixedFiles };

        foreach (var file in SafeFileEnumerator.EnumerateFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (ShouldSkipDotfile(name, skipOptions))
            {
                continue;
            }

            if (IsSymlink(file))
            {
                Console.Error.WriteLine($"[warn] Skipping symlink: {file}");
                continue;
            }

            var relativeDirectory = Path.GetRelativePath(sourceDir, Path.GetDirectoryName(file)!);
            var destinationSubdir = relativeDirectory == "."
                ? destinationDir
                : Path.Combine(destinationDir, relativeDirectory);
            Directory.CreateDirectory(destinationSubdir);
            SyncFile(file, destinationSubdir, "size-time", outputRoot, pathPolicy);
        }
    }

    private static bool ShouldSkipDotfile(string name, DirectoryCopyOptions? options = null)
    {
        if (options?.DotfileAllowList?.Contains(name) == true)
        {
            return false;
        }

        if (StaticFilePathPolicy.IsDefaultAllowedDotfileSegment(name))
        {
            return false;
        }

        if (options?.AlwaysDenySensitiveDotfiles != false)
        {
            if (options?.DotfileDenyList?.Contains(name) == true)
            {
                return true;
            }

            if (StaticFilePathPolicy.IsSensitiveSegment(name))
            {
                return true;
            }
        }

        if (options != null && options.IgnoreDotPrefixedFiles == false)
        {
            return false;
        }

        if (!name.StartsWith('.'))
        {
            return false;
        }

        return true;
    }

    private static void EnumerateDirectory(
        string currentDir,
        string relativeDir,
        string physicalSourceRoot,
        DirectoryCopyOptions options,
        HashSet<string> ancestors,
        List<DirectoryCopyItem> results,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.GetFiles(currentDir).OrderBy(path => path, PathComparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            if (ShouldSkipDotfile(name, options))
            {
                continue;
            }

            var isSymlink = IsSymlink(file);
            if (isSymlink && !options.FollowSymlinks)
            {
                Console.Error.WriteLine($"[warn] Skipping symlink: {file}");
                continue;
            }

            var sourcePath = ResolvePhysicalPath(file);
            if (sourcePath is null ||
                options.FollowSymlinks &&
                !TryResolveSafeTarget(file, physicalSourceRoot, options, out sourcePath))
            {
                Console.Error.WriteLine($"[warn] Skipping unsafe symlink target: {file}");
                continue;
            }

            results.Add(new DirectoryCopyItem(
                sourcePath,
                Path.Combine(relativeDir, name),
                physicalSourceRoot));
        }

        foreach (var dir in Directory.GetDirectories(currentDir).OrderBy(path => path, PathComparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(dir);
            if (ShouldSkipDotfile(name, options))
            {
                continue;
            }

            var isSymlink = IsSymlink(dir);
            if (isSymlink && !options.FollowSymlinks)
            {
                Console.Error.WriteLine($"[warn] Skipping symlink directory: {dir}");
                continue;
            }

            var realDirectory = Path.GetFullPath(dir);
            if (options.FollowSymlinks &&
                !TryResolveSafeTarget(dir, physicalSourceRoot, options, out realDirectory))
            {
                Console.Error.WriteLine($"[warn] Skipping unsafe symlink directory target: {dir}");
                continue;
            }

            if (ancestors.Contains(realDirectory))
            {
                Console.Error.WriteLine($"[warn] Skipping recursive symlink directory: {dir}");
                continue;
            }

            var childAncestors = new HashSet<string>(ancestors, PathComparer)
            {
                realDirectory
            };
            EnumerateDirectory(
                dir,
                Path.Combine(relativeDir, name),
                physicalSourceRoot,
                options,
                childAncestors,
                results,
                cancellationToken);
        }
    }

    private static bool IsSymlink(string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            return (attr & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return GetImmediateLinkTarget(path) is not null;
        }
    }

    private static bool TryResolveSafeTarget(
        string path,
        string physicalSourceRoot,
        DirectoryCopyOptions options,
        out string resolvedTarget)
    {
        resolvedTarget = string.Empty;
        try
        {
            var resolved = ResolvePhysicalPath(path);
            if (resolved is null || !IsSameOrSubPathOf(physicalSourceRoot, resolved))
            {
                return false;
            }

            var relativeTarget = Path.GetRelativePath(physicalSourceRoot, resolved);
            if (relativeTarget != "." && ShouldSkipRelativePath(relativeTarget, options))
            {
                return false;
            }

            resolvedTarget = resolved;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ShouldSkipRelativePath(string relativePath, DirectoryCopyOptions options)
    {
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (ShouldSkipDotfile(segment, options))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOrSubPathOf(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return relative == "." ||
               (!Path.IsPathRooted(relative) &&
                !relative.Equals("..", StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static string? ResolvePhysicalPath(string path)
        => ResolvePhysicalPath(path, new HashSet<string>(PathComparer), remainingHops: 64);

    private static string? ResolvePhysicalPath(string path, HashSet<string> visitedLinks, int remainingHops)
    {
        try
        {
            if (remainingHops <= 0)
            {
                return null;
            }

            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var segments = fullPath[root.Length..].Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                var target = GetImmediateLinkTarget(current);
                if (target is null)
                {
                    continue;
                }

                var fullLink = Path.GetFullPath(current);
                var remainingPath = index + 1 < segments.Length
                    ? Path.Combine(segments[(index + 1)..])
                    : string.Empty;
                var resolutionState = fullLink + "\0" + remainingPath;
                if (!visitedLinks.Add(resolutionState))
                {
                    return null;
                }

                var targetPath = Path.IsPathRooted(target)
                    ? target
                    : Path.Combine(Path.GetDirectoryName(fullLink)!, target);
                if (remainingPath.Length > 0)
                {
                    targetPath = Path.Combine(targetPath, remainingPath);
                }

                return ResolvePhysicalPath(targetPath, visitedLinks, remainingHops - 1);
            }

            return Path.GetFullPath(current);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetImmediateLinkTarget(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            return info.LinkTarget;
        }
        catch
        {
            return null;
        }
    }

    private static void PruneDestination(string sourceDir, string destinationDir)
    {
        foreach (var destinationFile in Directory.GetFiles(destinationDir))
        {
            var sourceFile = Path.Combine(sourceDir, Path.GetFileName(destinationFile));
            if (!File.Exists(sourceFile))
            {
                File.Delete(destinationFile);
            }
        }

        foreach (var destinationSubdir in Directory.GetDirectories(destinationDir))
        {
            var sourceSubdir = Path.Combine(sourceDir, Path.GetFileName(destinationSubdir));
            if (!Directory.Exists(sourceSubdir))
            {
                Directory.Delete(destinationSubdir, recursive: true);
            }
        }
    }

    private static void SyncFile(string sourceFile, string destinationDir, string hashMode, string? outputRoot = null, IOutputPathPolicy? pathPolicy = null)
    {
        var name = Path.GetFileName(sourceFile);
        var destinationFile = Path.Combine(destinationDir, name);

        SyncFileToPath(sourceFile, destinationFile, hashMode, outputRoot, pathPolicy);
    }

    private static void SyncFileToPath(string sourceFile, string destinationFile, string hashMode, string? outputRoot = null, IOutputPathPolicy? pathPolicy = null)
    {

        if (outputRoot is not null)
        {
            FileWriter.GetSafeFullPath(outputRoot, Path.GetRelativePath(outputRoot, destinationFile), pathPolicy);
        }

        var sourceInfo = new FileInfo(sourceFile);
        var destinationInfo = new FileInfo(destinationFile);
        if (destinationInfo.Exists
            && destinationInfo.Length == sourceInfo.Length
            && destinationInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc
            && (!string.Equals(hashMode, "sha256", StringComparison.OrdinalIgnoreCase) || FilesHaveSameHash(sourceFile, destinationFile)))
        {
            return;
        }

        File.Copy(sourceFile, destinationFile, overwrite: true);
        File.SetLastWriteTimeUtc(destinationFile, sourceInfo.LastWriteTimeUtc);
    }

    private static StringComparer PathComparer
        => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static bool FilesHaveSameHash(string left, string right)
    {
        using var leftStream = File.OpenRead(left);
        using var rightStream = File.OpenRead(right);
        Span<byte> leftHash = stackalloc byte[32];
        Span<byte> rightHash = stackalloc byte[32];
        SHA256.HashData(leftStream, leftHash);
        SHA256.HashData(rightStream, rightHash);
        return leftHash.SequenceEqual(rightHash);
    }
}
