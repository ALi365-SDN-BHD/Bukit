using System.Net;
using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Shared;
using static Bukit.Engine.Plugins.BuiltIn.ImageVariantGenerator;
using static Bukit.Engine.Plugins.BuiltIn.ImageVariantOwnership;

namespace Bukit.Engine.Plugins.BuiltIn;

/// <summary>
/// Adds srcset/decoding attributes to media images and wraps generated WebP
/// variants in picture elements.
/// </summary>
internal sealed partial class ResponsiveImageHtmlTransform(
    AppConfig config,
    string? mediaDownloadDir,
    string baseUrl,
    ILogger logger) : IHtmlTransform
{
    [GeneratedRegex(
        """<!--[\s\S]*?-->|<(?:script|style|template)\b(?:[^>"']|"[^"]*"|'[^']*')*>[\s\S]*?</(?:script|style|template)\s*>|<picture\b(?:[^>"']|"[^"]*"|'[^']*')*>[\s\S]*?</picture\s*>|<img(?=[\s/>])(?:[^>"']|"[^"]*"|'[^']*')*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlElementRegex();
    [GeneratedRegex(
        """(?<name>[^\s=/>]+)(?:\s*=\s*(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<unquoted>[^\s>]+)))?""",
        RegexOptions.CultureInvariant)]
    private static partial Regex AttributeRegex();
    private readonly IReadOnlyList<int> _sizes = NormalizeSizes(config.Theme.Images?.Sizes);
    private readonly string _baseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
    private readonly string _mediaUrlBase = NormalizeMediaUrlBase(config.Content.Media.UrlBase);
    private readonly Uri? _siteUri = Uri.TryCreate(config.Site.Url, UriKind.Absolute, out var siteUri) ? siteUri : null;
    private readonly bool _webpEnabled = config.Theme.Images?.Formats?.Any(format =>
        string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase)) == true;

    private const string GeneratedSrcsetMarker = "data-bukit-generated-srcset";

    public string Name => "responsive-images";

    public string Transform(HtmlTransformContext context, string html)
    {
        if (config.Theme.Images is not { Enabled: true } ||
            string.IsNullOrWhiteSpace(mediaDownloadDir) ||
            !Directory.Exists(mediaDownloadDir))
        {
            return html;
        }

        return HtmlElementRegex().Replace(html, match =>
            match.Value.StartsWith("<img", StringComparison.OrdinalIgnoreCase)
                ? RewriteImageTag(match.Value)
                : match.Value);
    }

    internal static string RewriteWebpPictures(
        string html,
        IReadOnlyDictionary<string, string> webpSrcsets,
        string baseUrl,
        string? siteUrl)
    {
        var normalizedBaseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        var siteUri = Uri.TryCreate(siteUrl, UriKind.Absolute, out var parsedSiteUri) ? parsedSiteUri : null;
        return HtmlElementRegex().Replace(html, match =>
        {
            if (!match.Value.StartsWith("<img", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            var attributes = AttributeRegex().Matches(match.Value[4..^1]);
            var hasGeneratedSrcset = attributes.Any(attribute =>
                attribute.Groups["name"].Value.Equals(GeneratedSrcsetMarker, StringComparison.OrdinalIgnoreCase));
            if (!hasGeneratedSrcset && attributes.Any(attribute =>
                attribute.Groups["name"].Value.Equals("srcset", StringComparison.OrdinalIgnoreCase)))
            {
                return match.Value;
            }

            var fallbackTag = hasGeneratedSrcset
                ? match.Value.Replace($" {GeneratedSrcsetMarker}=\"\"", string.Empty, StringComparison.Ordinal)
                : match.Value;
            var src = GetAttributeValue(attributes, "src");
            if (src is null || !TryNormalizeLocalImageUrl(src, normalizedBaseUrl, siteUri, out var sourceUrl) ||
                !webpSrcsets.TryGetValue(sourceUrl, out var webpSrcset))
            {
                return fallbackTag;
            }

            var sizes = GetAttributeValue(attributes, "sizes");
            var source = $"<source type=\"image/webp\" srcset=\"{WebUtility.HtmlEncode(webpSrcset)}\"" +
                (sizes is null ? ">" : $" sizes=\"{WebUtility.HtmlEncode(sizes)}\">");
            return $"<picture>{source}{fallbackTag}</picture>";
        });
    }

    private static string? GetAttributeValue(MatchCollection attributes, string name)
    {
        var match = attributes.FirstOrDefault(attribute =>
            attribute.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return null;
        }

        return WebUtility.HtmlDecode(
            match.Groups["double"].Success ? match.Groups["double"].Value :
            match.Groups["single"].Success ? match.Groups["single"].Value :
            match.Groups["unquoted"].Value);
    }

    private static bool TryNormalizeLocalImageUrl(
        string sourceUrl,
        string baseUrl,
        Uri? siteUri,
        out string normalized)
    {
        normalized = string.Empty;
        var path = sourceUrl;
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            if (siteUri is null ||
                !string.Equals(siteUri.GetLeftPart(UriPartial.Authority), absolute.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            path = absolute.AbsolutePath;
        }
        else if (sourceUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var suffixAt = path.IndexOfAny(['?', '#']);
        if (suffixAt >= 0)
        {
            path = path[..suffixAt];
        }
        if (!path.StartsWith('/') ||
            Path.GetExtension(path).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png"))
        {
            return false;
        }

        normalized = path;
        if (baseUrl.Length > 0 && !normalized.StartsWith(baseUrl + "/", StringComparison.Ordinal))
        {
            return false;
        }
        return true;
    }

    private string RewriteImageTag(string tag)
    {
        var parsedAttributes = AttributeRegex().Matches(tag[4..^1]);
        var srcMatch = parsedAttributes.FirstOrDefault(attribute =>
            attribute.Groups["name"].Value.Equals("src", StringComparison.OrdinalIgnoreCase));
        if (srcMatch is null)
        {
            return tag;
        }

        var sourceUrl = WebUtility.HtmlDecode(
            srcMatch.Groups["double"].Success ? srcMatch.Groups["double"].Value :
            srcMatch.Groups["single"].Success ? srcMatch.Groups["single"].Value :
            srcMatch.Groups["unquoted"].Value);
        if (!TryResolveMediaSource(sourceUrl, out var sourceFile) ||
            !TryGetImageWidth(sourceFile, logger, out var sourceWidth))
        {
            return tag;
        }

        var attributes = string.Empty;
        var applicableSizes = _sizes.Where(size => size < sourceWidth).ToArray();
        if (applicableSizes.Length > 0 && !parsedAttributes.Any(attribute => attribute.Groups["name"].Value.Equals("srcset", StringComparison.OrdinalIgnoreCase)))
        {
            var candidates = applicableSizes
                .Select(size => $"{BuildVariantUrl(sourceUrl, size)} {size}w")
                .Append($"{sourceUrl} {sourceWidth}w");
            attributes += $" srcset=\"{WebUtility.HtmlEncode(string.Join(", ", candidates))}\"";
            if (_webpEnabled)
            {
                attributes += $" {GeneratedSrcsetMarker}=\"\"";
            }
        }
        if (!parsedAttributes.Any(attribute => attribute.Groups["name"].Value.Equals("decoding", StringComparison.OrdinalIgnoreCase)))
        {
            attributes += " decoding=\"async\"";
        }

        if (attributes.Length == 0)
        {
            return tag;
        }

        var insertAt = tag.LastIndexOf('>');
        if (insertAt > 0 && tag[insertAt - 1] == '/')
        {
            insertAt--;
        }
        return tag.Insert(insertAt, attributes);
    }

    private bool TryResolveMediaSource(string sourceUrl, out string sourceFile)
    {
        sourceFile = string.Empty;
        var path = sourceUrl;
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var absolute) &&
            absolute.Scheme is "http" or "https")
        {
            if (_siteUri is null ||
                !string.Equals(_siteUri.GetLeftPart(UriPartial.Authority), absolute.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            path = absolute.AbsolutePath;
        }

        var prefix = _baseUrl + _mediaUrlBase;
        if (!path.StartsWith(prefix + '/', StringComparison.Ordinal))
        {
            return false;
        }

        string relative;
        try
        {
            relative = Uri.UnescapeDataString(path[(prefix.Length + 1)..]);
        }
        catch (UriFormatException)
        {
            return false;
        }
        if (relative.Contains('\\'))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(
            mediaDownloadDir!,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinDirectory(mediaDownloadDir!, candidate) ||
            !File.Exists(candidate) ||
            Path.GetExtension(candidate).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png"))
        {
            return false;
        }

        sourceFile = candidate;
        return true;
    }

    internal static string NormalizeMediaUrlBase(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "/assets/uploads" : value.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }
        return normalized.TrimEnd('/');
    }

    private static string BuildVariantUrl(string sourceUrl, int width)
    {
        var extensionAt = sourceUrl.LastIndexOf('.');
        return $"{sourceUrl[..extensionAt]}-{width}w{sourceUrl[extensionAt..]}";
    }
}
