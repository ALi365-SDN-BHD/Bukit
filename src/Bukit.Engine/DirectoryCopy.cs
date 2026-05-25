using System.Security.Cryptography;

namespace Bukit.Engine;

public sealed record DirectoryCopyOptions
{
    public string HashMode { get; init; } = "size-time";
    public bool Prune { get; init; }
    public bool IgnoreDotPrefixedFiles { get; init; }
}

public static class DirectoryCopy
{
    public static void Copy(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(destinationDir, name);
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(destinationDir, name);
            Copy(dir, dest);
        }
    }

    public static void Sync(string sourceDir, string destinationDir, bool prune = false)
        => Sync(sourceDir, destinationDir, new DirectoryCopyOptions { Prune = prune });

    public static void Sync(string sourceDir, string destinationDir, DirectoryCopyOptions options)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (options.IgnoreDotPrefixedFiles && name.StartsWith('.'))
            {
                continue;
            }

            SyncFile(file, destinationDir, options.HashMode);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(destinationDir, name);
            Sync(dir, dest, options);
        }

        if (options.Prune)
        {
            PruneDestination(sourceDir, destinationDir);
        }
    }

    public static void SyncFiles(string sourceDir, string destinationDir, bool ignoreDotPrefixedFiles = false)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (ignoreDotPrefixedFiles && name.StartsWith('.'))
            {
                continue;
            }

            SyncFile(file, destinationDir, "size-time");
        }
    }

    public static void SyncFilesRecursive(string sourceDir, string destinationDir, bool ignoreDotPrefixedFiles = false)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (ignoreDotPrefixedFiles && name.StartsWith('.'))
            {
                continue;
            }

            var relativeDirectory = Path.GetRelativePath(sourceDir, Path.GetDirectoryName(file)!);
            var destinationSubdir = relativeDirectory == "."
                ? destinationDir
                : Path.Combine(destinationDir, relativeDirectory);
            Directory.CreateDirectory(destinationSubdir);
            SyncFile(file, destinationSubdir, "size-time");
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

    private static void SyncFile(string sourceFile, string destinationDir, string hashMode)
    {
        var name = Path.GetFileName(sourceFile);
        var destinationFile = Path.Combine(destinationDir, name);

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
