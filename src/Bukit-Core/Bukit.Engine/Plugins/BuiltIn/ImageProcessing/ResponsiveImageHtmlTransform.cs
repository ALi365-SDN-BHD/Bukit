using System.Net;
using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Shared;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantGenerator;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantOwnership;

namespace Bukit.Engine.Plugins.BuiltIn.ImageProcessing;

/// <summary>
/// Adds srcset, decoding, width/height and loading attributes to media images (every media image but a
/// page's first is made lazy) and wraps generated WebP variants in picture elements.
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
    private readonly string? _sizesAttribute = config.Theme.Images?.SizesAttribute;
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

        // The first media image is often the largest above-the-fold element, so it is not made lazy.
        var seenMediaImage = false;
        return HtmlElementRegex().Replace(html, match =>
        {
            if (!match.Value.StartsWith("<img", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            var rewritten = RewriteImageTag(match.Value, lazy: seenMediaImage, out var isMediaImage);
            seenMediaImage |= isMediaImage;
            return rewritten;
        });
    }

    internal static string RewriteWebpPictures(
        string html,
        IReadOnlyDictionary<string, string> webpSrcsets,
        string baseUrl,
        string? siteUrl,
        string? assetBaseUrl,
        SharedAssetScope? sharedAssets)
    {
        var normalizedBaseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        var normalizedAssetBaseUrl = BuildPathUtils.NormalizeBaseUrl(assetBaseUrl ?? baseUrl).TrimEnd('/');
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
            if (src is null || !TryNormalizeLocalImageUrl(src, normalizedBaseUrl, normalizedAssetBaseUrl, sharedAssets, siteUri, out var sourceUrl) ||
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

    private static bool HasAttribute(MatchCollection attributes, string name)
        => attributes.Any(attribute => attribute.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase));

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
        string assetBaseUrl,
        SharedAssetScope? sharedAssets,
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
            var relative = path;
            if (assetBaseUrl.Length > 0)
            {
                if (!relative.StartsWith(assetBaseUrl + "/", StringComparison.Ordinal))
                {
                    return false;
                }

                relative = relative[(assetBaseUrl.Length + 1)..];
            }
            else
            {
                relative = relative.TrimStart('/');
            }

            if (sharedAssets is null || !sharedAssets.Contains(relative))
            {
                return false;
            }
        }
        return true;
    }

    private string RewriteImageTag(string tag, bool lazy, out bool isMediaImage)
    {
        isMediaImage = false;
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
            !TryGetImageSize(sourceFile, logger, out var sourceWidth, out var sourceHeight))
        {
            return tag;
        }

        isMediaImage = true;
        var attributes = string.Empty;
        var applicableSizes = ImageVariantPlan.GeneratedWidths(_sizes, sourceWidth).ToArray();
        if (applicableSizes.Length > 0 && !HasAttribute(parsedAttributes, "srcset"))
        {
            var candidates = applicableSizes
                .Select(size => $"{BuildVariantUrl(sourceUrl, size)} {size}w")
                .Append($"{sourceUrl} {sourceWidth}w");
            attributes += $" srcset=\"{WebUtility.HtmlEncode(string.Join(", ", candidates))}\"";
            attributes += BuildSizesAttribute(parsedAttributes, lazy);
            if (_webpEnabled)
            {
                attributes += $" {GeneratedSrcsetMarker}=\"\"";
            }
        }
        if (!HasAttribute(parsedAttributes, "decoding"))
        {
            attributes += " decoding=\"async\"";
        }

        // Author dimensions win; adding only the missing one could distort the aspect ratio.
        if (!HasAttribute(parsedAttributes, "width") && !HasAttribute(parsedAttributes, "height"))
        {
            attributes += $" width=\"{sourceWidth}\" height=\"{sourceHeight}\"";
        }

        if (lazy && !HasAttribute(parsedAttributes, "loading"))
        {
            attributes += " loading=\"lazy\"";
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

    // Without sizes the browser assumes 100vw and picks a candidate for the full viewport width.
    // "auto" lets it use the laid-out width, which the spec allows only on lazy images.
    private string BuildSizesAttribute(MatchCollection parsedAttributes, bool lazy)
    {
        if (_sizesAttribute is null || HasAttribute(parsedAttributes, "sizes"))
        {
            return string.Empty;
        }

        var loading = GetAttributeValue(parsedAttributes, "loading");
        var isLazy = loading is null ? lazy : loading.Trim().Equals("lazy", StringComparison.OrdinalIgnoreCase);
        var value = isLazy ? $"auto, {_sizesAttribute}" : _sizesAttribute;
        return $" sizes=\"{WebUtility.HtmlEncode(value)}\"";
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
            !ImageVariantPlan.IsResizableSource(candidate))
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
        => ImageVariantPlan.VariantUrl(sourceUrl, width);
}
