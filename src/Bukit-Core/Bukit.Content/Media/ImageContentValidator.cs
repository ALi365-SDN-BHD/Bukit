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
///
/// Validation runs in two bounded phases:
/// 1. <see cref="Image.IdentifyAsync(System.IO.Stream, System.Threading.CancellationToken)"/>
///    checks the detected format and applies the decoded-pixel and frame budgets to the
///    header metadata without decoding pixels.
/// 2. The stream is rewound and the image is fully loaded (with a frame cap) to prove
///    the payload actually decodes; format, frame count and total pixel budget are
///    re-checked against the decoded image before it is disposed.
/// </summary>
internal sealed class ImageContentValidator
{
    internal const long MaxTotalDecodedPixels = 100_000_000;
    internal const int MaxFrameCount = 256;

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

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 8192,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            // Phase 1: header identification with bounded budgets.
            var info = await Image.IdentifyAsync(stream, cancellationToken);
            if (info is null)
            {
                return false;
            }

            if (!ReferenceEquals(info.Metadata.DecodedImageFormat, expectedFormat))
            {
                return false;
            }

            var identifiedFrames = Math.Max(info.FrameMetadataCollection.Count, 1);
            if (identifiedFrames > MaxFrameCount)
            {
                return false;
            }

            var size = info.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                return false;
            }

            checked
            {
                var estimatedPixels = (long)size.Width * size.Height * identifiedFrames;
                if (estimatedPixels > MaxTotalDecodedPixels)
                {
                    return false;
                }
            }

            // Phase 2: prove the payload is fully decodable by the approved decoder.
            stream.Position = 0;
            using var image = await Image.LoadAsync(
                new DecoderOptions { MaxFrames = MaxFrameCount + 1 },
                stream,
                cancellationToken);

            if (!ReferenceEquals(image.Metadata.DecodedImageFormat, expectedFormat))
            {
                return false;
            }

            var decodedFrames = Math.Max(image.Frames.Count, 1);
            if (decodedFrames > MaxFrameCount)
            {
                return false;
            }

            checked
            {
                var decodedPixels = (long)image.Width * image.Height * decodedFrames;
                if (decodedPixels > MaxTotalDecodedPixels)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (
            ex is UnknownImageFormatException
            or InvalidImageContentException
            or ImageFormatException
            or IOException)
        {
            return false;
        }
    }
}
