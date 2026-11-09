using Bukit.Engine.Abstractions.Routing;
using System.Collections.Concurrent;
using System.Text;

namespace Bukit.Routing;

public static class RoutePathBuilder
{
    public static string NormalizeUrl(string url)
        => NormalizeUrl(url, IsFileUrl(url));

    internal static string NormalizeUrl(string url, bool isFileRoute)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (!isFileRoute && !trimmed.EndsWith('/'))
        {
            trimmed += "/";
        }

        return trimmed;
    }

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // Decodes a URL field that was pasted already percent-encoded (e.g. /%e8%81%94/ -> /联/),
    // so the output folder matches what browsers request. Decodes one layer only, and only when
    // every escape forms valid UTF-8 and none of them encodes a path or control character.
    public static string DecodePercentEncodedUrl(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.Contains('%', StringComparison.Ordinal))
        {
            return url;
        }

        var bytes = new List<byte>(url.Length);
        for (var i = 0; i < url.Length; i++)
        {
            var c = url[i];
            if (c == '%')
            {
                if (i + 2 >= url.Length || !Uri.IsHexDigit(url[i + 1]) || !Uri.IsHexDigit(url[i + 2]))
                {
                    return url; // stray '%': not an encoded URL
                }

                var b = Convert.ToByte(url.Substring(i + 1, 2), 16);
                // Never let an escape change the path: / \ . % and control characters stay encoded.
                if (b < 0x20 || b == 0x7F || b == (byte)'/' || b == (byte)'\\' || b == (byte)'.' || b == (byte)'%')
                {
                    return url;
                }

                bytes.Add(b);
                i += 2;
            }
            else
            {
                var len = char.IsHighSurrogate(c) && i + 1 < url.Length ? 2 : 1;
                bytes.AddRange(Encoding.UTF8.GetBytes(url.Substring(i, len)));
                i += len - 1;
            }
        }

        string decoded;
        try
        {
            decoded = StrictUtf8.GetString([.. bytes]);
        }
        catch (DecoderFallbackException)
        {
            return url;
        }

        // Invisible or look-alike characters (zero-width, bidi overrides, NBSP...) would make
        // folder names that look identical but differ, so such URLs stay encoded.
        return decoded.Any(IsInvisibleOrSpecialSpace) ? url : decoded;
    }

    private static bool IsInvisibleOrSpecialSpace(char ch)
    {
        if (ch == ' ')
        {
            return false;
        }

        var category = char.GetUnicodeCategory(ch);
        return char.IsWhiteSpace(ch) ||
            category is System.Globalization.UnicodeCategory.Format
                or System.Globalization.UnicodeCategory.Control
                or System.Globalization.UnicodeCategory.PrivateUse
                or System.Globalization.UnicodeCategory.OtherNotAssigned;
    }

    public static string NormalizeListRoute(string route)
    {
        var normalized = NormalizeUrl(route, isFileRoute: false);
        return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
    }

    // Extensions that make a route a file. Any other dotted segment (/docs/v1.0) stays a directory.
    private static readonly HashSet<string> WebFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".xml", ".json", ".txt"
    };

    private static bool IsFileUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (trimmed.EndsWith('/'))
        {
            return false;
        }

        var lastSlash = trimmed.LastIndexOf('/');
        var lastSegment = lastSlash >= 0 ? trimmed[(lastSlash + 1)..] : trimmed;
        return IsWebFileSegment(lastSegment);
    }

    internal static bool IsWebFileSegment(string segment)
    {
        var dot = segment.LastIndexOf('.');
        return dot > 0 && WebFileExtensions.Contains(segment[dot..]);
    }

    // Files that templates cannot produce; they belong in static/ or assets/, not in a page route.
    private static readonly HashSet<string> StaticAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".js", ".mjs", ".map", ".wasm",
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".avif", ".ico", ".bmp",
        ".woff", ".woff2", ".ttf", ".otf", ".eot",
        ".pdf", ".zip", ".gz", ".tgz", ".tar", ".7z", ".rar",
        ".mp3", ".mp4", ".webm", ".ogg", ".wav", ".m4a", ".mov",
        ".csv", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
    };

    /// <summary>
    /// Returns the static-asset extension (for example ".pdf") when the URL or pattern ends in one
    /// without a trailing slash; otherwise null. A trailing slash marks an intentional page directory.
    /// </summary>
    internal static string? GetStaticAssetExtension(string urlOrPattern)
    {
        var trimmed = (urlOrPattern ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.EndsWith('/'))
        {
            return null;
        }

        var lastSlash = trimmed.LastIndexOf('/');
        var lastSegment = lastSlash >= 0 ? trimmed[(lastSlash + 1)..] : trimmed;
        var dot = lastSegment.LastIndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var extension = lastSegment[dot..];
        return StaticAssetExtensions.Contains(extension) ? extension : null;
    }

    public static string BuildOutputPathFromUrl(string url, string outputPathEncoding = "none")
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            RouteSecurityValidator.ValidateInternalUrl(url, "route URL");
        }

        var normalizedUrl = NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(normalizedUrl) || normalizedUrl == "/")
        {
            return NormalizeOutputPath("index.html", outputPathEncoding);
        }

        var outputPath = normalizedUrl.TrimStart('/');
        // File-like routes (/page.html) keep their extension; directory routes get index.html.
        if (outputPath.EndsWith('/'))
        {
            outputPath += "index.html";
        }
        else if (!Path.HasExtension(outputPath))
        {
            outputPath = outputPath.TrimEnd('/') + "/index.html";
        }

        return NormalizeOutputPath(outputPath, outputPathEncoding);
    }

    public static string NormalizeOutputPath(string outputPath, string outputPathEncoding = "none")
    {
        var trimmed = (outputPath ?? string.Empty).Trim().TrimStart('/', '\\');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var normalized = trimmed.Replace('\\', '/');
        return ApplyOutputPathEncoding(normalized, outputPathEncoding);
    }

    private static string ApplyOutputPathEncoding(string outputPath, string outputPathEncoding)
    {
        var mode = NormalizeEncoding(outputPathEncoding);
        if (mode == "none")
        {
            return outputPath;
        }

        var parts = outputPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => mode switch
            {
                "urlencode" => Uri.EscapeDataString(p),
                "slug" => SlugifySegment(p),
                "sanitize" => SanitizeSegment(p),
                _ => p
            })
            .Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join("/", parts);
    }

    private static string NormalizeEncoding(string? encoding)
    {
        return string.IsNullOrWhiteSpace(encoding) ? "none" : encoding.Trim().ToLowerInvariant();
    }

    private static string SanitizeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "page";
        }

        var sb = new StringBuilder(segment.Length);
        foreach (var ch in segment)
        {
            if (ch < 32)
            {
                continue;
            }

            if (ch == ' ')
            {
                sb.Append('-');
                continue;
            }

            if (ch is '<' or '>' or ':' or '"' or '|' or '?' or '*')
            {
                continue;
            }

            sb.Append(ch);
        }

        var cleaned = CompressDashes(sb.ToString());
        cleaned = cleaned.TrimEnd(' ', '.');
        return string.IsNullOrWhiteSpace(cleaned) ? "page" : cleaned;
    }

    private static string SlugifySegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "page";
        }

        var leadDot = segment.StartsWith('.') ? "." : string.Empty;
        var core = segment.TrimStart('.');
        if (string.IsNullOrWhiteSpace(core))
        {
            return segment;
        }

        var name = core;
        var extension = string.Empty;
        var dot = core.LastIndexOf('.');
        if (dot > 0 && dot < core.Length - 1)
        {
            name = core[..dot];
            extension = core[(dot + 1)..];
        }

        var slug = Slugify(name);
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "page";
        }

        return string.IsNullOrWhiteSpace(extension)
            ? $"{leadDot}{slug}"
            : $"{leadDot}{slug}.{extension}";
    }

    private static readonly ConcurrentDictionary<string, string> SlugCache = new(StringComparer.Ordinal);

    internal static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return SlugCache.GetOrAdd(text, static t => SlugifyCore(t));
    }

    private static string SlugifyCore(string text)
    {
        var sb = new StringBuilder();
        var lastDash = false;
        foreach (var ch in text.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastDash = false;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
            {
                if (!lastDash && sb.Length > 0)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }
        }

        return sb.ToString().Trim('-');
    }

    private static string CompressDashes(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        var lastDash = false;
        foreach (var ch in text)
        {
            if (ch == '-')
            {
                if (lastDash)
                {
                    continue;
                }

                lastDash = true;
                sb.Append(ch);
                continue;
            }

            lastDash = false;
            sb.Append(ch);
        }

        return sb.ToString();
    }
}
