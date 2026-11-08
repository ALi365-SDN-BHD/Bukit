using System.Buffers.Binary;

namespace Bukit.Content.Media;

internal static class ImageContentSignature
{
    private const int MaxSignatureBytes = 4096;

    // Raster types a generic binary response may be identified as; SVG is never identified.
    private static readonly string[] DetectableContentTypes =
    [
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "image/avif", "image/bmp", "image/x-icon", "image/tiff"
    ];

    internal static async Task<bool> MatchesFileAsync(
        string path,
        string contentType,
        CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(path, cancellationToken);
        return Matches(contentType, header);
    }

    /// <summary>Returns the image content type whose signature the file starts with, or null.</summary>
    internal static async Task<string?> DetectFileAsync(string path, CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(path, cancellationToken);
        return Detect(header);
    }

    internal static string? Detect(ReadOnlySpan<byte> content)
    {
        foreach (var contentType in DetectableContentTypes)
        {
            if (Matches(contentType, content))
            {
                return contentType;
            }
        }

        return null;
    }

    private static async Task<byte[]> ReadHeaderAsync(string path, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxSignatureBytes];
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return buffer.AsSpan(0, totalRead).ToArray();
    }

    internal static bool Matches(string contentType, ReadOnlySpan<byte> content)
    {
        var normalized = contentType.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => StartsWith(content, [0xFF, 0xD8, 0xFF]),
            "image/png" => StartsWith(content, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            "image/gif" => StartsWith(content, "GIF87a"u8) || StartsWith(content, "GIF89a"u8),
            "image/webp" => IsWebP(content),
            "image/avif" => IsAvif(content),
            "image/bmp" => StartsWith(content, "BM"u8),
            "image/x-icon" or "image/vnd.microsoft.icon" or "image/ico" =>
                StartsWith(content, [0x00, 0x00, 0x01, 0x00]),
            "image/tiff" => IsTiff(content),
            _ => false
        };
    }

    private static bool IsWebP(ReadOnlySpan<byte> content)
        => content.Length >= 12 &&
           content[..4].SequenceEqual("RIFF"u8) &&
           content.Slice(8, 4).SequenceEqual("WEBP"u8);

    private static bool IsAvif(ReadOnlySpan<byte> content)
    {
        if (content.Length < 16 || !content.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return false;
        }

        var boxSize = BinaryPrimitives.ReadUInt32BigEndian(content[..4]);
        if (boxSize < 16 || boxSize > content.Length)
        {
            return false;
        }

        if (IsAvifBrand(content.Slice(8, 4)))
        {
            return true;
        }

        for (var offset = 16; offset + 4 <= boxSize; offset += 4)
        {
            if (IsAvifBrand(content.Slice(offset, 4)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAvifBrand(ReadOnlySpan<byte> brand)
        => brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8);

    private static bool IsTiff(ReadOnlySpan<byte> content)
        => StartsWith(content, [0x49, 0x49, 0x2A, 0x00]) ||
           StartsWith(content, [0x4D, 0x4D, 0x00, 0x2A]);

    private static bool StartsWith(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature)
        => content.Length >= signature.Length && content[..signature.Length].SequenceEqual(signature);
}
