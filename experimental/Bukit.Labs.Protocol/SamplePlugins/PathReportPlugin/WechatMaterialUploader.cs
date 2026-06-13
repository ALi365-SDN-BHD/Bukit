using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Bukit.Plugins.PathReportPlugin;

public sealed class WechatMaterialUploader : IDisposable
{
    private readonly HttpClient _httpClient;
    private string? _cachedAccessToken;
    private DateTimeOffset _tokenExpireAt;

    public WechatMaterialUploader()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
    {
    }

    public WechatMaterialUploader(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _tokenExpireAt = DateTimeOffset.MinValue;
    }

    public async Task<WechatMaterialUploadResponse> UploadPermanentMaterialAsync(
        string appId,
        string appSecret,
        string type,
        byte[] bytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            type = "image";
        }

        var token = await GetAccessTokenAsync(appId, appSecret, cancellationToken);
        try
        {
            return await AddMaterialAsync(token, type.Trim(), bytes, fileName, contentType, cancellationToken);
        }
        catch (WechatApiException ex) when (IsTokenInvalid(ex.ErrCode))
        {
            _cachedAccessToken = null;
            _tokenExpireAt = DateTimeOffset.MinValue;
            token = await GetAccessTokenAsync(appId, appSecret, cancellationToken);
            return await AddMaterialAsync(token, type.Trim(), bytes, fileName, contentType, cancellationToken);
        }
    }

    private async Task<string> GetAccessTokenAsync(string appId, string appSecret, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && _tokenExpireAt > now.AddMinutes(1))
        {
            return _cachedAccessToken!;
        }

        var endpoint =
            $"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={Uri.EscapeDataString(appId)}&secret={Uri.EscapeDataString(appSecret)}";

        using var resp = await _httpClient.GetAsync(endpoint, cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"wechat token http {(int)resp.StatusCode}: {text}");
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
            throw new InvalidOperationException("wechat token response access_token is empty.");
        }

        var expiresIn = 7200;
        if (root.TryGetProperty("expires_in", out var expEl) && expEl.ValueKind == JsonValueKind.Number)
        {
            expEl.TryGetInt32(out expiresIn);
        }

        _cachedAccessToken = token.Trim();
        _tokenExpireAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(300, expiresIn));
        return _cachedAccessToken;
    }

    private async Task<WechatMaterialUploadResponse> AddMaterialAsync(
        string accessToken,
        string type,
        byte[] bytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (bytes is null || bytes.Length == 0)
        {
            throw new InvalidOperationException("wechat addMaterial bytes is empty.");
        }

        ValidateSize(type, bytes.Length);

        contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        fileName = SanitizeFileName(string.IsNullOrWhiteSpace(fileName) ? "media" : fileName.Trim());

        var endpoint =
            $"https://api.weixin.qq.com/cgi-bin/material/add_material?access_token={Uri.EscapeDataString(accessToken)}&type={Uri.EscapeDataString(type)}";

        var boundary = "----bukit-" + Guid.NewGuid().ToString("N");
        var multipartBytes = BuildMultipart(boundary, bytes, fileName, contentType);
        using var body = new ByteArrayContent(multipartBytes);
        body.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");

        using var resp = await _httpClient.PostAsync(endpoint, body, cancellationToken);
        var textResp = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"wechat addMaterial http {(int)resp.StatusCode}: {textResp}");
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

        string? url = null;
        if (root.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
        {
            url = urlEl.GetString()?.Trim();
        }

        return new WechatMaterialUploadResponse(mediaId.Trim(), url, textResp);
    }

    private static byte[] BuildMultipart(string boundary, byte[] bytes, string fileName, string contentType)
    {
        var header =
            $"--{boundary}\r\n" +
            $"Content-Disposition: form-data; name=\"media\"; filename=\"{fileName}\"\r\n" +
            $"Content-Type: {contentType}\r\n\r\n";
        var footer = $"\r\n--{boundary}--\r\n";

        var headerBytes = Encoding.UTF8.GetBytes(header);
        var footerBytes = Encoding.UTF8.GetBytes(footer);

        var result = new byte[headerBytes.Length + bytes.Length + footerBytes.Length];
        Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
        Buffer.BlockCopy(bytes, 0, result, headerBytes.Length, bytes.Length);
        Buffer.BlockCopy(footerBytes, 0, result, headerBytes.Length + bytes.Length, footerBytes.Length);
        return result;
    }

    private static string SanitizeFileName(string fileName)
    {
        var cleaned = fileName.Replace("\"", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        return string.IsNullOrWhiteSpace(cleaned) ? "media" : cleaned;
    }

    private static void ValidateSize(string type, int length)
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

    private static bool IsTokenInvalid(int errcode)
    {
        return errcode is 40001 or 40014 or 42001;
    }

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

    public void Dispose() => _httpClient.Dispose();
}

public sealed record WechatMaterialUploadResponse(string MediaId, string? Url, string Raw);

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
