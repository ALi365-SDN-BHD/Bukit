using System.Text.Json;
using Bukit.Shared;

namespace Bukit.WechatSyncing;

using static WechatSyncHelpers;

/// <summary>
/// Resolves and uploads cover thumbnails for WeChat drafts.
/// Supports multi-level fallback: cover field -> first image in HTML ->
/// local file -> remote URL download -> media cache -> default image -> defaultThumbMediaId.
/// </summary>
internal sealed class ThumbResolver
{
    private readonly IWechatDraftGateway _gateway;
    private readonly Func<string, CancellationToken, Task<byte[]>> _downloadImageAsync;
    private readonly Bukit.Shared.ILogger _logger;

    internal ThumbResolver(
        IWechatDraftGateway gateway,
        Func<string, CancellationToken, Task<byte[]>> downloadImageAsync,
        Bukit.Shared.ILogger logger)
    {
        _gateway = gateway;
        _downloadImageAsync = downloadImageAsync;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the thumb media ID for a content item, uploading if needed.
    /// Returns the media ID to use and whether the cache was updated.
    /// </summary>
    internal async Task<(string ThumbMediaId, bool CacheUpdated)> ResolveAndUploadThumbAsync(
        WechatSyncContext context,
        WechatSyncItem item,
        WechatSyncOptions cfg,
        SyncCache cache,
        CancellationToken cancellationToken)
    {
        var thumbSource = ResolveThumbSource(item, cfg);
        if (string.IsNullOrWhiteSpace(thumbSource))
        {
            if (!string.IsNullOrWhiteSpace(cfg.DefaultThumbMediaId))
            {
                return (cfg.DefaultThumbMediaId.Trim(), false);
            }

            throw new InvalidOperationException("wechat-sync requires cover, first image in content, or wechat.defaultThumbMediaId.");
        }

        var thumbSourceText = thumbSource.Trim();
        var thumbIsUrl = LooksLikeUrl(thumbSourceText);

        if (!thumbIsUrl)
        {
            // Treat as a pre-existing media ID
            return (thumbSourceText, false);
        }

        var cacheUpdated = false;
        var thumbKey = ComputeUrlKey($"source:{thumbSourceText}");
        var absoluteThumbUrl = string.Empty;
        var hasAbsoluteThumbUrl = false;

        if (TryNormalizeToAbsoluteUrl(thumbSourceText, cfg.SiteUrl, cfg.BaseUrl, out absoluteThumbUrl))
        {
            hasAbsoluteThumbUrl = true;
            thumbKey = ComputeUrlKey(absoluteThumbUrl);
        }
        else if (!string.IsNullOrWhiteSpace(cfg.DefaultThumbMediaId))
        {
            // Can't resolve URL, fall back to default media ID
            return (cfg.DefaultThumbMediaId.Trim(), false);
        }

        var localFileWasFound = TryResolveLocalAssetPath(context, thumbSourceText, out var localPath);
        if (!localFileWasFound && hasAbsoluteThumbUrl)
        {
            localFileWasFound = TryResolveLocalAssetPath(context, absoluteThumbUrl, out localPath);
        }

        if (localFileWasFound)
        {
            thumbKey = ComputeUrlKey($"{thumbKey}:local-file:{SyncCacheManager.ComputeFileSignature(localPath)}");
        }

        // Check thumb media ID cache first
        if (cache.ThumbMediaIds.TryGetValue(thumbKey, out var cachedThumbId) && !string.IsNullOrWhiteSpace(cachedThumbId))
        {
            return (cachedThumbId, false);
        }

        // Try upload from local file
        if (localFileWasFound)
        {
            var localMediaId = await UploadLocalFileAsync(localPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(localMediaId))
            {
                cache.ThumbMediaIds[thumbKey] = localMediaId!;
                return (localMediaId!, true);
            }
        }

        // If local file was found but format conversion failed (e.g. WebP in AOT mode),
        // skip HTTP download and media cache — they would find the same unsupported file.
        // Jump directly to default image fallback.
        if (localFileWasFound)
        {
            _logger.Warn($"plugin wechat-sync cover local file found but format unsupported, skipping HTTP download, falling back to default image");
        }
        else
        {
            if (!hasAbsoluteThumbUrl)
            {
                throw new InvalidOperationException("wechat-sync cover image is not absolute and local file not found.");
            }

            // Try download from URL and upload
            var downloadedMediaId = await TryDownloadAndUploadAsync(absoluteThumbUrl, cancellationToken);
            if (!string.IsNullOrWhiteSpace(downloadedMediaId))
            {
                cache.ThumbMediaIds[thumbKey] = downloadedMediaId!;
                return (downloadedMediaId!, true);
            }

            // URL download failed, try local media cache
            _logger.Warn($"plugin wechat-sync cover download failed imageUrl={absoluteThumbUrl}, try local media cache then fallback default image");
            var localCacheMediaId = await TryUploadThumbFromLocalMediaCacheAsync(context, cfg, cache, absoluteThumbUrl, cancellationToken);
            if (!string.IsNullOrWhiteSpace(localCacheMediaId))
            {
                return (localCacheMediaId!, true);
            }
        }

        // Try default image
        var defaultMediaId = await TryUploadDefaultImageAsync(context, cfg, cache, cancellationToken);
        if (!string.IsNullOrWhiteSpace(defaultMediaId))
        {
            cacheUpdated = true;
            return (defaultMediaId!, cacheUpdated);
        }

        if (!string.IsNullOrWhiteSpace(cfg.DefaultThumbMediaId))
        {
            return (cfg.DefaultThumbMediaId.Trim(), cacheUpdated);
        }

        throw new InvalidOperationException("wechat-sync cover download failed and no fallback available.");
    }

    // ── Upload with format validation ─────────────────────────────────────

    /// <summary>
    /// Wrapper that detects the real image format via magic bytes, converts unsupported
    /// formats (WebP, GIF, BMP) to JPG/PNG using ImageConverter, compresses if needed,
    /// then uploads. Returns <c>null</c> if the format cannot be converted.
    /// </summary>
    private async Task<string?> TryUploadIfSupportedAsync(
        byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
    {
        // Try to normalize image (convert format + compress if needed)
        var normalized = ImageConverter.NormalizeForUpload(bytes, ImageConverter.MaterialImageMaxBytes, _logger);
        if (normalized is not null)
        {
            var (convertedBytes, convertedType, ext) = normalized.Value;
            var convertedFileName = Path.ChangeExtension(
                string.IsNullOrWhiteSpace(Path.GetExtension(fileName)) ? fileName + ".tmp" : fileName, ext);

            return await _gateway.UploadThumbAsync(convertedBytes, convertedFileName, convertedType, cancellationToken);
        }

        // Magic bytes unrecognized - fall back to extension-based content type if it's supported
        var detected = DetectImageContentType(bytes);
        if (string.IsNullOrWhiteSpace(detected) && IsWechatSupportedImage(contentType))
        {
            // Extension says it's JPEG/PNG but no magic bytes match - try uploading as-is
            return await _gateway.UploadThumbAsync(bytes, fileName, contentType, cancellationToken);
        }

        _logger.Warn($"plugin wechat-sync image format not supported and conversion failed: {contentType} ({fileName}), trying fallback");
        return null;
    }

    // ── Thumb source resolution ─────────────────────────────────────────

    internal static string? ResolveThumbSource(WechatSyncItem item, WechatSyncOptions cfg)
    {
        var cover = ReadFieldString(item, "cover");
        if (!string.IsNullOrWhiteSpace(cover))
        {
            return cover;
        }

        if (!string.IsNullOrWhiteSpace(item.ContentHtml) &&
            TryExtractFirstImageUrl(item.ContentHtml, out var raw) &&
            TryNormalizeToAbsoluteUrl(raw, cfg.SiteUrl, cfg.BaseUrl, out var normalized))
        {
            return normalized;
        }

        return null;
    }

    // ── Local file upload ───────────────────────────────────────────────

    private async Task<string?> UploadLocalFileAsync(string localPath, CancellationToken cancellationToken)
    {
        var info = new FileInfo(localPath);
        if (!info.Exists || info.Length == 0)
        {
            return null;
        }

        var bytes = await ImageConverter.TryReadImageFileWithLimitAsync(
            localPath,
            ImageConverter.MaterialImageMaxBytes,
            "cover local file",
            _logger,
            cancellationToken);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var fileName = Path.GetFileName(localPath);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            fileName = $"{fileName}.jpg";
        }

        var contentType = GuessImageContentType(localPath);
        return await TryUploadIfSupportedAsync(bytes, fileName, contentType, cancellationToken);
    }

    // ── Remote URL download + upload ────────────────────────────────────

    private async Task<string?> TryDownloadAndUploadAsync(string absoluteUrl, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _downloadImageAsync(absoluteUrl, cancellationToken);
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            var fileName = InferFileNameFromUrl(absoluteUrl);
            var contentType = GuessImageContentType(fileName);
            return await TryUploadIfSupportedAsync(bytes, fileName, contentType, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warn($"plugin wechat-sync download and upload failed url={absoluteUrl}: {ex.Message}");
            return null;
        }
    }

    private static string InferFileNameFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileName(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return "cover.jpg";
    }

    // ── Local media cache upload ────────────────────────────────────────

    private async Task<string?> TryUploadThumbFromLocalMediaCacheAsync(
        WechatSyncContext context,
        WechatSyncOptions cfg,
        SyncCache cache,
        string absoluteThumbUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(absoluteThumbUrl))
        {
            return null;
        }

        var normalizedForIndex = NormalizeMediaSourceUrlKey(absoluteThumbUrl);
        if (string.IsNullOrWhiteSpace(normalizedForIndex))
        {
            return null;
        }

        if (TryResolveLocalAssetPath(context, absoluteThumbUrl, out var filePath))
        {
            return await TryUploadThumbFromResolvedMediaFileAsync(filePath, absoluteThumbUrl, cache, cancellationToken);
        }

        var effectiveDownloadDir = ResolveEffectiveMediaDownloadDir(context);
        var indexPath = TryResolveFromMediaIndex(effectiveDownloadDir, normalizedForIndex);
        if (!string.IsNullOrWhiteSpace(indexPath) && File.Exists(indexPath))
        {
            var uploadedFromIndex = await TryUploadThumbFromResolvedMediaFileAsync(
                indexPath,
                absoluteThumbUrl,
                cache,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(uploadedFromIndex))
            {
                return uploadedFromIndex;
            }
        }

        var hashPath = TryResolveFromMediaHashName(effectiveDownloadDir, normalizedForIndex, absoluteThumbUrl);
        if (!string.IsNullOrWhiteSpace(hashPath) && File.Exists(hashPath))
        {
            return await TryUploadThumbFromResolvedMediaFileAsync(hashPath, absoluteThumbUrl, cache, cancellationToken);
        }

        _logger.Warn("plugin wechat-sync local media cache miss for cover url");
        return null;
    }

    private async Task<string?> TryUploadThumbFromResolvedMediaFileAsync(
        string filePath,
        string absoluteThumbUrl,
        SyncCache cache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _logger.Warn("plugin wechat-sync local media cache miss for cover url");
            return null;
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            _logger.Warn("plugin wechat-sync local media cache file empty for cover url");
            return null;
        }

        try
        {
            var bytes = await ImageConverter.TryReadImageFileWithLimitAsync(
                filePath,
                ImageConverter.MaterialImageMaxBytes,
                "cover media cache file",
                _logger,
                cancellationToken);
            if (bytes is null || bytes.Length == 0)
            {
                _logger.Warn("plugin wechat-sync local media cache file has zero bytes");
                return null;
            }

            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
            {
                fileName = $"{fileName}.jpg";
            }

            var contentType = GuessImageContentType(filePath);
            var uploaded = await TryUploadIfSupportedAsync(bytes, fileName, contentType, cancellationToken);
            if (string.IsNullOrWhiteSpace(uploaded))
            {
                return null;
            }

            var thumbKey = ComputeUrlKey(absoluteThumbUrl);
            cache.ThumbMediaIds[thumbKey] = uploaded;
            return uploaded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warn($"plugin wechat-sync local media cache upload failed: {ex.Message}");
            return null;
        }
    }

    // ── Default image upload ────────────────────────────────────────────

    private async Task<string?> TryUploadDefaultImageAsync(
        WechatSyncContext context,
        WechatSyncOptions cfg,
        SyncCache cache,
        CancellationToken cancellationToken)
    {
        var fallback = (cfg.DefaultImageUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fallback))
        {
            return cfg.DefaultThumbMediaId?.Trim();
        }

        if (!IsHttpUrl(fallback))
        {
            // Try local file first
            if (TryResolveLocalAssetPath(context, fallback, out var filePath))
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists && fileInfo.Length > 0)
                {
                    var fileKey = ComputeUrlKey($"file:{SyncCacheManager.ComputeFileSignature(filePath)}");
                    if (cache.ThumbMediaIds.TryGetValue(fileKey, out var cachedFile) && !string.IsNullOrWhiteSpace(cachedFile))
                    {
                        return cachedFile;
                    }

                    try
                    {
                        var bytes = await ImageConverter.TryReadImageFileWithLimitAsync(
                            filePath,
                            ImageConverter.MaterialImageMaxBytes,
                            "defaultImageUrl local file",
                            _logger,
                            cancellationToken);
                        if (bytes is null)
                        {
                            return cfg.DefaultThumbMediaId?.Trim();
                        }

                        var fileName = Path.GetFileName(filePath);
                        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
                        {
                            fileName = $"{fileName}.jpg";
                        }

                        var contentType = GuessImageContentType(filePath);
                        _logger.Info($"plugin wechat-sync uploading defaultImageUrl from local file path={filePath}");
                        var uploaded = await TryUploadIfSupportedAsync(bytes, fileName, contentType, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(uploaded))
                        {
                            cache.ThumbMediaIds[fileKey] = uploaded;
                        }

                        return uploaded ?? cfg.DefaultThumbMediaId?.Trim();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.Warn($"plugin wechat-sync defaultImageUrl local upload failed, fallback to wechat.defaultThumbMediaId: {ex.Message}");
                        return cfg.DefaultThumbMediaId?.Trim();
                    }
                }
                else
                {
                    _logger.Warn($"plugin wechat-sync defaultImageUrl local file is empty path={filePath}, fallback to wechat.defaultThumbMediaId");
                    return cfg.DefaultThumbMediaId?.Trim();
                }
            }

            // Local file not found, try absolute URL
            _logger.Warn($"plugin wechat-sync defaultImageUrl local file not found path={fallback}, try absolute url upload");
            if (TryNormalizeToAbsoluteUrl(fallback, cfg.SiteUrl, cfg.BaseUrl, out var fallbackAbsolute))
            {
                var uploaded = await TryDownloadAndUploadForDefaultAsync(fallbackAbsolute, cache, cancellationToken);
                if (!string.IsNullOrWhiteSpace(uploaded))
                {
                    return uploaded;
                }
            }

            return cfg.DefaultThumbMediaId?.Trim();
        }

        // HTTP URL
        if (!TryNormalizeToAbsoluteUrl(fallback, cfg.SiteUrl, cfg.BaseUrl, out var absolute))
        {
            return cfg.DefaultThumbMediaId?.Trim();
        }

        return await TryDownloadAndUploadForDefaultAsync(absolute, cache, cancellationToken);
    }

    private async Task<string?> TryDownloadAndUploadForDefaultAsync(
        string absoluteUrl,
        SyncCache cache,
        CancellationToken cancellationToken)
    {
        var key = ComputeUrlKey(absoluteUrl);
        if (cache.ThumbMediaIds.TryGetValue(key, out var cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        try
        {
            _logger.Info($"plugin wechat-sync uploading defaultImageUrl by url imageUrl={absoluteUrl}");
            var bytes = await _downloadImageAsync(absoluteUrl, cancellationToken);
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            var fileName = InferFileNameFromUrl(absoluteUrl);
            var contentType = GuessImageContentType(fileName);
            var uploaded = await TryUploadIfSupportedAsync(bytes, fileName, contentType, cancellationToken);
            if (!string.IsNullOrWhiteSpace(uploaded))
            {
                cache.ThumbMediaIds[key] = uploaded;
            }

            return uploaded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warn($"plugin wechat-sync defaultImageUrl download failed, fallback to wechat.defaultThumbMediaId: {ex.Message}");
            return null;
        }
    }

    // ── Local asset path resolution ─────────────────────────────────────

    internal static bool TryResolveLocalAssetPath(WechatSyncContext context, string urlOrPath, out string filePath)
    {
        filePath = string.Empty;
        if (string.IsNullOrWhiteSpace(urlOrPath))
        {
            return false;
        }

        var original = urlOrPath.Trim();
        if (TryResolveAbsoluteLocalFilePath(original, out filePath) &&
            IsUnderAllowedLocalRoot(filePath, context.RootDir, context.OutputDir))
        {
            return true;
        }

        var raw = urlOrPath.Trim().Replace('\\', '/');

        // Strip site domain from absolute HTTP URLs so that
        // "https://0060.my/assets/uploads/img.webp" becomes "/assets/uploads/img.webp"
        raw = StripSiteDomainIfMatch(raw, context.SiteUrl, context.BaseUrl);
        var baseUrl = (context.BaseUrl ?? "/").Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "/";
        }

        if (!baseUrl.StartsWith('/'))
        {
            baseUrl = "/" + baseUrl;
        }

        baseUrl = baseUrl.Length > 1 ? baseUrl.TrimEnd('/') : "/";

        if (baseUrl != "/" && raw.StartsWith(baseUrl + "/", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw.Substring(baseUrl.Length);
        }

        raw = raw.TrimStart('/');
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var rel = raw.Replace('/', Path.DirectorySeparatorChar);
        var distCandidate = Path.GetFullPath(Path.Combine(context.OutputDir, rel));
        if (IsUnderRoot(distCandidate, context.OutputDir) && File.Exists(distCandidate))
        {
            filePath = distCandidate;
            return true;
        }

        var inferredAssetsDirs = new List<string>(capacity: 1);
        inferredAssetsDirs.Add(Path.Combine(context.RootDir, "assets"));

        var relUnix = raw;
        if (relUnix.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            relUnix = relUnix.Substring("assets/".Length);
        }

        var relForAssets = relUnix.Replace('/', Path.DirectorySeparatorChar);
        foreach (var assetsDir in inferredAssetsDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.GetFullPath(Path.Combine(assetsDir, relForAssets));
            if (IsUnderRoot(candidate, assetsDir) && File.Exists(candidate))
            {
                filePath = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveAbsoluteLocalFilePath(string raw, out string filePath)
    {
        filePath = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var candidates = new List<string>(capacity: 3) { raw };
        if (raw.StartsWith("/", StringComparison.Ordinal) && raw.Length > 2 && raw[2] == ':')
        {
            candidates.Add(raw.TrimStart('/'));
        }

        if (raw.Contains('\\'))
        {
            candidates.Add(raw.Replace('\\', '/'));
        }

        foreach (var c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (LooksLikeWindowsAbsolutePath(c) || Path.IsPathRooted(c))
            {
                try
                {
                    var full = Path.GetFullPath(c);
                    if (File.Exists(full))
                    {
                        filePath = full;
                        return true;
                    }
                }
                catch
                {
                    // ignore invalid local path candidates
                }
            }
        }

        return false;
    }

    private static bool IsUnderAllowedLocalRoot(string path, string rootDir, string outputDir)
        => IsUnderRoot(path, rootDir) || IsUnderRoot(path, outputDir);

    private static bool IsUnderRoot(string path, string root)
        => PathUtils.IsSameOrSubPathOf(path, root);

    // ── Media cache helpers ─────────────────────────────────────────────

    internal static string ResolveEffectiveMediaDownloadDir(WechatSyncContext context)
    {
        var downloadDir = (context.MediaDownloadDir ?? string.Empty).Trim();
        var resolved = downloadDir.Length == 0 || string.Equals(downloadDir, "assets/uploads", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(context.RootDir, ".cache", "media")
            : Path.IsPathRooted(downloadDir)
                ? downloadDir
                : Path.GetFullPath(Path.Combine(context.RootDir, downloadDir));

        if (!PathUtils.IsSameOrSubPathOf(resolved, context.RootDir))
        {
            context.Logger.Warn("plugin wechat-sync media download directory real path escapes project root, ignoring local media cache");
            return string.Empty;
        }

        return resolved;
    }

    internal static string? TryResolveFromMediaIndex(string downloadDir, string normalizedKey)
    {
        if (string.IsNullOrWhiteSpace(downloadDir) || string.IsNullOrWhiteSpace(normalizedKey))
        {
            return null;
        }

        var indexPath = Path.Combine(downloadDir, ".media-index.json");
        if (!File.Exists(indexPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(indexPath);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            JsonElement entries;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("entries", out var e) &&
                e.ValueKind == JsonValueKind.Object)
            {
                entries = e;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                entries = root;
            }
            else
            {
                return null;
            }

            if (!entries.TryGetProperty(normalizedKey, out var fileEl) || fileEl.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var fileName = fileEl.GetString();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var path = Path.GetFullPath(Path.Combine(downloadDir, fileName.Trim()));
            return IsUnderRoot(path, downloadDir) && File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    internal static string? TryResolveFromMediaHashName(string downloadDir, string normalizedKey, string originalAbsoluteUrl)
    {
        if (string.IsNullOrWhiteSpace(downloadDir) || string.IsNullOrWhiteSpace(normalizedKey))
        {
            return null;
        }

        var ext = ResolveMediaCacheExtension(originalAbsoluteUrl);
        var fileName = BuildMediaCacheStableFileName(normalizedKey, ext);
        var path = Path.GetFullPath(Path.Combine(downloadDir, fileName));
        return IsUnderRoot(path, downloadDir) && File.Exists(path) ? path : null;
    }

    /// <summary>
    /// If <paramref name="url"/> is an absolute HTTP(S) URL whose authority matches
    /// the site domain (from <paramref name="siteUrl"/>), strips the scheme + authority
    /// (and optional base URL prefix) to return only the path portion.
    /// Otherwise returns the input unchanged.
    /// </summary>
    internal static string StripSiteDomainIfMatch(string url, string? siteUrl, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(siteUrl))
        {
            return url;
        }

        if (!IsHttpUrl(url))
        {
            return url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !Uri.TryCreate(siteUrl, UriKind.Absolute, out var siteUri))
        {
            return url;
        }

        // Compare authority (host + port)
        if (!string.Equals(uri.Authority, siteUri.Authority, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        // Authority matches — extract the path portion
        var path = uri.AbsolutePath;

        // Strip baseUrl prefix if present (e.g. "/blog" from "/blog/assets/uploads/img.webp")
        var normalizedBase = (baseUrl ?? "/").Trim();
        if (!normalizedBase.StartsWith('/'))
        {
            normalizedBase = "/" + normalizedBase;
        }

        normalizedBase = normalizedBase.Length > 1 ? normalizedBase.TrimEnd('/') : "/";

        if (normalizedBase != "/" && path.StartsWith(normalizedBase + "/", StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(normalizedBase.Length);
        }

        return path;
    }
}
