using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace Bukit.WechatSyncing;

/// <summary>
/// Converts unsupported image formats (WebP, GIF, BMP) to JPG/PNG
/// and compresses images that exceed the specified size limit.
/// Uses SixLabors.ImageSharp for full format conversion and compression.
/// </summary>
internal static class ImageConverter
{
    /// <summary>
    /// Maximum size for inline content images (uploadimg API): 2 MB.
    /// </summary>
    internal const int ContentImageMaxBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Maximum size for material images (add_material API): 10 MB.
    /// </summary>
    internal const int MaterialImageMaxBytes = 10 * 1024 * 1024;

    internal static async Task<byte[]?> TryReadImageFileWithLimitAsync(
        string path,
        int maxBytes,
        string description,
        Bukit.Shared.ILogger logger,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return null;
        }

        if (info.Length == 0)
        {
            logger.Warn($"plugin wechat-sync {description} file empty path={path}");
            return null;
        }

        if (info.Length > maxBytes)
        {
            logger.Warn($"plugin wechat-sync {description} too large path={path} size={info.Length} max={maxBytes}");
            return null;
        }

        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    internal static byte[]? TryReadImageFileWithLimit(
        string path,
        int maxBytes,
        string description,
        Bukit.Shared.ILogger logger)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return null;
        }

        if (info.Length == 0)
        {
            logger.Warn($"plugin wechat-sync {description} file empty path={path}");
            return null;
        }

        if (info.Length > maxBytes)
        {
            logger.Warn($"plugin wechat-sync {description} too large path={path} size={info.Length} max={maxBytes}");
            return null;
        }

        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// Ensures image bytes are in a WeChat-compatible format (JPEG or PNG)
    /// and within the specified size limit. Converts and/or compresses as needed.
    /// Returns null if conversion fails or the format is unrecoverable (e.g. SVG without raster).
    /// </summary>
    /// <param name="bytes">Raw image bytes.</param>
    /// <param name="maxBytes">Maximum allowed size in bytes.</param>
    /// <param name="logger">Logger for warnings.</param>
    /// <returns>A tuple of (converted bytes, content type, file extension) or null if failed.</returns>
    internal static (byte[] Bytes, string ContentType, string Extension)? NormalizeForUpload(
        byte[] bytes, int maxBytes, Bukit.Shared.ILogger? logger = null)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var detectedType = WechatSyncHelpers.DetectImageContentType(bytes);

        if (string.IsNullOrWhiteSpace(detectedType))
        {
            logger?.Warn("plugin wechat-sync image format unrecognized, cannot convert");
            return null;
        }

        if (WechatSyncHelpers.IsWechatSupportedImage(detectedType) && bytes.Length <= maxBytes)
        {
            var ext = WechatSyncHelpers.ContentTypeToExtension(detectedType);
            return (bytes, detectedType, ext);
        }

        return NormalizeWithImageSharp(bytes, detectedType, maxBytes, logger);
    }

    /// <summary>
    /// Full image normalization using SixLabors.ImageSharp.
    /// Handles format conversion (WebP/GIF/BMP -> JPEG/PNG) and compression.
    /// </summary>
    private static (byte[] Bytes, string ContentType, string Extension)? NormalizeWithImageSharp(
        byte[] bytes, string detectedType, int maxBytes, Bukit.Shared.ILogger? logger)
    {
        try
        {
            using var image = Image.Load(bytes);

            var preferPng = detectedType is "image/png" or "image/webp" or "image/gif" or "image/bmp";

            if (WechatSyncHelpers.IsWechatSupportedImage(detectedType))
            {
                return CompressImage(image, detectedType == "image/png", maxBytes, logger);
            }

            return ConvertAndCompress(image, preferPng, maxBytes, logger);
        }
        catch (Exception ex)
        {
            logger?.Warn($"plugin wechat-sync image conversion failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts image to JPEG or PNG and optionally compresses to fit within maxBytes.
    /// </summary>
    private static (byte[] Bytes, string ContentType, string Extension)? ConvertAndCompress(
        Image image, bool preferPng, int maxBytes, Bukit.Shared.ILogger? logger)
    {
        if (preferPng)
        {
            var pngBytes = SaveAsPng(image);
            if (pngBytes.Length <= maxBytes)
            {
                return (pngBytes, "image/png", ".png");
            }

            logger?.Warn("plugin wechat-sync PNG conversion exceeds size limit, falling back to JPEG");
        }

        return CompressAsJpeg(image, maxBytes, logger);
    }

    /// <summary>
    /// Compresses an already-supported image (JPEG or PNG) to fit within maxBytes.
    /// </summary>
    private static (byte[] Bytes, string ContentType, string Extension)? CompressImage(
        Image image, bool isPng, int maxBytes, Bukit.Shared.ILogger? logger)
    {
        if (isPng)
        {
            var pngBytes = SaveAsPng(image, compressionLevel: 7);
            if (pngBytes.Length <= maxBytes)
            {
                return (pngBytes, "image/png", ".png");
            }

            logger?.Warn("plugin wechat-sync PNG compression insufficient, converting to JPEG");
        }

        return CompressAsJpeg(image, maxBytes, logger);
    }

    /// <summary>
    /// Saves image as JPEG with decreasing quality until it fits within maxBytes.
    /// </summary>
    private static (byte[] Bytes, string ContentType, string Extension)? CompressAsJpeg(
        Image image, int maxBytes, Bukit.Shared.ILogger? logger)
    {
        for (var quality = 85; quality >= 50; quality -= 10)
        {
            var jpegBytes = SaveAsJpeg(image, quality);
            if (jpegBytes.Length <= maxBytes)
            {
                if (quality < 85)
                {
                    logger?.Info($"plugin wechat-sync image compressed to JPEG quality={quality} size={jpegBytes.Length}");
                }

                return (jpegBytes, "image/jpeg", ".jpg");
            }
        }

        var lastResort = SaveAsJpeg(image, 30);
        if (lastResort.Length <= maxBytes)
        {
            logger?.Warn($"plugin wechat-sync image compressed to very low JPEG quality=30 size={lastResort.Length}");
            return (lastResort, "image/jpeg", ".jpg");
        }

        logger?.Warn($"plugin wechat-sync image too large even at minimum quality: {lastResort.Length} bytes (max {maxBytes})");
        return null;
    }

    private static byte[] SaveAsJpeg(Image image, int quality = 85)
    {
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms, new JpegEncoder { Quality = quality });
        return ms.ToArray();
    }

    private static byte[] SaveAsPng(Image image, int compressionLevel = 6)
    {
        using var ms = new MemoryStream();
        image.SaveAsPng(ms, new PngEncoder
        {
            CompressionLevel = (PngCompressionLevel)Math.Clamp(compressionLevel, 0, 9)
        });
        return ms.ToArray();
    }
}
