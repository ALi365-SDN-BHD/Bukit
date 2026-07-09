using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Bukit.WechatSyncing;

/// <summary>
/// Pure utility methods shared across the wechat-sync plugin.
/// </summary>
internal static class WechatSyncHelpers
{
    // ── HTML ────────────────────────────────────────────────────────────

    internal static bool TryExtractFirstImageUrl(string html, out string url)
    {
        url = string.Empty;
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var idx = html.IndexOf("<img", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return false;
        }

        var end = html.IndexOf('>', idx);
        if (end < 0)
        {
            return false;
        }

        var tag = html.Substring(idx, end - idx + 1);

        if (TryReadHtmlAttribute(tag, "data-src", out url) ||
            TryReadHtmlAttribute(tag, "data-original", out url) ||
            TryReadHtmlAttribute(tag, "src", out url))
        {
            url = url.Trim();
            if (!string.IsNullOrWhiteSpace(url) && !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (TryReadHtmlAttribute(tag, "srcset", out var srcset))
        {
            var best = TryPickBestSrcsetUrl(srcset);
            if (!string.IsNullOrWhiteSpace(best))
            {
                url = best;
                return true;
            }
        }

        return false;
    }

    internal static bool TryReadHtmlAttribute(string tag, string name, out string value)
    {
        value = string.Empty;
        var i = 0;
        while (i < tag.Length)
        {
            i = tag.IndexOf(name, i, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
            {
                return false;
            }

            var afterName = i + name.Length;
            if (afterName >= tag.Length)
            {
                return false;
            }

            var prev = i > 0 ? tag[i - 1] : ' ';
            if (char.IsLetterOrDigit(prev) || prev == '_' || prev == '-')
            {
                i = afterName;
                continue;
            }

            var j = afterName;
            while (j < tag.Length && char.IsWhiteSpace(tag[j]))
            {
                j++;
            }

            if (j >= tag.Length || tag[j] != '=')
            {
                i = afterName;
                continue;
            }

            j++;
            while (j < tag.Length && char.IsWhiteSpace(tag[j]))
            {
                j++;
            }

            if (j >= tag.Length)
            {
                return false;
            }

            var quote = tag[j];
            if (quote == '"' || quote == '\'')
            {
                j++;
                var k = tag.IndexOf(quote, j);
                if (k < 0)
                {
                    return false;
                }

                value = tag.Substring(j, k - j);
                return true;
            }

            var endIdx = j;
            while (endIdx < tag.Length && !char.IsWhiteSpace(tag[endIdx]) && tag[endIdx] != '>')
            {
                endIdx++;
            }

            value = tag.Substring(j, endIdx - j);
            return true;
        }

        return false;
    }

    internal static string? TryPickBestSrcsetUrl(string srcset)
    {
        if (string.IsNullOrWhiteSpace(srcset))
        {
            return null;
        }

        var bestUrl = string.Empty;
        var bestScore = 0;
        var entries = srcset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var parts = entry.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            var u = parts[0];
            var score = 0;
            for (var k = 1; k < parts.Length; k++)
            {
                var p = parts[k];
                if (p.EndsWith('w') && int.TryParse(p.AsSpan(0, p.Length - 1), out var w))
                {
                    score = Math.Max(score, w);
                }
                else if (p.EndsWith('x') && int.TryParse(p.AsSpan(0, p.Length - 1), out var x))
                {
                    score = Math.Max(score, x * 1000);
                }
            }

            if (score >= bestScore)
            {
                bestScore = score;
                bestUrl = u;
            }
        }

        return string.IsNullOrWhiteSpace(bestUrl) ? null : bestUrl;
    }

    internal static string StripHtml(string html, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(html) || maxChars <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(Math.Min(maxChars, html.Length));
        var inside = false;
        for (var i = 0; i < html.Length && sb.Length < maxChars; i++)
        {
            var ch = html[i];
            if (ch == '<')
            {
                inside = true;
                continue;
            }

            if (ch == '>')
            {
                inside = false;
                if (sb.Length < maxChars)
                {
                    sb.Append(' ');
                }

                continue;
            }

            if (!inside)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().ReplaceLineEndings(" ").Trim();
    }

    // ── WeChat draft field limits ───────────────────────────────────────

    /// <summary>WeChat draft title limit (64 characters).</summary>
    internal const int WechatTitleMaxChars = 64;

    /// <summary>WeChat draft digest/description limit (120 characters).</summary>
    internal const int WechatDigestMaxChars = 120;

    /// <summary>WeChat draft content limit (20,000 characters).</summary>
    internal const int WechatContentMaxChars = 20_000;

    /// <summary>WeChat draft content size limit (1 MB).</summary>
    internal const int WechatContentMaxBytes = 1 * 1024 * 1024;

    /// <summary>
    /// Truncates a string to <paramref name="maxChars"/> characters.
    /// If the string is longer, it is cut and "..." is appended.
    /// </summary>
    internal static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxChars)
        {
            return text ?? string.Empty;
        }

        // Leave room for the ellipsis
        return maxChars > 3 ? text[..(maxChars - 3)] + "..." : text[..maxChars];
    }

    // ── URL ─────────────────────────────────────────────────────────────

    internal static bool IsHttpUrl(string text)
    {
        return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool LooksLikeUrl(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        if (IsHttpUrl(text) || text.StartsWith("//", StringComparison.Ordinal) || text.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        if (text.StartsWith("./", StringComparison.Ordinal) || text.StartsWith("../", StringComparison.Ordinal))
        {
            return true;
        }

        var lower = text.ToLowerInvariant();
        return lower.EndsWith(".png", StringComparison.Ordinal) ||
            lower.EndsWith(".jpg", StringComparison.Ordinal) ||
            lower.EndsWith(".jpeg", StringComparison.Ordinal) ||
            lower.EndsWith(".gif", StringComparison.Ordinal) ||
            lower.EndsWith(".webp", StringComparison.Ordinal) ||
            lower.EndsWith(".svg", StringComparison.Ordinal);
    }

    internal static bool TryNormalizeToAbsoluteUrl(string raw, string? siteUrl, string baseUrl, out string absolute)
    {
        absolute = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = WebUtility.HtmlDecode(raw.Trim());
        if (IsHttpUrl(raw))
        {
            absolute = raw;
            return true;
        }

        if (raw.StartsWith("//", StringComparison.Ordinal))
        {
            var scheme = TryGetScheme(siteUrl) ?? "https";
            absolute = $"{scheme}:{raw}";
            return true;
        }

        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var siteUri))
        {
            return false;
        }

        var normalizedBase = string.IsNullOrWhiteSpace(baseUrl) ? "/" : baseUrl;
        if (!normalizedBase.StartsWith('/'))
        {
            normalizedBase = "/" + normalizedBase;
        }

        var baseUri = new Uri($"{siteUri.Scheme}://{siteUri.Authority}{normalizedBase.TrimEnd('/')}/");
        if (!Uri.TryCreate(baseUri, raw, out var resolved))
        {
            return false;
        }

        absolute = resolved.ToString();
        return true;
    }

    internal static string? TryGetScheme(string? siteUrl)
    {
        if (Uri.TryCreate(siteUrl, UriKind.Absolute, out var u))
        {
            return u.Scheme;
        }

        return null;
    }

    internal static string CombineAbsoluteUrl(string? siteUrl, string baseUrl, string routeUrl)
    {
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            return string.Empty;
        }

        var site = siteUrl.Trim().TrimEnd('/');
        var normalizedBase = string.IsNullOrWhiteSpace(baseUrl) ? "/" : baseUrl.Trim();
        if (!normalizedBase.StartsWith('/'))
        {
            normalizedBase = "/" + normalizedBase;
        }

        if (normalizedBase != "/")
        {
            normalizedBase = normalizedBase.TrimEnd('/');
        }

        var route = string.IsNullOrWhiteSpace(routeUrl) ? "/" : routeUrl.Trim();
        route = route.StartsWith('/') ? route : "/" + route;

        if (normalizedBase != "/" &&
            (route.Equals(normalizedBase, StringComparison.OrdinalIgnoreCase) ||
             route.StartsWith(normalizedBase + "/", StringComparison.OrdinalIgnoreCase)))
        {
            return $"{site}{route}";
        }

        return normalizedBase == "/" ? $"{site}{route}" : $"{site}{normalizedBase}{route}";
    }

    internal static string ComputeUrlKey(string url)
    {
        var bytes = Encoding.UTF8.GetBytes(url);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    internal static string NormalizeMediaSourceUrlKey(string sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";

        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        path = path.Replace('\\', '/');
        while (path.Contains("//", StringComparison.Ordinal))
        {
            path = path.Replace("//", "/", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        return $"{scheme}://{host}{port}{path}";
    }

    internal static string ResolveMediaCacheExtension(string sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            return ".img";
        }

        var ext = Path.GetExtension(uri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 6)
        {
            return ext.ToLowerInvariant();
        }

        return ".img";
    }

    internal static string BuildMediaCacheStableFileName(string normalizedKey, string ext)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey));
        var shortHash = Convert.ToHexString(hash).ToLowerInvariant()[..16];
        return $"{shortHash}{ext}";
    }

    // ── File / Content-Type ─────────────────────────────────────────────

    internal static string GuessImageContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    /// <summary>
    /// Detects the actual image content type from file header magic bytes.
    /// Returns <c>null</c> when the format is unrecognized.
    /// </summary>
    internal static string? DetectImageContentType(byte[] bytes)
    {
        if (bytes is null || bytes.Length < 4)
        {
            return null;
        }

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        // GIF: 47 49 46 38
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
        {
            return "image/gif";
        }

        // WebP: RIFF....WEBP
        if (bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        // BMP: 42 4D
        if (bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return "image/bmp";
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when the content type is supported by the WeChat
    /// <c>add_material</c> API (<c>type=image</c>): only JPG and PNG.
    /// </summary>
    internal static bool IsWechatSupportedImage(string contentType)
        => contentType is "image/jpeg" or "image/png";

    /// <summary>
    /// Maps a content type to its conventional file extension.
    /// </summary>
    internal static string ContentTypeToExtension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "image/bmp" => ".bmp",
        _ => ".jpg"
    };

    internal static bool LooksLikeWindowsAbsolutePath(string path)
    {
        return path.Length >= 3 &&
            ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')) &&
            path[1] == ':' &&
            (path[2] == '\\' || path[2] == '/');
    }

    // ── Content field helpers ───────────────────────────────────────────

    internal static string? ReadFieldString(WechatSyncItem item, string key)
    {
        if (item.Fields is null || !item.Fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        var v = field.Value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    internal static string ReadMetaString(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        return value.ToString()?.Trim() ?? string.Empty;
    }

    internal static string? ReadFieldType(IReadOnlyDictionary<string, WechatSyncField>? fields)
    {
        if (fields is null || !fields.TryGetValue("type", out var field))
        {
            return null;
        }

        var value = field.Value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value.ToLowerInvariant();
    }

    // ── Environment ─────────────────────────────────────────────────────

    internal static bool ReadTrueFromEnv(string envName)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw.Trim();
        return raw is "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    internal static string ReadRequiredEnv(string envName)
    {
        var value = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"wechat-sync requires env var '{envName}'.");
        }

        return value.Trim();
    }

    // ── Config parsing ──────────────────────────────────────────────────

    internal static IReadOnlyDictionary<string, object>? ReadMap(IReadOnlyDictionary<string, object>? map, string key)
    {
        if (map is null || !map.TryGetValue(key, out var obj) || obj is null)
        {
            return null;
        }

        return obj as IReadOnlyDictionary<string, object>;
    }

    internal static IReadOnlyList<string>? ReadStringList(IReadOnlyDictionary<string, object>? map, string key)
    {
        if (map is null || !map.TryGetValue(key, out var obj) || obj is null)
        {
            return null;
        }

        if (obj is IReadOnlyList<object> list)
        {
            var values = list.Select(x => x?.ToString() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
            return values.Count == 0 ? null : values;
        }

        if (obj is string s)
        {
            var values = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return values.Length == 0 ? null : values;
        }

        return null;
    }

    internal static string? ReadString(IReadOnlyDictionary<string, object>? map, string key)
    {
        if (map is null || !map.TryGetValue(key, out var obj) || obj is null)
        {
            return null;
        }

        var s = obj.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    internal static int? ReadInt(IReadOnlyDictionary<string, object>? map, string key)
    {
        var s = ReadString(map, key);
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        return int.TryParse(s, out var i) ? i : null;
    }

    internal static bool? ReadBool(IReadOnlyDictionary<string, object>? map, string key)
    {
        var s = ReadString(map, key);
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        if (bool.TryParse(s, out var b))
        {
            return b;
        }

        return s.Equals("1", StringComparison.OrdinalIgnoreCase)
            ? true
            : s.Equals("0", StringComparison.OrdinalIgnoreCase)
                ? false
                : null;
    }

    internal static HashSet<string> ToNormalizedSet(IReadOnlyList<string>? values)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values is null)
        {
            return set;
        }

        foreach (var v in values)
        {
            var t = (v ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(t))
            {
                set.Add(t.ToLowerInvariant());
            }
        }

        return set;
    }

    // ── Retry ───────────────────────────────────────────────────────────

    internal static TimeSpan ComputeDelay(int baseDelayMs, int backoffFactor, int attempt)
    {
        long delay = baseDelayMs;
        for (var i = 1; i < attempt; i++)
        {
            delay = checked(delay * backoffFactor);
            if (delay > 120_000)
            {
                delay = 120_000;
                break;
            }
        }

        return TimeSpan.FromMilliseconds(delay);
    }
}
