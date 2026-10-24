using System.Buffers.Binary;
using SkiaSharp;

namespace Bukit.WechatSyncing;

/// <summary>
/// Converts unsupported image formats (WebP, GIF, BMP) to JPG/PNG
/// and compresses images that exceed the specified size limit.
/// Uses SkiaSharp for full format conversion and compression.
/// </summary>
internal static class ImageConverter
{
    /// <summary>
    /// Maximum size for inline content images (uploadimg API): strictly below 1 MiB.
    /// </summary>
    internal const int ContentImageMaxBytes = WechatDraftContract.InlineImageMaxBytesExclusive;

    /// <summary>
    /// Maximum bytes read from a local or cached inline-image source before normalization.
    /// This is intentionally separate from the smaller uploadimg payload boundary.
    /// </summary>
    internal const int InlineImageSourceMaxBytes = 10 * 1024 * 1024;

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
        byte[] bytes, int maxBytes, Bukit.Shared.ILogger? logger = null, bool requireStrictlyBelowMaxBytes = false)
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

        if (WechatSyncHelpers.IsWechatSupportedImage(detectedType) &&
            (requireStrictlyBelowMaxBytes ? bytes.Length < maxBytes : bytes.Length <= maxBytes))
        {
            var ext = WechatSyncHelpers.ContentTypeToExtension(detectedType);
            return (bytes, detectedType, ext);
        }

        return NormalizeWithSkiaSharp(bytes, detectedType, requireStrictlyBelowMaxBytes ? maxBytes - 1 : maxBytes, logger);
    }

    /// <summary>
    /// Full image normalization using SkiaSharp.
    /// Handles format conversion (WebP/GIF/BMP -> JPEG/PNG) and compression.
    /// </summary>
    private static (byte[] Bytes, string ContentType, string Extension)? NormalizeWithSkiaSharp(
        byte[] bytes, string detectedType, int maxBytes, Bukit.Shared.ILogger? logger)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var pngOrigin = detectedType == "image/png" ? ReadPngOrigin(stream) : SKEncodedOrigin.TopLeft;
            stream.Position = 0;
            using var codec = SKCodec.Create(stream);
            if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png
                or SKEncodedImageFormat.Gif or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Bmp))
            {
                throw new InvalidDataException("Unsupported image encoding.");
            }

            var frames = Math.Max(codec.FrameCount, 1);
            if (frames > 1)
                throw new NotSupportedException("Image conversion cannot preserve animation.");
            using var srgb = SKColorSpace.CreateSrgb();
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul, srgb);
            if (info.Width <= 0 || info.Height <= 0 || checked((long)info.Width * info.Height) > 100_000_000)
            {
                throw new InvalidDataException("Image exceeds the decoded pixel/frame budget.");
            }

            using var image = new SKBitmap(info);
            if (codec.GetPixels(info, image.GetPixels()) != SKCodecResult.Success)
            {
                throw new InvalidDataException("Image pixels could not be fully decoded.");
            }

            using var oriented = ApplyOrientation(image, codec.EncodedFormat == SKEncodedImageFormat.Png ? pngOrigin : codec.EncodedOrigin);
            var displayed = oriented ?? image;
            var preferPng = detectedType is "image/png" or "image/webp" or "image/gif" or "image/bmp";

            if (WechatSyncHelpers.IsWechatSupportedImage(detectedType))
            {
                return CompressImage(displayed, detectedType == "image/png", maxBytes, logger);
            }

            return ConvertAndCompress(displayed, preferPng, maxBytes, logger);
        }
        catch (Exception ex)
        {
            logger?.Warn($"plugin wechat-sync image conversion failed: {ex.Message}");
            return null;
        }
    }

    private static SKEncodedOrigin ReadPngOrigin(Stream stream)
    {
        Span<byte> header = stackalloc byte[8];
        Span<byte> buffer = stackalloc byte[1024];
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        stream.ReadExactly(header);
        if (!header.SequenceEqual(signature))
            throw new InvalidDataException("Invalid PNG signature.");
        var origin = SKEncodedOrigin.TopLeft;
        var foundExif = false;
        while (stream.Position <= stream.Length - 12)
        {
            stream.ReadExactly(header);
            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            var start = stream.Position;
            if (length > int.MaxValue || length > stream.Length - start - 4)
                throw new InvalidDataException("Truncated PNG chunk.");
            if (header[4..].SequenceEqual("IDAT"u8)) return origin;
            if (header[4..].SequenceEqual("IEND"u8)) break;
            if (header[4..].SequenceEqual("acTL"u8))
                throw new NotSupportedException("The pinned PNG decoder cannot preserve APNG animation.");
            if (header[4..].SequenceEqual("eXIf"u8))
            {
                if (foundExif) throw new InvalidDataException("Duplicate PNG eXIf chunk.");
                foundExif = true;
                // Skia ignores eXIf, so verify its full CRC before trusting the orientation.
                uint crc = uint.MaxValue;
                stream.Position = start - 4;
                for (long remaining = length + 4L; remaining > 0;)
                {
                    var block = buffer[..(int)Math.Min(buffer.Length, remaining)];
                    stream.ReadExactly(block);
                    foreach (var value in block)
                    {
                        crc ^= value;
                        for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
                    }
                    remaining -= block.Length;
                }
                stream.ReadExactly(header[..4]);
                if (BinaryPrimitives.ReadUInt32BigEndian(header) != ~crc)
                    throw new InvalidDataException("Invalid PNG eXIf CRC.");
                stream.Position = start;
                origin = ReadExifOrigin(stream, length);
            }
            stream.Position = start + length + 4;
        }
        throw new InvalidDataException("Missing PNG image data.");
    }

    private static SKEncodedOrigin ReadExifOrigin(Stream stream, uint length)
    {
        var start = stream.Position;
        Span<byte> entry = stackalloc byte[12];
        if (length < 8) throw new InvalidDataException("Truncated PNG eXIf header.");
        stream.ReadExactly(entry[..8]);
        var littleEndian = entry[..4].SequenceEqual("II\x2a\0"u8);
        if (!littleEndian && !entry[..4].SequenceEqual("MM\0\x2a"u8))
            throw new InvalidDataException("Invalid PNG eXIf TIFF header.");
        var offset = Read32(entry[4..]);
        if (offset < 8 || offset + 2L > length) throw new InvalidDataException("Invalid PNG eXIf IFD offset.");
        stream.Position = start + offset;
        stream.ReadExactly(entry[..2]);
        var count = Read16(entry);
        if (2L + count * 12L + 4 > length - offset) throw new InvalidDataException("Truncated PNG eXIf IFD.");
        var origin = SKEncodedOrigin.TopLeft;
        var found = false;
        for (var i = 0; i < count; i++)
        {
            stream.ReadExactly(entry);
            if (Read16(entry) != 0x112) continue;
            var value = Read16(entry[8..]);
            if (found || Read16(entry[2..]) != 3 || Read32(entry[4..]) != 1 || value is < 1 or > 8)
                throw new InvalidDataException("Invalid PNG eXIf orientation.");
            found = true;
            origin = (SKEncodedOrigin)value;
        }
        return origin;

        ushort Read16(ReadOnlySpan<byte> bytes) => littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes);
        uint Read32(ReadOnlySpan<byte> bytes) => littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static SKBitmap? ApplyOrientation(SKBitmap image, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return null;
        var width = image.Width;
        var height = image.Height;
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => throw new InvalidDataException("Invalid image orientation.")
        };
        var info = origin >= SKEncodedOrigin.LeftTop ? image.Info.WithSize(height, width) : image.Info;
        using var rendered = new SKBitmap(info.WithAlphaType(SKAlphaType.Premul));
        using (var canvas = new SKCanvas(rendered))
        {
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        }
        // Canvas needs premultiplied pixels; JPEG must receive unassociated RGB again.
        var rotated = new SKBitmap(info);
        using var pixels = rendered.PeekPixels();
        if (pixels.ReadPixels(info, rotated.GetPixels(), rotated.RowBytes)) return rotated;
        rotated.Dispose();
        throw new InvalidDataException("Image orientation conversion failed.");
    }

    /// <summary>
    /// Converts image to JPEG or PNG and optionally compresses to fit within maxBytes.
    /// </summary>
    private static (byte[] Bytes, string ContentType, string Extension)? ConvertAndCompress(
        SKBitmap image, bool preferPng, int maxBytes, Bukit.Shared.ILogger? logger)
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
        SKBitmap image, bool isPng, int maxBytes, Bukit.Shared.ILogger? logger)
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
        SKBitmap image, int maxBytes, Bukit.Shared.ILogger? logger)
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

    private static byte[] SaveAsJpeg(SKBitmap image, int quality = 85)
    {
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality)
            ?? throw new InvalidDataException("JPEG encoding failed.");
        return data.ToArray();
    }

    private static byte[] SaveAsPng(SKBitmap image, int compressionLevel = 6)
    {
        using var pixels = image.PeekPixels();
        using var data = pixels.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, compressionLevel))
            ?? throw new InvalidDataException("PNG encoding failed.");
        return data.ToArray();
    }
}
