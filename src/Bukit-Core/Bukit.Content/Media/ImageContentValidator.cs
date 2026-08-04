using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;

namespace Bukit.Content.Media;

/// <summary>
/// Validates that a file is both signature-consistent with its declared MIME type
/// and fully decodable by the approved ImageSharp decoders. AVIF/ICO have no
/// approved decoder in the pinned ImageSharp version and fail closed.
/// </summary>
internal sealed class ImageContentValidator
{
    private static readonly Dictionary<string, IImageFormat> s_formatByMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = JpegFormat.Instance,
        ["image/jpg"] = JpegFormat.Instance,
        ["image/png"] = PngFormat.Instance,
        ["image/gif"] = GifFormat.Instance,
        ["image/webp"] = WebpFormat.Instance,
        ["image/bmp"] = BmpFormat.Instance,
        ["image/tiff"] = TiffFormat.Instance,
    };

    internal async Task<bool> ValidateAsync(
        string path,
        string contentType,
        CancellationToken cancellationToken)
    {
        var normalized = contentType.Trim();
        if (!s_formatByMime.TryGetValue(normalized, out var expectedFormat))
        {
            // Unsupported MIME (including AVIF/ICO with no approved decoder): fail closed
            return false;
        }

        // 1. Signature must match the declared MIME type
        if (!await ImageContentSignature.MatchesFileAsync(path, normalized, cancellationToken))
        {
            return false;
        }

        // 2. The container must be fully decodable and its detected format must match
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 8192,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var info = await Image.IdentifyAsync(stream, cancellationToken);
            if (info is null)
            {
                return false;
            }

            return ReferenceEquals(info.Metadata.DecodedImageFormat, expectedFormat);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or IOException)
        {
            return false;
        }
    }
}
