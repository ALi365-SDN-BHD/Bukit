using System.Buffers.Binary;
using SkiaSharp;
using TiffLibrary;
using TiffLibrary.PixelFormats;

namespace Bukit.Content.Media;

/// <summary>
/// Checks signatures and decoded-pixel/frame budgets before fully decoding every
/// frame with SkiaSharp, or every TIFF page with TiffLibrary. AVIF/ICO fail closed.
/// </summary>
internal sealed class ImageContentValidator
{
    internal const long MaxTotalDecodedPixels = 100_000_000;
    internal const int MaxFrameCount = 256;

    private static readonly Dictionary<string, SKEncodedImageFormat> s_formatByMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = SKEncodedImageFormat.Jpeg,
        ["image/jpg"] = SKEncodedImageFormat.Jpeg,
        ["image/png"] = SKEncodedImageFormat.Png,
        ["image/gif"] = SKEncodedImageFormat.Gif,
        ["image/webp"] = SKEncodedImageFormat.Webp,
        ["image/bmp"] = SKEncodedImageFormat.Bmp,
    };

    internal async Task<bool> ValidateAsync(
        string path,
        string contentType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = contentType.Trim();
        var isTiff = normalized.Equals("image/tiff", StringComparison.OrdinalIgnoreCase);
        if (!s_formatByMime.TryGetValue(normalized, out var expectedFormat) && !isTiff)
        {
            return false;
        }

        if (!await ImageContentSignature.MatchesFileAsync(path, normalized, cancellationToken))
        {
            return false;
        }

        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            if (isTiff)
            {
                return await ValidateTiffAsync(stream, cancellationToken);
            }

            if (expectedFormat == SKEncodedImageFormat.Png && IsAnimatedPng(stream, cancellationToken))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var codec = SKCodec.Create(stream);
            cancellationToken.ThrowIfCancellationRequested();
            if (codec is null || codec.EncodedFormat != expectedFormat)
            {
                return false;
            }

            var frames = Math.Max(codec.FrameCount, 1);
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            if (frames > MaxFrameCount || info.Width <= 0 || info.Height <= 0 ||
                checked((long)info.Width * info.Height * frames) > MaxTotalDecodedPixels)
            {
                return false;
            }

            using var bitmap = new SKBitmap(info);
            for (var frame = 0; frame < frames; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // No prior frame: Skia reconstructs dependencies, including restore-previous.
                var result = codec.GetPixels(info, bitmap.GetPixels(), new SKCodecOptions(frame));
                cancellationToken.ThrowIfCancellationRequested();
                if (result != SKCodecResult.Success)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException
            or NotSupportedException or ArgumentException or OverflowException)
        {
            return false;
        }
    }

    internal static bool IsAnimatedPng(Stream stream, CancellationToken cancellationToken)
    {
        var position = stream.Position;
        Span<byte> header = stackalloc byte[8];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        try
        {
            stream.Position = 0;
            stream.ReadExactly(header);
            if (!header.SequenceEqual(signature))
            {
                return false;
            }

            while (stream.Position <= stream.Length - 12)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stream.ReadExactly(header);
                var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                if (length > stream.Length - stream.Position - 4)
                {
                    throw new InvalidDataException("Truncated PNG chunk.");
                }
                // shortcut: the pinned PNG decoder exposes only APNG's default image; revisit with full APNG decoding.
                if (header[4..].SequenceEqual("acTL"u8)) return true;
                if (header[4..].SequenceEqual("IDAT"u8) || header[4..].SequenceEqual("IEND"u8)) return false;
                stream.Seek(length + 4L, SeekOrigin.Current);
            }
            throw new InvalidDataException("Missing PNG image data.");
        }
        finally
        {
            stream.Position = position;
        }
    }

    private static async Task<bool> ValidateTiffAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = await TiffFileReader.OpenAsync(stream, leaveOpen: true, cancellationToken: cancellationToken);
        if (!reader.IsStandardTiff) return false;
        using var fields = await reader.CreateFieldReaderAsync(cancellationToken);
        var offset = reader.FirstImageFileDirectoryOffset;
        var visited = new HashSet<TiffStreamOffset>();
        var decoders = new List<TiffImageDecoder>();
        var countBytes = new byte[2];
        long totalPixels = 0;
        while (!offset.IsZero)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(offset) || visited.Count > MaxFrameCount)
            {
                return false;
            }

            // Bound the IFD table and every field before the library allocates from declared counts.
            if (!FitsTiffRange(offset.Offset, 2, stream.Length)) return false;
            stream.Position = offset.Offset;
            await stream.ReadExactlyAsync(countBytes, cancellationToken);
            var entryCount = reader.IsLittleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(countBytes)
                : BinaryPrimitives.ReadUInt16BigEndian(countBytes);
            if (!FitsTiffRange(offset.Offset, checked(6L + entryCount * 12L), stream.Length)) return false;
            var directory = await reader.ReadImageFileDirectoryAsync(offset, cancellationToken);
            foreach (var entry in directory)
            {
                if (entry.ValueCount < 0 || entry.ValueCount > int.MaxValue ||
                    !entry.TryDetermineValueLength(out var length) || length < 0 ||
                    (length > 4 && !FitsTiffRange(entry.ValueOffset, length, stream.Length))) return false;
            }

            foreach (var (offsetTag, countTag) in new[]
            {
                (TiffTag.StripOffsets, TiffTag.StripByteCounts),
                (TiffTag.TileOffsets, TiffTag.TileByteCounts),
                (TiffTag.JPEGInterchangeFormat, TiffTag.JPEGInterchangeFormatLength)
            })
            {
                if (!await ValidateTiffRangesAsync(fields, directory.FindEntry(offsetTag), directory.FindEntry(countTag),
                    stream.Length, cancellationToken)) return false;
            }

            var width = await ReadTiffDimensionAsync(fields, directory, TiffTag.ImageWidth, cancellationToken);
            var height = await ReadTiffDimensionAsync(fields, directory, TiffTag.ImageLength, cancellationToken);
            long decodedWidth = width, decodedHeight = height;
            if (directory.Contains(TiffTag.TileWidth) || directory.Contains(TiffTag.TileLength))
            {
                var tileWidth = await ReadTiffDimensionAsync(fields, directory, TiffTag.TileWidth, cancellationToken);
                var tileHeight = await ReadTiffDimensionAsync(fields, directory, TiffTag.TileLength, cancellationToken);
                // Edge tiles are decoded at their full padded size, even for a one-pixel image.
                decodedWidth = checked(((long)width + tileWidth - 1) / tileWidth * tileWidth);
                decodedHeight = checked(((long)height + tileHeight - 1) / tileHeight * tileHeight);
            }
            totalPixels = checked(totalPixels + decodedWidth * decodedHeight);
            if (totalPixels > MaxTotalDecodedPixels)
            {
                return false;
            }

            var decoder = await reader.CreateImageDecoderAsync(directory, cancellationToken: cancellationToken);
            if (decoder.Width <= 0 || decoder.Height <= 0 ||
                checked((long)decoder.Width * decoder.Height) != (long)width * height) return false;
            decoders.Add(decoder);
            offset = directory.NextOffset;
        }

        foreach (var decoder in decoders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pixels = new TiffRgba32[checked(decoder.Width * decoder.Height)];
            var buffer = new TiffMemoryPixelBuffer<TiffRgba32>(pixels, decoder.Width, decoder.Height, writable: true);
            await decoder.DecodeAsync(buffer, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return decoders.Count > 0;
    }

    private static bool FitsTiffRange(long offset, long count, long fileLength)
        => offset >= 0 && count >= 0 && checked(offset + count) <= fileLength;

    private static async Task<bool> ValidateTiffRangesAsync(
        TiffFieldReader fields, TiffImageFileDirectoryEntry offsets, TiffImageFileDirectoryEntry counts,
        long fileLength, CancellationToken cancellationToken)
    {
        if (offsets.Tag == 0 && counts.Tag == 0) return true;
        if (offsets.ValueCount == 0 || offsets.ValueCount != counts.ValueCount) return false;
        var offsetBuffer = new ulong[128];
        var countBuffer = new ulong[128];
        for (var index = 0; index < offsets.ValueCount;)
        {
            var length = (int)Math.Min(offsetBuffer.Length, offsets.ValueCount - index);
            await fields.ReadLong8FieldAsync(offsets, index, offsetBuffer.AsMemory(0, length), cancellationToken: cancellationToken);
            await fields.ReadLong8FieldAsync(counts, index, countBuffer.AsMemory(0, length), cancellationToken: cancellationToken);
            for (var i = 0; i < length; i++)
            {
                if (!FitsTiffRange(checked((long)offsetBuffer[i]), checked((long)countBuffer[i]), fileLength)) return false;
            }
            index = checked(index + length);
        }
        return true;
    }

    private static async Task<int> ReadTiffDimensionAsync(
        TiffFieldReader fields, TiffImageFileDirectory directory, TiffTag tag, CancellationToken cancellationToken)
    {
        var entry = directory.FindEntry(tag);
        if (entry.ValueCount != 1) throw new InvalidDataException("Invalid TIFF dimension.");
        var values = await fields.ReadLong8FieldAsync(entry, 1, cancellationToken: cancellationToken);
        var value = values.GetFirstOrDefault();
        if (value == 0 || value > int.MaxValue) throw new InvalidDataException("Invalid TIFF dimension.");
        return (int)value;
    }
}
