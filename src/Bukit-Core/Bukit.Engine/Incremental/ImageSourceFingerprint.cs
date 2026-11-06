using Bukit.Config;
using Bukit.Engine.Plugins.BuiltIn.ImageProcessing;
using Bukit.Shared;

namespace Bukit.Engine.Incremental;

/// <summary>
/// Fingerprints what the responsive-image plugin writes into HTML. Page markup only
/// depends on each source image's path and display width (which decides the variant
/// set and srcset). Variant files are regenerated from fresh content by the plugin on
/// every build, so a content change that keeps the width does not need page re-renders.
/// Widths come from the image header, so this stays cheap for large media libraries.
/// shortcut: custom templates that depend on height, format or bytes must rebuild with
/// --clean after replacing an image; revisit if Core starts exposing those properties.
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

        // The plugin reads images from output assets/ and the media URL base. Static
        // files land at the output root, so only these two static subtrees matter.
        var mediaUrlBase = ResponsiveImageHtmlTransform.NormalizeMediaUrlBase(config.Content.Media.UrlBase).TrimStart('/');
        var roots = new List<(string Name, string? Path)>
        {
            ("media", media.DownloadDir),
            ("assets", assetsDir),
            ("parent-assets", parentAssetsDir),
            ("static-assets", SubDirectory(staticDir, "assets")),
            ("parent-static-assets", SubDirectory(parentStaticDir, "assets")),
        };
        if (!IsUnderAssets(mediaUrlBase))
        {
            roots.Add(("static-media", SubDirectory(staticDir, mediaUrlBase)));
            roots.Add(("parent-static-media", SubDirectory(parentStaticDir, mediaUrlBase)));
        }

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
                .Select(candidate => (
                    FullPath: candidate,
                    Relative: Path.GetRelativePath(fullPath, candidate).Replace('\\', '/')))
                .OrderBy(entry => entry.Relative, StringComparer.Ordinal)
                .Select(entry => $"{name}:{entry.Relative}:{ReadWidth(entry.FullPath, logger)}"));
        }

        return HashUtil.Sha256Hex(string.Join('\n', entries));
    }

    private static string ReadWidth(string path, ILogger logger)
    {
        try
        {
            return ImageVariantGenerator.TryReadImageWidth(path, out var width)
                ? width.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "invalid";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            logger.Debug($"event=image_fingerprint.unreadable file={Path.GetFileName(path)}");
            return "invalid";
        }
    }

    private static string? SubDirectory(string? root, string relative)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        var rootFull = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(rootFull, relative));
        var prefix = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.Ordinal) ? candidate : null;
    }

    private static bool IsUnderAssets(string mediaUrlBase)
        => mediaUrlBase.Equals("assets", StringComparison.OrdinalIgnoreCase) ||
           mediaUrlBase.StartsWith("assets/", StringComparison.OrdinalIgnoreCase);
}
