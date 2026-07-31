using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Shared;

namespace Bukit.WechatSyncing;

/// <summary>
/// Gateway abstraction for WeChat draft/material operations.
/// </summary>
public interface IWechatDraftGateway
{
    Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken);

    Task<string> UploadThumbAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads an inline content image via <c>media/uploadimg</c>.
    /// Returns the WeChat CDN URL (not a media_id).
    /// </summary>
    Task<string> UploadContentImageAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Submits a draft for publishing via <c>freepublish/submit</c>.
    /// Returns the publish_id for status polling.
    /// </summary>
    Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the publish status via <c>freepublish/get</c>.
    /// Returns the parsed JSON result including publish_status.
    /// </summary>
    Task<WechatPublishStatusResult> CheckPublishStatusAsync(string publishId, CancellationToken cancellationToken);
}

public sealed record WechatDraftRequest(
    string Title,
    string Author,
    string Digest,
    string ContentHtml,
    string ContentSourceUrl,
    string ThumbMediaId,
    bool NeedOpenComment,
    bool OnlyFansCanComment);

/// <summary>
/// Concrete WeChat API gateway. Uses hand-crafted multipart/form-data for material upload
/// to ensure WeChat reliably reads the <c>media</c> field.
/// </summary>
internal sealed class WechatDraftGateway : IWechatDraftGateway, IDisposable
{
    internal const int MaxDownloadedImageBytes = ImageConverter.MaterialImageMaxBytes;

    private readonly HttpClient _httpClient;
    private readonly Bukit.Shared.ILogger _logger;
    private readonly string _appId;
    private readonly string _appSecret;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedAccessToken;
    private DateTimeOffset _tokenExpireAt = DateTimeOffset.MinValue;

    public WechatDraftGateway(Bukit.Shared.ILogger logger, string appId, string appSecret)
    {
        _logger = logger;
        _appId = appId;
        _appSecret = appSecret;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    // ── Token-retry helper ─────────────────────────────────────────────

    private async Task<T> ExecuteWithTokenRetryAsync<T>(
        Func<string, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        try
        {
            return await operation(token, cancellationToken);
        }
        catch (WechatApiException ex) when (IsTokenInvalid(ex.ErrCode))
        {
            await InvalidateTokenAsync();
            token = await GetAccessTokenAsync(cancellationToken);
            return await operation(token, cancellationToken);
        }
    }

    // ── AddDraft ────────────────────────────────────────────────────────

    public async Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
    {
        WechatDraftContract.ValidateDraft(request);
        return await ExecuteWithTokenRetryAsync(
            (token, ct) => AddDraftCoreAsync(token, request, ct),
            cancellationToken);
    }

    private async Task<string> AddDraftCoreAsync(string accessToken, WechatDraftRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"https://api.weixin.qq.com/cgi-bin/draft/add?access_token={Uri.EscapeDataString(accessToken)}";
        var payload = new WechatDraftAddRequest(new[]
        {
            new WechatDraftArticle(
                request.Title,
                request.Author,
                request.Digest,
                request.ContentHtml,
                request.ContentSourceUrl,
                request.ThumbMediaId,
                request.NeedOpenComment ? 1 : 0,
                request.OnlyFansCanComment ? 1 : 0)
        });

        // Use UnsafeRelaxedJsonEscaping so that non-ASCII characters (Chinese, etc.)
        // are written as-is instead of being escaped to \uXXXX.
        // WeChat API docs: "不要输入\u4f5c\u8005\u540d这种表达，直接传字符串即可"
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }))
        {
            JsonSerializer.Serialize(writer, payload, WechatSyncJsonContext.Default.WechatDraftAddRequest);
        }

        var json = Encoding.UTF8.GetString(buffer.ToArray());
        using var resp = await _httpClient.PostAsync(
            endpoint,
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new WechatHttpException("wechat draft add", (int)resp.StatusCode, text);
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        ThrowIfWechatErr(root, "wechat draft add", text);

        if (!root.TryGetProperty("media_id", out var mediaIdEl) || mediaIdEl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("wechat draft add response missing media_id.");
        }

        var mediaId = mediaIdEl.GetString();
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            throw new InvalidOperationException("wechat draft add response media_id is empty.");
        }

        return mediaId.Trim();
    }

    // ── UploadThumb (hand-crafted multipart/form-data) ──────────────────

    public async Task<string> UploadThumbAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
    {
        return await ExecuteWithTokenRetryAsync(
            (token, ct) => UploadMaterialCoreAsync(token, bytes, fileName, contentType, ct),
            cancellationToken);
    }

    private async Task<string> UploadMaterialCoreAsync(
        string accessToken,
        byte[] bytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (bytes is null || bytes.Length == 0)
        {
            throw new InvalidOperationException("wechat addMaterial bytes is empty.");
        }

        ValidateSize("image", bytes.Length);

        contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        fileName = SanitizeFileName(string.IsNullOrWhiteSpace(fileName) ? "media" : fileName.Trim());

        // Defensive: detect real content type from magic bytes and fix filename extension
        var detected = WechatSyncHelpers.DetectImageContentType(bytes);
        if (!string.IsNullOrWhiteSpace(detected))
        {
            contentType = detected;
            fileName = Path.ChangeExtension(
                string.IsNullOrWhiteSpace(Path.GetExtension(fileName)) ? fileName + ".tmp" : fileName,
                WechatSyncHelpers.ContentTypeToExtension(contentType));
        }
        else
        {
            fileName = EnsureFileExtension(fileName, contentType);
        }

        var endpoint =
            $"https://api.weixin.qq.com/cgi-bin/material/add_material?access_token={Uri.EscapeDataString(accessToken)}&type=image";

        using var body = BuildMultipartContent(bytes, fileName, contentType);

        using var resp = await _httpClient.PostAsync(endpoint, body, cancellationToken);
        var textResp = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new WechatHttpException("wechat addMaterial", (int)resp.StatusCode, textResp);
        }

        using var doc = JsonDocument.Parse(textResp);
        var root = doc.RootElement;
        ThrowIfWechatErr(root, "wechat addMaterial", textResp);

        if (!root.TryGetProperty("media_id", out var mediaIdEl) || mediaIdEl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("wechat addMaterial response missing media_id.");
        }

        var mediaId = mediaIdEl.GetString();
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            throw new InvalidOperationException("wechat addMaterial response media_id is empty.");
        }

        return mediaId.Trim();
    }

    // ── UploadContentImage (inline images via media/uploadimg) ─────────

    public async Task<string> UploadContentImageAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
    {
        WechatDraftContract.ValidateInlineImage(bytes, contentType);
        return await ExecuteWithTokenRetryAsync(
            (token, ct) => UploadContentImageCoreAsync(token, bytes, fileName, contentType, ct),
            cancellationToken);
    }

    private async Task<string> UploadContentImageCoreAsync(
        string accessToken,
        byte[] bytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (bytes is null || bytes.Length == 0)
        {
            throw new InvalidOperationException("wechat uploadimg bytes is empty.");
        }

        contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        fileName = SanitizeFileName(string.IsNullOrWhiteSpace(fileName) ? "media" : fileName.Trim());

        var detected = WechatSyncHelpers.DetectImageContentType(bytes);
        if (!string.IsNullOrWhiteSpace(detected))
        {
            contentType = detected;
            fileName = Path.ChangeExtension(
                string.IsNullOrWhiteSpace(Path.GetExtension(fileName)) ? fileName + ".tmp" : fileName,
                WechatSyncHelpers.ContentTypeToExtension(contentType));
        }
        else
        {
            fileName = EnsureFileExtension(fileName, contentType);
        }

        var endpoint =
            $"https://api.weixin.qq.com/cgi-bin/media/uploadimg?access_token={Uri.EscapeDataString(accessToken)}";

        using var body = BuildMultipartContent(bytes, fileName, contentType);

        using var resp = await _httpClient.PostAsync(endpoint, body, cancellationToken);
        var textResp = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new WechatHttpException("wechat uploadimg", (int)resp.StatusCode, textResp);
        }

        using var doc = JsonDocument.Parse(textResp);
        var root = doc.RootElement;
        ThrowIfWechatErr(root, "wechat uploadimg", textResp);

        if (!root.TryGetProperty("url", out var urlEl) || urlEl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("wechat uploadimg response missing url.");
        }

        var url = urlEl.GetString();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("wechat uploadimg response url is empty.");
        }

        return url.Trim();
    }

    // ── Publish (freepublish/submit) ────────────────────────────────────

    public async Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
    {
        return await ExecuteWithTokenRetryAsync(
            (token, ct) => PublishCoreAsync(token, mediaId, ct),
            cancellationToken);
    }

    private async Task<string> PublishCoreAsync(string accessToken, string mediaId, CancellationToken cancellationToken)
    {
        var endpoint = $"https://api.weixin.qq.com/cgi-bin/freepublish/submit?access_token={Uri.EscapeDataString(accessToken)}";
        var payload = new WechatPublishSubmitRequest(mediaId);
        var json = JsonSerializer.Serialize(payload, WechatSyncJsonContext.Default.WechatPublishSubmitRequest);
        using var resp = await _httpClient.PostAsync(
            endpoint,
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new WechatHttpException("wechat publish", (int)resp.StatusCode, text);
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        ThrowIfWechatErr(root, "wechat publish", text);

        if (!root.TryGetProperty("publish_id", out var publishIdEl) || publishIdEl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("wechat publish response missing publish_id.");
        }

        var publishId = publishIdEl.GetString();
        if (string.IsNullOrWhiteSpace(publishId))
        {
            throw new InvalidOperationException("wechat publish response publish_id is empty.");
        }

        return publishId.Trim();
    }

    // ── CheckPublishStatus (freepublish/get) ────────────────────────────

    public async Task<WechatPublishStatusResult> CheckPublishStatusAsync(string publishId, CancellationToken cancellationToken)
    {
        return await ExecuteWithTokenRetryAsync(
            (token, ct) => CheckPublishStatusCoreAsync(token, publishId, ct),
            cancellationToken);
    }

    private async Task<WechatPublishStatusResult> CheckPublishStatusCoreAsync(
        string accessToken, string publishId, CancellationToken cancellationToken)
    {
        var endpoint = $"https://api.weixin.qq.com/cgi-bin/freepublish/get?access_token={Uri.EscapeDataString(accessToken)}";
        var payload = new WechatPublishStatusRequest(publishId);
        var json = JsonSerializer.Serialize(payload, WechatSyncJsonContext.Default.WechatPublishStatusRequest);
        using var resp = await _httpClient.PostAsync(
            endpoint,
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new WechatHttpException("wechat freepublish/get", (int)resp.StatusCode, text);
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        ThrowIfWechatErr(root, "wechat freepublish/get", text);

        var status = root.TryGetProperty("publish_status", out var statusEl) && statusEl.ValueKind == JsonValueKind.Number
            ? statusEl.GetInt32()
            : -1;

        string? articleUrl = null;
        if (root.TryGetProperty("article_id", out var articleIdEl) && articleIdEl.ValueKind == JsonValueKind.String)
        {
            articleUrl = articleIdEl.GetString();
        }

        // Try to extract article URL from article_detail
        if (root.TryGetProperty("article_detail", out var detailEl) && detailEl.ValueKind == JsonValueKind.Object)
        {
            if (detailEl.TryGetProperty("item", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsEl.EnumerateArray())
                {
                    if (item.TryGetProperty("article_url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                    {
                        articleUrl = urlEl.GetString();
                        break;
                    }
                }
            }
        }

        return new WechatPublishStatusResult(publishId, status, articleUrl);
    }

    // ── Hand-crafted multipart builder ──────────────────────────────────

    /// <summary>
    /// Builds a <see cref="MultipartFormDataContent"/> with a single file part
    /// (name="media") for WeChat material/image upload endpoints.
    /// Uses streaming — avoids allocating a full byte[] copy of the multipart body.
    /// </summary>
    internal static MultipartFormDataContent BuildMultipartContent(byte[] fileBytes, string fileName, string contentType)
    {
        var safeContentType = contentType.Replace("\"", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        var safeFileName = fileName.Replace("\"", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(safeContentType);

        var multipart = new MultipartFormDataContent();
        multipart.Add(fileContent, "media", safeFileName);
        return multipart;
    }

    /// <summary>
    /// Sanitizes an HTTP error response body for logging.
    /// When the body looks like HTML (e.g. nginx/IIS error pages), returns a short
    /// placeholder instead of dumping the entire page into the log.
    /// </summary>
    internal static string SanitizeErrorResponseBody(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return "(empty body)";
        }

        var text = Encoding.UTF8.GetString(bytes);
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(empty body)";
        }

        // Detect HTML content: check for common HTML markers
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<head", StringComparison.OrdinalIgnoreCase))
        {
            return $"(HTML response, {bytes.Length} bytes)";
        }

        // For non-HTML responses, truncate if too long (keep max 200 chars)
        const int maxLen = 200;
        if (text.Length > maxLen)
        {
            return text[..maxLen] + "...";
        }

        return text;
    }

    internal static string SanitizeFileName(string fileName)
    {
        var cleaned = fileName.Replace("\"", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        return string.IsNullOrWhiteSpace(cleaned) ? "media" : cleaned;
    }

    internal static string EnsureFileExtension(string fileName, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            return fileName;
        }

        var ext = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
        return fileName + ext;
    }

    internal static void ValidateSize(string type, int length)
    {
        var t = (type ?? string.Empty).Trim().ToLowerInvariant();
        var maxBytes = t switch
        {
            "thumb" => 64 * 1024,
            "voice" => 2 * 1024 * 1024,
            "video" => 10 * 1024 * 1024,
            "image" => 10 * 1024 * 1024,
            _ => 10 * 1024 * 1024
        };

        if (length > maxBytes)
        {
            throw new InvalidOperationException($"wechat addMaterial file too large: {length} bytes (max {maxBytes}) type={t}");
        }
    }

    // ── Access Token ────────────────────────────────────────────────────

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && _tokenExpireAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return _cachedAccessToken!;
            }

            var endpoint =
                $"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={Uri.EscapeDataString(_appId)}&secret={Uri.EscapeDataString(_appSecret)}";
            using var resp = await _httpClient.GetAsync(endpoint, cancellationToken);
            var text = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                throw new WechatHttpException("wechat token", (int)resp.StatusCode, text);
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            ThrowIfWechatErr(root, "wechat token", text);

            if (!root.TryGetProperty("access_token", out var tokenEl) || tokenEl.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("wechat token response missing access_token.");
            }

            var token = tokenEl.GetString();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("wechat token response access_token empty.");
            }

            var expiresIn = root.TryGetProperty("expires_in", out var expiresEl) && expiresEl.ValueKind == JsonValueKind.Number
                ? expiresEl.GetInt32()
                : 7200;

            _cachedAccessToken = token;
            _tokenExpireAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(300, expiresIn));
            _logger.Info("plugin wechat-sync token refreshed");
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task InvalidateTokenAsync()
    {
        await _tokenLock.WaitAsync();
        try
        {
            _cachedAccessToken = null;
            _tokenExpireAt = DateTimeOffset.MinValue;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static bool IsTokenInvalid(int errcode)
    {
        return errcode is 40001 or 40014 or 42001;
    }

    // ── Error helpers ───────────────────────────────────────────────────

    private static void ThrowIfWechatErr(JsonElement root, string operation, string rawText)
    {
        if (root.TryGetProperty("errcode", out var codeEl) && codeEl.ValueKind == JsonValueKind.Number)
        {
            if (!codeEl.TryGetInt32(out var code))
            {
                return;
            }

            if (code == 0)
            {
                return;
            }

            var msg = root.TryGetProperty("errmsg", out var msgEl) && msgEl.ValueKind == JsonValueKind.String
                ? msgEl.GetString() ?? string.Empty
                : string.Empty;
            throw new WechatApiException(operation, code, msg, rawText);
        }
    }

    /// <summary>
    /// Default image download implementation using HttpClient.
    /// Used when no custom download function is provided.
    /// </summary>
    internal static async Task<byte[]> DefaultDownloadImageAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"wechat thumb url must be absolute: {url}");
        }

        using var httpClient = new HttpClient(new SocketsHttpHandler
        {
            ConnectCallback = SsrfGuard.SsrfSafeConnectAsync
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        using var resp = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (resp.Content.Headers.ContentLength is long contentLength &&
            contentLength > MaxDownloadedImageBytes)
        {
            throw new InvalidOperationException(
                $"wechat thumb download too large imageUrl={url} size={contentLength} max={MaxDownloadedImageBytes}");
        }

        var bytes = await ReadContentWithLimitAsync(resp.Content, MaxDownloadedImageBytes, url, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = SanitizeErrorResponseBody(bytes);
            throw new WechatHttpException($"wechat thumb download url={url}", (int)resp.StatusCode, detail);
        }

        if (bytes.Length == 0)
        {
            throw new InvalidOperationException($"wechat thumb download returned empty body imageUrl={url}");
        }

        return bytes;
    }

    internal static async Task<byte[]> ReadContentWithLimitAsync(
        HttpContent content,
        int maxBytes,
        string url,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(capacity: Math.Min(maxBytes, 81920));
        var chunk = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidOperationException(
                    $"wechat thumb download too large imageUrl={url} max={maxBytes}");
            }

            buffer.Write(chunk, 0, read);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _tokenLock.Dispose();
    }
}

// ── Publish request models ─────────────────────────────────────────────

internal sealed record WechatPublishSubmitRequest(
    [property: JsonPropertyName("media_id")] string MediaId);

internal sealed record WechatPublishStatusRequest(
    [property: JsonPropertyName("publish_id")] string PublishId);

// ── API request/article models ──────────────────────────────────────────

internal sealed record WechatDraftAddRequest(
    [property: JsonPropertyName("articles")] WechatDraftArticle[] Articles);

internal sealed record WechatDraftArticle(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("content_source_url")] string ContentSourceUrl,
    [property: JsonPropertyName("thumb_media_id")] string ThumbMediaId,
    [property: JsonPropertyName("need_open_comment")] int NeedOpenComment,
    [property: JsonPropertyName("only_fans_can_comment")] int OnlyFansCanComment);

// ── Publish status result ───────────────────────────────────────────────

/// <summary>
/// Result of a publish status check.
/// <c>PublishStatus</c>: 0 = success, 1 = publishing, 2+ = failure.
/// </summary>
public sealed record WechatPublishStatusResult(
    string PublishId,
    int PublishStatus,
    string? ArticleUrl);

// ── Structured WeChat API exception ─────────────────────────────────────

public sealed class WechatApiException : Exception
{
    public WechatApiException(string operation, int errCode, string errMsg, string rawText)
        : base(FormatMessage(operation, errCode, errMsg, rawText))
    {
        Operation = operation;
        ErrCode = errCode;
        ErrMsg = errMsg;
        RawText = rawText;
    }

    public string Operation { get; }
    public int ErrCode { get; }
    public string ErrMsg { get; }
    public string RawText { get; }

    private static string FormatMessage(string operation, int errCode, string errMsg, string rawText)
    {
        var sb = new StringBuilder();
        sb.Append(operation);
        sb.Append(" failed: errcode=");
        sb.Append(errCode);
        if (!string.IsNullOrWhiteSpace(errMsg))
        {
            sb.Append(" errmsg=");
            sb.Append(errMsg.Trim());
        }

        if (!string.IsNullOrWhiteSpace(rawText))
        {
            sb.Append(" raw=");
            sb.Append(rawText.Trim());
        }

        return sb.ToString();
    }
}

// ── Structured WeChat HTTP exception ────────────────────────────────────

/// <summary>
/// Thrown when a WeChat API call returns a non-success HTTP status code.
/// </summary>
public sealed class WechatHttpException : InvalidOperationException
{
    public WechatHttpException(string operation, int statusCode, string responseBody)
        : base($"{operation} http {statusCode}: {responseBody}")
    {
        Operation = operation;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string Operation { get; }
    public int StatusCode { get; }
    public string ResponseBody { get; }
}
