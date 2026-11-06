using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;
using System.Security.Cryptography;
using System.Text;

namespace Bukit.Cli.Commands;

internal static class WorktreeMediaOverlay
{
    internal static string Fingerprint(string path)
    {
        if (!Directory.Exists(path)) return "missing";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Visit(path, "");
        return Convert.ToHexString(hash.GetHashAndReset());

        void Visit(string directory, string relative)
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"Cannot safely stage media symbolic link: {entry.FullName}");
                var name = Path.Combine(relative, entry.Name);
                var label = Encoding.UTF8.GetBytes((entry is DirectoryInfo ? "d:" : "f:") + name + "\0");
                hash.AppendData(label);
                if (entry is DirectoryInfo) Visit(entry.FullName, name);
                else
                {
                    using var stream = File.OpenRead(entry.FullName);
                    hash.AppendData(SHA256.HashData(stream));
                }
            }
        }
    }

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

}
