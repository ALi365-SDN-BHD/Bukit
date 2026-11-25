using System.Collections.Concurrent;
using Bukit.Engine.Incremental;
using Bukit.Engine.Plugins.BuiltIn.ImageProcessing;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// Adds og:image width, height and type for a site-hosted social image. The SEO model is built
/// before assets are copied to the output, so the image is read from its source: the media cache,
/// the theme (and parent theme) assets, or the site (and parent) static directory.
/// </summary>
internal sealed class SeoImageMetadataResolver
{
    private readonly Uri? _siteUri;
    private readonly IReadOnlyList<string> _basePaths;
    private readonly string _mediaUrlBase;
    private readonly string? _mediaDir;
    private readonly IReadOnlyList<string> _assetDirs;
    private readonly IReadOnlyList<string> _staticDirs;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, ImageMetadata?> _cache = new(StringComparer.Ordinal);

    internal sealed record ImageMetadata(int Width, int Height, string? Type);

    internal SeoImageMetadataResolver(
        string? siteUrl,
        IEnumerable<string?> basePaths,
        string? mediaUrlBase,
        string? mediaDir,
        IEnumerable<string?> assetDirs,
        IEnumerable<string?> staticDirs,
        ILogger logger)
    {
        _siteUri = Uri.TryCreate(siteUrl, UriKind.Absolute, out var siteUri) ? siteUri : null;
        // Longest base first, so a language base wins over the root base it extends.
        _basePaths = [.. basePaths
            .Select(path => BuildPathUtils.NormalizeBaseUrl(path ?? "/").TrimEnd('/'))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(path => path.Length)];
        _mediaUrlBase = ResponsiveImageHtmlTransform.NormalizeMediaUrlBase(mediaUrlBase).TrimStart('/');
        _mediaDir = string.IsNullOrWhiteSpace(mediaDir) ? null : mediaDir;
        _assetDirs = [.. assetDirs.OfType<string>().Where(dir => dir.Length > 0)];
        _staticDirs = [.. staticDirs.OfType<string>().Where(dir => dir.Length > 0)];
        _logger = logger;
    }

    internal SeoModel Apply(SeoModel model)
    {
        if (model?.Og.Image is not { Length: > 0 } image || model.Og.ImageWidth is not null)
        {
            return model!;
        }

        return _cache.GetOrAdd(image, Resolve) is { } metadata
            ? model with { Og = model.Og with { ImageWidth = metadata.Width, ImageHeight = metadata.Height, ImageType = metadata.Type } }
            : model;
    }

    /// <summary>
    /// Hash of every given share image's resolved metadata, or null when none is site-hosted. Pages
    /// skipped by an incremental build keep their head, so a share image that changes size or type
    /// must change the render-dependency hash.
    /// </summary>
    internal string? Fingerprint(IEnumerable<string?> imageUrls)
    {
        var entries = imageUrls
            .OfType<string>()
            .Where(url => url.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(url => (Url: url, Metadata: _cache.GetOrAdd(url, Resolve)))
            .Where(entry => entry.Metadata is not null)
            .Select(entry => string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{entry.Url}={entry.Metadata!.Width}x{entry.Metadata.Height};{entry.Metadata.Type}"))
            .Order(StringComparer.Ordinal)
            .ToList();
        return entries.Count == 0 ? null : HashUtil.Sha256Hex(string.Join('\n', entries));
    }

    private ImageMetadata? Resolve(string imageUrl)
    {
        if (!TryGetSitePath(imageUrl, out var sitePath))
        {
            return null;
        }

        foreach (var candidate in Candidates(sitePath))
        {
            if (File.Exists(candidate))
            {
                return Read(candidate);
            }
        }

        return null;
    }

    // The image's path below the site root, or false for external, data and unparsable URLs.
    private bool TryGetSitePath(string imageUrl, out string sitePath)
    {
        sitePath = string.Empty;
        var path = imageUrl;
        // A root-relative path is a site path; on Unix Uri.TryCreate would read it as a file:// URI.
        if (!imageUrl.StartsWith('/') || imageUrl.StartsWith("//", StringComparison.Ordinal))
        {
            var candidate = imageUrl.StartsWith("//", StringComparison.Ordinal) ? "https:" + imageUrl : imageUrl;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var absolute) ||
                absolute.Scheme is not ("http" or "https") ||
                _siteUri is null ||
                !string.Equals(_siteUri.Host, absolute.Host, StringComparison.OrdinalIgnoreCase) ||
                (!imageUrl.StartsWith("//", StringComparison.Ordinal) && _siteUri.Port != absolute.Port))
            {
                return false;
            }

            path = absolute.AbsolutePath;
        }

        var basePath = _basePaths.FirstOrDefault(prefix => prefix.Length == 0 || path.StartsWith(prefix + '/', StringComparison.Ordinal));
        if (basePath is null)
        {
            return false;
        }

        try
        {
            sitePath = Uri.UnescapeDataString(path[(basePath.Length + 1)..].Split('?', '#')[0]);
        }
        catch (UriFormatException)
        {
            return false;
        }

        return sitePath.Length > 0 && !sitePath.Contains('\\');
    }

    private IEnumerable<string> Candidates(string sitePath)
    {
        if (_mediaDir is not null && sitePath.StartsWith(_mediaUrlBase + '/', StringComparison.Ordinal) &&
            Within(_mediaDir, sitePath[(_mediaUrlBase.Length + 1)..]) is { } media)
        {
            yield return media;
        }

        if (sitePath.StartsWith("assets/", StringComparison.Ordinal))
        {
            foreach (var dir in _assetDirs)
            {
                if (Within(dir, sitePath["assets/".Length..]) is { } asset)
                {
                    yield return asset;
                }
            }
        }

        foreach (var dir in _staticDirs)
        {
            if (Within(dir, sitePath) is { } staticFile)
            {
                yield return staticFile;
            }
        }
    }

    // The file below root, or null when the relative path escapes it.
    private static string? Within(string root, string relative)
    {
        // A decoded URL may carry characters no file name can (such as NUL from %00).
        if (relative.AsSpan().IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return null;
        }

        var rootFull = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.Ordinal) ? candidate : null;
    }

    private ImageMetadata? Read(string path)
    {
        try
        {
            return ImageVariantGenerator.TryReadImageSize(path, out var width, out var height)
                ? new ImageMetadata(width, height, MimeType(path))
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            _logger.Debug($"event=seo_image.unreadable file={Path.GetFileName(path)}");
            return null;
        }
    }

    private static string? MimeType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => null
        };
}
