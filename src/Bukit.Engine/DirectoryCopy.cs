using System.Security.Cryptography;
using Bukit.Engine.Output;

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

public static class DirectoryCopy
{
    private static readonly HashSet<string> DefaultDotfileDenyList = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".git", ".github", ".svn", ".hg", ".DS_Store", "Thumbs.db",
        ".npmrc", ".yarnrc"
    };

    private static readonly string[] DefaultDotfileDenyExtensions = { ".pem", ".key", ".pfx", ".p12" };

    private static readonly HashSet<string> DefaultDotfileAllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        ".well-known"
    };

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

            var dest = Path.Combine(destinationDir, name);
            Sync(dir, dest, options, outputRoot, pathPolicy);
        }

        if (options.Prune)
        {
            PruneDestination(sourceDir, destinationDir);
        }
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

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
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

        if (DefaultDotfileAllowList.Contains(name))
        {
            return false;
        }

        if (options?.AlwaysDenySensitiveDotfiles != false)
        {
            if (options?.DotfileDenyList?.Contains(name) == true)
            {
                return true;
            }

            if (DefaultDotfileDenyList.Contains(name))
            {
                return true;
            }

            if (name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var ext in DefaultDotfileDenyExtensions)
            {
                if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
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

    private static bool IsSymlink(string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            return (attr & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
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
