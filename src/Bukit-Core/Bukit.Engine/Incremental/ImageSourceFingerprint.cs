using System.Security.Cryptography;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine.Incremental;

/// <summary>
/// Fingerprints the images that the responsive-image plugin reads. The generated
/// variant set depends on these source dimensions, so incremental page reuse must
/// observe source changes even when the page body and template did not change.
/// </summary>
internal static class ImageSourceFingerprint
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    internal static string? Compute(
        AppConfig config,
        string rootDir,
        string mediaCacheDir,
        string? assetsDir,
        string? parentAssetsDir,
        string? staticDir,
        string? parentStaticDir,
        ILogger logger)
    {
        if (config.Theme.Images is not { Enabled: true })
        {
            return null;
        }

        var media = ContentProviderFactory.BuildEffectiveMediaConfig(
            config.Content.Media,
            rootDir,
            mediaCacheDir);
        var roots = new (string Name, string? Path)[]
        {
            ("media", media.DownloadDir),
            ("assets", assetsDir),
            ("parent-assets", parentAssetsDir),
            ("static", staticDir),
            ("parent-static", parentStaticDir)
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<string>();
        foreach (var (name, path) in roots)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (!seen.Add(fullPath))
            {
                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                entries.Add($"{name}:missing");
                continue;
            }

            entries.AddRange(SafeFileEnumerator.EnumerateFiles(fullPath)
                .Where(candidate => Extensions.Contains(Path.GetExtension(candidate)))
                .Select(candidate => new
                {
                    FullPath = candidate,
                    Relative = Path.GetRelativePath(fullPath, candidate).Replace('\\', '/')
                })
                .OrderBy(entry => entry.Relative, StringComparer.Ordinal)
                .Select(entry =>
                {
                    using var stream = new FileStream(
                        entry.FullPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 64 * 1024,
                        FileOptions.SequentialScan);
                    return $"{name}:{entry.Relative}:{stream.Length}:{Convert.ToHexStringLower(SHA256.HashData(stream))}";
                }));
        }

        return HashUtil.Sha256Hex(string.Join('\n', entries));
    }
}
