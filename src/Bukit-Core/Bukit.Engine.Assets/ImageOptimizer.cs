using System.Buffers.Binary;
using System.Reflection;
using System.Threading;
using Bukit.Config;
using Bukit.Content.Media;
using Bukit.Shared;
using SkiaSharp;

namespace Bukit.Engine;

internal static class ImageOptimizer
{
    private static readonly string s_webpEncoderIdentity = "skiasharp-webp-orientation-v2-" + (
        typeof(SKCodec).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
        typeof(SKCodec).Assembly.GetName().Version?.ToString() ??
        "unknown");

    private const int PngZLibLevel = 6;

    internal static async Task OptimizeIfEnabled(string assetsDir, ImageOptimizationConfig? config, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (config is not { Enabled: true })
        {
            return;
        }

        // Only WebP is produced here. AVIF is skipped without running a converter: no approved AVIF
        // decoder can validate its output (the image-processing plugin logs image_avif.skip).
        var formats = config.Formats ?? ["webp"];
        if (!formats.Contains("webp", StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var exts = new[] { ".jpg", ".jpeg", ".png" };
        var imageFiles = SafeFileEnumerator.EnumerateFiles(assetsDir, "*.*")
            .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();
        var quality = config.Quality > 0 ? config.Quality : 80;

        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var outputFile = Path.ChangeExtension(imageFile, ".webp");
                if (!File.Exists(outputFile))
                {
                    await ConvertToWebp(imageFile, outputFile, quality, logger, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Warn($"event=image_optimize.error file={Path.GetFileName(imageFile)} format=webp reason={ex.Message}");
            }
        }
    }

    public static string BuildSrcset(string baseName, IReadOnlyList<int> sizes, string format)
    {
        var parts = new List<string>();
        var ext = $".{format.TrimStart('.')}";
        foreach (var size in sizes)
        {
            var sizedFile = $"{baseName}-{size}w{ext}";
            parts.Add($"{sizedFile} {size}w");
        }

        return string.Join(", ", parts);
    }

    internal static string WebpEncoderIdentity => s_webpEncoderIdentity;

    internal static Task EncodeWebpAsync(
        string inputFile,
        string outputFile,
        int? width,
        int quality,
        CancellationToken cancellationToken)
        => EncodeAsync(inputFile, outputFile, width, quality, SKEncodedImageFormat.Webp, cancellationToken);

    internal static async Task EncodeAsync(
        string inputFile,
        string outputFile,
        int? width,
        int quality,
        SKEncodedImageFormat format,
        CancellationToken cancellationToken,
        bool allowStaticWebpSource = false)
    {
        if (width is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(inputFile);
            if (ImageContentValidator.IsAnimatedPng(stream, cancellationToken))
            {
                throw new NotSupportedException("The pinned PNG decoder cannot preserve APNG animation.");
            }
            using var codec = SKCodec.Create(stream);
            cancellationToken.ThrowIfCancellationRequested();
            var staticWebp = allowStaticWebpSource &&
                codec?.EncodedFormat == SKEncodedImageFormat.Webp && codec.FrameCount <= 1;
            if (codec is null || (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png) && !staticWebp))
            {
                throw new InvalidDataException(allowStaticWebpSource
                    ? "Expected a fully decodable JPEG, PNG or static WebP source."
                    : "Expected a fully decodable JPEG or PNG source.");
            }

            var origin = GetEncodedOrigin(inputFile, codec, cancellationToken);
            var frames = Math.Max(codec.FrameCount, 1);
            using var srgb = SKColorSpace.CreateSrgb();
            // Opaque sources (RGB PNG, JPEG) stay opaque so the PNG encoder writes colour type 2 rather
            // than RGBA; orientation and resizing reuse this info and keep the alpha type.
            var alphaType = codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, alphaType, srgb);
            if (frames > ImageContentValidator.MaxFrameCount || info.Width <= 0 || info.Height <= 0 ||
                checked((long)info.Width * info.Height * frames) > ImageContentValidator.MaxTotalDecodedPixels)
            {
                throw new InvalidDataException("Source exceeds the decoded image budget.");
            }

            using var image = new SKBitmap(info);
            DecodeFrames(codec, image, frames, cancellationToken);
            using var opaqueView = CreateOpaqueView(image);
            var decoded = opaqueView ?? image;
            using var oriented = ApplyOrientation(decoded, origin);
            var displayed = oriented ?? decoded;
            using var resized = width is { } targetWidth && targetWidth < displayed.Width
                ? displayed.Resize(displayed.Info.WithSize(targetWidth, Math.Max(1, (int)Math.Round((double)displayed.Height * targetWidth / displayed.Width))),
                    new SKSamplingOptions(SKCubicResampler.Mitchell))
                    ?? throw new InvalidDataException("Image resize failed.")
                : null;
            cancellationToken.ThrowIfCancellationRequested();
            using var encoded = Encode(resized ?? displayed, format, quality)
                ?? throw new InvalidDataException("Image encoding failed.");
            cancellationToken.ThrowIfCancellationRequested();
            await using (var output = File.Create(outputFile))
            {
                encoded.SaveTo(output);
            }
            cancellationToken.ThrowIfCancellationRequested();

            var mime = format switch
            {
                SKEncodedImageFormat.Jpeg => "image/jpeg",
                SKEncodedImageFormat.Png => "image/png",
                SKEncodedImageFormat.Webp => "image/webp",
                _ => throw new NotSupportedException("Unsupported output format.")
            };
            if (!await new ImageContentValidator().ValidateAsync(outputFile, mime, cancellationToken))
            {
                throw new InvalidDataException("SkiaSharp produced an invalid image output.");
            }
        }
        catch
        {
            TryDelete(outputFile);
            throw;
        }
    }

    private static void DecodeFrames(SKCodec codec, SKBitmap image, int frames, CancellationToken cancellationToken)
    {
        // Decode frame zero last so the output retains the original first frame.
        for (var frame = frames - 1; frame >= 0; frame--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = codec.GetPixels(image.Info, image.GetPixels(), new SKCodecOptions(frame));
            cancellationToken.ThrowIfCancellationRequested();
            if (result != SKCodecResult.Success)
            {
                throw new InvalidDataException("Source pixels could not be fully decoded.");
            }
        }
    }

    // Most uploaded PNGs are RGBA files whose pixels are all opaque. Decoding already touched every pixel, so
    // this scan is cheap. With every alpha at 255 the premultiplied bytes equal the unpremultiplied ones, so the
    // view reinterprets the same buffer as opaque without changing any colour value.
    private static SKBitmap? CreateOpaqueView(SKBitmap image)
    {
        if (image.AlphaType == SKAlphaType.Opaque)
        {
            return null;
        }

        using (var pixmap = image.PeekPixels() ?? throw new InvalidDataException("Image pixels are unavailable."))
        {
            if (!pixmap.ComputeIsOpaque())
            {
                return null;
            }
        }

        var view = new SKBitmap();
        if (!view.InstallPixels(image.Info.WithAlphaType(SKAlphaType.Opaque), image.GetPixels(), image.RowBytes))
        {
            view.Dispose();
            throw new InvalidDataException("Opaque image view could not be created.");
        }

        return view;
    }

    private static SKData? Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        if (format != SKEncodedImageFormat.Png)
        {
            return bitmap.Encode(format, Math.Clamp(quality, 1, 100));
        }

        // PNG ignores quality. Level 9 saved ~5% over 6 on a 1200x630 fixture but took ~11x longer.
        using var pixmap = bitmap.PeekPixels() ?? throw new InvalidDataException("Image pixels are unavailable.");
        return pixmap.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, PngZLibLevel));
    }

    internal static SKEncodedOrigin GetEncodedOrigin(string path, SKCodec codec, CancellationToken cancellationToken = default)
    {
        if (codec.EncodedFormat != SKEncodedImageFormat.Png) return codec.EncodedOrigin;
        using var stream = File.OpenRead(path);
        return ReadPngOrigin(stream, cancellationToken);
    }

    private static SKEncodedOrigin ReadPngOrigin(Stream stream, CancellationToken cancellationToken)
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
            cancellationToken.ThrowIfCancellationRequested();
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
                    cancellationToken.ThrowIfCancellationRequested();
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
                origin = ReadExifOrigin(stream, length, cancellationToken);
            }
            stream.Position = start + length + 4;
        }
        throw new InvalidDataException("Missing PNG image data.");
    }

    private static SKEncodedOrigin ReadExifOrigin(Stream stream, uint length, CancellationToken cancellationToken)
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
            cancellationToken.ThrowIfCancellationRequested();
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
        var rotated = new SKBitmap(origin >= SKEncodedOrigin.LeftTop ? image.Info.WithSize(height, width) : image.Info);
        try
        {
            using var canvas = new SKCanvas(rotated);
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            return rotated;
        }
        catch
        {
            rotated.Dispose();
            throw;
        }
    }

    private static async Task ConvertToWebp(string inputFile, string outputFile, int quality, ILogger logger, CancellationToken cancellationToken)
    {
        var temporaryOutput = Path.Combine(
            Path.GetDirectoryName(outputFile)!,
            $".{Path.GetFileNameWithoutExtension(outputFile)}.bukit-{Guid.NewGuid():N}{Path.GetExtension(outputFile)}");
        try
        {
            await EncodeWebpAsync(inputFile, temporaryOutput, width: null, quality, cancellationToken);
            File.Move(temporaryOutput, outputFile, overwrite: true);
            logger.Info($"event=image_optimize.ok file={Path.GetFileName(inputFile)}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Warn($"event=image_optimize.error file={Path.GetFileName(inputFile)} reason=encode_failed type={ex.GetType().Name}");
        }
        finally
        {
            TryDelete(temporaryOutput);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

}
