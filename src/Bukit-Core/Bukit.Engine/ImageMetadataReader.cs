using System.Globalization;
using System.Text;
using System.Xml;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;

namespace Bukit.Engine;

internal static class ImageMetadataReader
{
    internal static ImageMetadata? TryReadImageMetadata(string path)
    {
        Span<byte> buffer = stackalloc byte[64];
        using var stream = File.OpenRead(path);
        var read = stream.Read(buffer);
        var bytes = buffer[..read];
        if (IsSvgCandidate(path, bytes))
        {
            return TryReadSvgMetadata(path);
        }

        if (read < 10)
        {
            return null;
        }

        if (bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            read >= 24)
        {
            return new ImageMetadata("image/png", ReadBigEndianInt32(bytes[16..20]), ReadBigEndianInt32(bytes[20..24]));
        }

        if (bytes[0] == 0x47 &&
            bytes[1] == 0x49 &&
            bytes[2] == 0x46 &&
            read >= 10)
        {
            return new ImageMetadata("image/gif", ReadLittleEndianUInt16(bytes[6..8]), ReadLittleEndianUInt16(bytes[8..10]));
        }

        if (bytes[0] == 0x52 &&
            bytes[1] == 0x49 &&
            bytes[2] == 0x46 &&
            bytes[3] == 0x46 &&
            read >= 30 &&
            bytes[8] == 0x57 &&
            bytes[9] == 0x45 &&
            bytes[10] == 0x42 &&
            bytes[11] == 0x50)
        {
            return TryReadWebpMetadata(path);
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return TryReadJpegMetadata(path);
        }

        return null;
    }

    internal static ImageMetadata? TryReadSvgMetadata(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                XmlResolver = null
            });

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!reader.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var width = ParseSvgDimension(reader.GetAttribute("width"));
                var height = ParseSvgDimension(reader.GetAttribute("height"));
                if (width <= 0 || height <= 0)
                {
                    var viewBox = ParseSvgViewBox(reader.GetAttribute("viewBox"));
                    width = width > 0 ? width : viewBox.Width;
                    height = height > 0 ? height : viewBox.Height;
                }

                return new ImageMetadata("image/svg+xml", width, height);
            }
        }
        catch (XmlException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        return null;
    }

    internal static ImageMetadata? TryReadJpegMetadata(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
        {
            return null;
        }

        while (stream.Position < stream.Length)
        {
            if (stream.ReadByte() != 0xFF)
            {
                continue;
            }

            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker == 0xFF);

            if (marker < 0 || marker is 0xD8 or 0xD9)
            {
                continue;
            }

            var length = ReadBigEndianUInt16(stream);
            if (length < 2 || stream.Position + length - 2 > stream.Length)
            {
                return null;
            }

            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
            {
                stream.ReadByte();
                var height = ReadBigEndianUInt16(stream);
                var width = ReadBigEndianUInt16(stream);
                return new ImageMetadata("image/jpeg", width, height);
            }

            stream.Seek(length - 2, SeekOrigin.Current);
        }

        return null;
    }

    internal static ImageMetadata? TryReadWebpMetadata(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 30)
        {
            return null;
        }

        var chunk = Encoding.ASCII.GetString(bytes, 12, 4);
        if (chunk == "VP8X" && bytes.Length >= 30)
        {
            var width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
            var height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
            return new ImageMetadata("image/webp", width, height);
        }

        if (chunk == "VP8 " && bytes.Length >= 30)
        {
            var width = ReadLittleEndianUInt16(bytes.AsSpan(26, 2)) & 0x3FFF;
            var height = ReadLittleEndianUInt16(bytes.AsSpan(28, 2)) & 0x3FFF;
            return new ImageMetadata("image/webp", width, height);
        }

        if (chunk == "VP8L" && bytes.Length >= 25)
        {
            var b0 = bytes[21];
            var b1 = bytes[22];
            var b2 = bytes[23];
            var b3 = bytes[24];
            var width = 1 + (((b1 & 0x3F) << 8) | b0);
            var height = 1 + (((b3 & 0x0F) << 10) | (b2 << 2) | ((b1 & 0xC0) >> 6));
            return new ImageMetadata("image/webp", width, height);
        }

        return null;
    }

    internal static int ReadBigEndianInt32(ReadOnlySpan<byte> value)
        => (value[0] << 24) | (value[1] << 16) | (value[2] << 8) | value[3];

    internal static int ReadLittleEndianUInt16(ReadOnlySpan<byte> value)
        => value[0] | (value[1] << 8);

    internal static int ReadBigEndianUInt16(Stream stream)
    {
        var high = stream.ReadByte();
        var low = stream.ReadByte();
        return high < 0 || low < 0 ? 0 : (high << 8) | low;
    }

    private static bool IsSvgCandidate(string path, ReadOnlySpan<byte> bytes)
    {
        if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sample = Encoding.UTF8.GetString(bytes).TrimStart();
        return sample.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
               sample.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && sample.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseSvgDimension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var trimmed = value.Trim();
        if (trimmed.EndsWith('%'))
        {
            return 0;
        }

        var length = 0;
        while (length < trimmed.Length)
        {
            var c = trimmed[length];
            if (!char.IsDigit(c) && c is not '.' and not '+' and not '-')
            {
                break;
            }

            length++;
        }

        if (length == 0)
        {
            return 0;
        }

        return double.TryParse(trimmed[..length], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0
            ? (int)Math.Ceiling(number)
            : 0;
    }

    private static (int Width, int Height) ParseSvgViewBox(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (0, 0);
        }

        var parts = value
            .Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0)
            .ToArray();

        if (parts.Length < 4 || parts[2] <= 0 || parts[3] <= 0)
        {
            return (0, 0);
        }

        return ((int)Math.Ceiling(parts[2]), (int)Math.Ceiling(parts[3]));
    }

    internal static void AnalyzeImage(AppConfig config, string codePrefix, string routeUrl, string? image, string outputDir, List<SeoAuditIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        if (SeoAuditReportWriter.IsAbsoluteHttpUrl(image))
        {
            if (TryMapSiteUrlToOutputPath(config.Site.Url, config.Site.BaseUrl, image, outputDir, out var localPath))
            {
                AnalyzeLocalImage(codePrefix, routeUrl, image, localPath, issues);
            }
            else
            {
                issues.Add(Warning($"{codePrefix}_external_unverified", routeUrl, $"Image is external and was not fetched during SEO audit: {image}."));
            }

            return;
        }

        issues.Add(Warning($"{codePrefix}_not_absolute", routeUrl, $"Search/social image should be an absolute URL: {image}."));
        var relative = image.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        AnalyzeLocalImage(codePrefix, routeUrl, image, Path.Combine(outputDir, relative), issues);
    }

    internal static void AnalyzeLocalImage(string codePrefix, string routeUrl, string image, string fullPath, List<SeoAuditIssue> issues)
    {
        if (!File.Exists(fullPath))
        {
            issues.Add(Warning($"{codePrefix}_missing_file", routeUrl, $"Image file was not found in build output: {image}."));
            return;
        }

        var metadata = TryReadImageMetadata(fullPath);
        if (metadata is null)
        {
            issues.Add(Warning($"{codePrefix}_mime_unknown", routeUrl, $"Image MIME/dimensions could not be detected: {image}."));
            return;
        }

        if (!metadata.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning($"{codePrefix}_mime_invalid", routeUrl, $"Image MIME is not an image type: {metadata.MimeType}."));
        }

        if (metadata.Width < 300 || metadata.Height < 157)
        {
            issues.Add(Warning($"{codePrefix}_too_small", routeUrl, $"Image dimensions are small for social/search previews: {metadata.Width}x{metadata.Height}."));
        }
    }

    internal static bool TryMapSiteUrlToOutputPath(string? siteUrl, string baseUrl, string imageUrl, string outputDir, out string fullPath)
    {
        fullPath = string.Empty;
        var normalizedBaseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl);
        var siteRoot = siteUrl?.Trim().TrimEnd('/') + normalizedBaseUrl;
        if (string.IsNullOrWhiteSpace(siteUrl) ||
            !Uri.TryCreate(siteRoot, UriKind.Absolute, out var siteUri) ||
            !Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) ||
            !string.Equals(siteUri.Scheme, imageUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(siteUri.Host, imageUri.Host, StringComparison.OrdinalIgnoreCase) ||
            siteUri.Port != imageUri.Port ||
            !imageUri.AbsolutePath.StartsWith(siteUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = Uri.UnescapeDataString(imageUri.AbsolutePath[siteUri.AbsolutePath.Length..])
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative))
        {
            return false;
        }

        fullPath = Path.Combine(outputDir, relative);
        return true;
    }

    private static SeoAuditIssue Error(string code, string? route, string message) => new("error", code, route, message);

    private static SeoAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);
}

internal sealed record ImageMetadata(string MimeType, int Width, int Height);
