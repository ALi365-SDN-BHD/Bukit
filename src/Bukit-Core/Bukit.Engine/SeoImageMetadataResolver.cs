using System.Collections.Concurrent;
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
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute))
        {
            if (absolute.Scheme is not ("http" or "https") ||
                _siteUri is null ||
                !string.Equals(_siteUri.GetLeftPart(UriPartial.Authority), absolute.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            path = absolute.AbsolutePath;
        }
        else if (!imageUrl.StartsWith('/'))
        {
            return false;
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
