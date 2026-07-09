using System.Text.RegularExpressions;

namespace Bukit.WechatSyncing;

using static WechatSyncHelpers;

/// <summary>
/// Processes inline images in article HTML content:
/// - Finds all &lt;img&gt; tags
/// - Resolves image URLs (handling lazy-load attributes)
/// - Downloads images from source URLs
/// - Converts unsupported formats to JPEG/PNG
/// - Uploads to WeChat via <c>media/uploadimg</c>
/// - Replaces <c>src</c> attributes with WeChat CDN URLs
/// - Deduplicates uploads for the same URL
/// </summary>
internal sealed class ContentImageProcessor
{
    private readonly IWechatDraftGateway _gateway;
    private readonly Func<string, CancellationToken, Task<byte[]>> _downloadImageAsync;
    private readonly Bukit.Shared.ILogger _logger;

    internal ContentImageProcessor(
        IWechatDraftGateway gateway,
        Func<string, CancellationToken, Task<byte[]>> downloadImageAsync,
        Bukit.Shared.ILogger logger)
    {
        _gateway = gateway;
        _downloadImageAsync = downloadImageAsync;
        _logger = logger;
    }

    /// <summary>
    /// Processes all inline images in the HTML content.
    /// Downloads, converts, uploads images to WeChat, and replaces src attributes.
    /// </summary>
    internal async Task<string> ProcessImagesAsync(
        WechatSyncContext context,
        string html,
        WechatSyncOptions cfg,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Track uploaded URLs to avoid duplicates
        var uploadMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Find all <img> tags and process them
        var result = await ReplaceImgTagsAsync(html, uploadMap, context, cfg, cancellationToken);
        return result;
    }

    /// <summary>
    /// Finds all img tags in HTML and replaces their src with WeChat CDN URLs.
    /// Uses async processing for each image.
    /// </summary>
    private async Task<string> ReplaceImgTagsAsync(
        string html,
        Dictionary<string, string> uploadMap,
        WechatSyncContext context,
        WechatSyncOptions cfg,
        CancellationToken cancellationToken)
    {
        // Collect all img tag matches first
        var imgMatches = Regex.Matches(html, @"<img\b[^>]*/?>", RegexOptions.IgnoreCase);
        if (imgMatches.Count == 0)
        {
            return html;
        }

        // Process each img tag - we need to go backwards to preserve string positions
        var sb = new System.Text.StringBuilder(html);
        for (var i = imgMatches.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var match = imgMatches[i];
            var imgTag = match.Value;

            var replacement = await ProcessSingleImgTagAsync(imgTag, uploadMap, context, cfg, cancellationToken);
            if (replacement is not null && replacement != imgTag)
            {
                sb.Remove(match.Index, match.Length);
                sb.Insert(match.Index, replacement);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Processes a single &lt;img&gt; tag: resolves URL, downloads, uploads, and returns the modified tag.
    /// Returns null if the tag should be left unchanged.
    /// </summary>
    private async Task<string?> ProcessSingleImgTagAsync(
        string imgTag,
        Dictionary<string, string> uploadMap,
        WechatSyncContext context,
        WechatSyncOptions cfg,
        CancellationToken cancellationToken)
    {
        // Resolve the best candidate URL from various attributes
        var candidateUrl = ResolveBestImageUrl(imgTag);
        if (string.IsNullOrWhiteSpace(candidateUrl))
        {
            return null;
        }

        // Skip data: URLs and SVGs
        if (candidateUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Regex.IsMatch(candidateUrl, @"\.svg(\?|$)", RegexOptions.IgnoreCase))
        {
            _logger.Warn($"plugin wechat-sync skipping SVG image: {candidateUrl}");
            return null;
        }

        // Check dedup map
        if (uploadMap.TryGetValue(candidateUrl, out var cachedWechatUrl))
        {
            return BuildReplacedImgTag(imgTag, cachedWechatUrl, candidateUrl);
        }

        // Normalize URL to absolute
        var absoluteUrl = NormalizeImageUrl(candidateUrl, cfg);
        if (string.IsNullOrWhiteSpace(absoluteUrl))
        {
            _logger.Warn($"plugin wechat-sync cannot resolve image URL: {candidateUrl}");
            return null;
        }

        // Try to download and upload
        try
        {
            var bytes = await DownloadImageAsync(context, candidateUrl, absoluteUrl, cancellationToken);
            if (bytes is null || bytes.Length == 0)
            {
                _logger.Warn($"plugin wechat-sync image download returned empty: {absoluteUrl}");
                return null;
            }

            // Normalize image format and size for uploadimg (2MB limit, JPEG/PNG only)
            var normalized = ImageConverter.NormalizeForUpload(bytes, ImageConverter.ContentImageMaxBytes, _logger);
            if (normalized is null)
            {
                _logger.Warn($"plugin wechat-sync image format conversion failed, skipping: {absoluteUrl}");
                return null;
            }

            var (convertedBytes, contentType, ext) = normalized.Value;

            // Upload to WeChat
            var fileName = InferFileNameFromUrl(absoluteUrl, ext);
            var wechatUrl = await _gateway.UploadContentImageAsync(convertedBytes, fileName, contentType, cancellationToken);

            if (string.IsNullOrWhiteSpace(wechatUrl))
            {
                _logger.Warn($"plugin wechat-sync uploadimg returned empty URL for: {absoluteUrl}");
                return null;
            }

            // Cache in dedup map
            uploadMap[candidateUrl] = wechatUrl;
            _logger.Info($"plugin wechat-sync uploaded inline image: {candidateUrl} -> {wechatUrl}");

            return BuildReplacedImgTag(imgTag, wechatUrl, candidateUrl);
        }
        catch (Exception ex)
        {
            _logger.Warn($"plugin wechat-sync inline image upload failed for {absoluteUrl}: {ex.Message}");
            return null;
        }
    }

    // ── URL resolution ──────────────────────────────────────────────────

    /// <summary>
    /// Resolves the best image URL from an img tag, checking lazy-load attributes first.
    /// Priority: data-src -> data-original -> data-actualsrc -> data-lazy-src -> src -> srcset
    /// </summary>
    internal static string? ResolveBestImageUrl(string imgTag)
    {
        // Try lazy-load attributes first
        string? candidate = null;
        foreach (var attr in new[] { "data-src", "data-original", "data-actualsrc", "data-lazy-src" })
        {
            if (TryReadHtmlAttribute(imgTag, attr, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                candidate = val.Trim();
                if (!candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                candidate = null;
            }
        }

        // Fall back to src
        if (string.IsNullOrWhiteSpace(candidate))
        {
            if (TryReadHtmlAttribute(imgTag, "src", out var src) && !string.IsNullOrWhiteSpace(src))
            {
                candidate = src.Trim();
                if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = null;
                }
            }
        }

        // Last resort: srcset
        if (string.IsNullOrWhiteSpace(candidate))
        {
            if (TryReadHtmlAttribute(imgTag, "srcset", out var srcset))
            {
                candidate = TryPickBestSrcsetUrl(srcset);
            }
        }

        // Sanitize
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            candidate = SanitizeCandidateUrl(candidate);
        }

        return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
    }

    /// <summary>
    /// Sanitizes a candidate URL by decoding entities, trimming whitespace,
    /// and removing wrapper characters.
    /// </summary>
    internal static string SanitizeCandidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var u = System.Net.WebUtility.HtmlDecode(url);
        u = u.Trim();

        // Handle url(...) values before general character trimming
        if (u.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && u.EndsWith(')'))
        {
            u = u[4..^1];
        }

        u = u.Trim(' ', '\t', '\n', '\r', '\0', '"', '\'', '`');

        return u;
    }

    private string NormalizeImageUrl(string candidateUrl, WechatSyncOptions cfg)
    {
        if (IsHttpUrl(candidateUrl))
        {
            return candidateUrl;
        }

        if (candidateUrl.StartsWith("//", StringComparison.Ordinal))
        {
            var scheme = TryGetScheme(cfg.SiteUrl) ?? "https";
            return $"{scheme}:{candidateUrl}";
        }

        if (TryNormalizeToAbsoluteUrl(candidateUrl, cfg.SiteUrl, cfg.BaseUrl, out var absolute))
        {
            return absolute;
        }

        return string.Empty;
    }

    // ── Image download ──────────────────────────────────────────────────

    /// <summary>
    /// Downloads image bytes using a multi-level fallback strategy:
    /// <list type="number">
    ///   <item>Try resolving the original candidate URL (often a relative path) to a local file</item>
    ///   <item>Try resolving the absolute URL to a local file (handles site-domain stripping)</item>
    ///   <item>Look up the media download directory via <c>.media-index.json</c></item>
    ///   <item>Try hash-based filename match in the media download directory</item>
    ///   <item>Fall back to HTTP download</item>
    /// </list>
    /// </summary>
    private async Task<byte[]?> DownloadImageAsync(
        WechatSyncContext context,
        string candidateUrl,
        string absoluteUrl,
        CancellationToken cancellationToken)
    {
        // 1. Try resolving the original candidateUrl (relative path like "/assets/uploads/xxx.webp")
        if (!string.Equals(candidateUrl, absoluteUrl, StringComparison.Ordinal) &&
            ThumbResolver.TryResolveLocalAssetPath(context, candidateUrl, out var localPath1))
        {
            if (File.Exists(localPath1) && new FileInfo(localPath1).Length > 0)
            {
                _logger.Info($"plugin wechat-sync resolved image from local path (candidateUrl): {candidateUrl}");
                return await File.ReadAllBytesAsync(localPath1, cancellationToken);
            }
        }

        // 2. Try resolving the absoluteUrl to a local file
        //    (TryResolveLocalAssetPath now strips site domain, so https://0060.my/assets/... works)
        if (ThumbResolver.TryResolveLocalAssetPath(context, absoluteUrl, out var localPath2))
        {
            if (File.Exists(localPath2) && new FileInfo(localPath2).Length > 0)
            {
                _logger.Info($"plugin wechat-sync resolved image from local path (absoluteUrl): {absoluteUrl}");
                return await File.ReadAllBytesAsync(localPath2, cancellationToken);
            }
        }

        // 3. Try media cache: look up .media-index.json and hash-based filenames
        var localCacheBytes = TryReadFromMediaCache(context, absoluteUrl);
        if (localCacheBytes is not null)
        {
            _logger.Info($"plugin wechat-sync resolved image from media cache: {absoluteUrl}");
            return localCacheBytes;
        }

        // 4. Fall back to HTTP download
        try
        {
            return await _downloadImageAsync(absoluteUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Warn($"plugin wechat-sync inline image download failed url={absoluteUrl}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to read image bytes from the local media download directory
    /// by consulting the <c>.media-index.json</c> index or by computing the
    /// hash-based stable filename used by Bukit's media asset localization.
    /// </summary>
    private byte[]? TryReadFromMediaCache(WechatSyncContext context, string absoluteUrl)
    {
        if (!IsHttpUrl(absoluteUrl))
        {
            return null;
        }

        var normalizedKey = NormalizeMediaSourceUrlKey(absoluteUrl);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return null;
        }

        var downloadDir = ThumbResolver.ResolveEffectiveMediaDownloadDir(context);

        // Try .media-index.json lookup
        var indexPath = ThumbResolver.TryResolveFromMediaIndex(downloadDir, normalizedKey);
        if (!string.IsNullOrWhiteSpace(indexPath) && File.Exists(indexPath))
        {
            var info = new FileInfo(indexPath);
            if (info.Length > 0)
            {
                return File.ReadAllBytes(indexPath);
            }
        }

        // Try hash-based filename
        var hashPath = ThumbResolver.TryResolveFromMediaHashName(downloadDir, normalizedKey, absoluteUrl);
        if (!string.IsNullOrWhiteSpace(hashPath) && File.Exists(hashPath))
        {
            var info = new FileInfo(hashPath);
            if (info.Length > 0)
            {
                return File.ReadAllBytes(hashPath);
            }
        }

        return null;
    }

    // ── Tag rebuilding ──────────────────────────────────────────────────

    /// <summary>
    /// Builds a replacement img tag with the WeChat CDN URL,
    /// removing lazy-load attributes and updating src.
    /// </summary>
    private static string BuildReplacedImgTag(string originalTag, string wechatUrl, string originalUrl)
    {
        var tag = originalTag;

        // Remove lazy-load attributes
        tag = Regex.Replace(tag, @"\s+(?:srcset|data-(?:src|original|actualsrc|lazy-src|lazyload|lazy)|loading|decoding)\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", string.Empty, RegexOptions.IgnoreCase);

        // Replace or add src attribute
        if (Regex.IsMatch(tag, @"\bsrc\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase))
        {
            tag = Regex.Replace(tag, @"\bsrc\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", $"src=\"{wechatUrl}\"", RegexOptions.IgnoreCase);
        }
        else
        {
            tag = Regex.Replace(tag, @"<img\b", $"<img src=\"{wechatUrl}\"", RegexOptions.IgnoreCase);
        }

        // Add alt attribute if missing
        if (!Regex.IsMatch(tag, @"\balt\s*=", RegexOptions.IgnoreCase))
        {
            var altText = InferAltFromUrl(originalUrl);
            tag = Regex.Replace(tag, @"(/?)>$", $" alt=\"{altText}\"$1>");
        }

        return tag;
    }

    private static string InferAltFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return "image";
    }

    private static string InferFileNameFromUrl(string url, string ext)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name + ext;
            }
        }

        return "image" + ext;
    }
}
