using System.Text;

namespace Bukit.Routing;

public static class RoutePathBuilder
{
    public static string NormalizeUrl(string url)
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

        if (!trimmed.EndsWith('/'))
        {
            trimmed += "/";
        }

        return trimmed;
    }

    public static string NormalizeListRoute(string route)
    {
        var normalized = NormalizeUrl(route);
        return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
    }

    public static string BuildOutputPathFromUrl(string url, string outputPathEncoding = "none")
    {
        var normalizedUrl = NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(normalizedUrl) || normalizedUrl == "/")
        {
            return NormalizeOutputPath("index.html", outputPathEncoding);
        }

        var outputPath = normalizedUrl.TrimStart('/');
        if (outputPath.EndsWith('/'))
        {
            outputPath += "index.html";
        }
        else if (!Path.HasExtension(outputPath))
        {
            outputPath = outputPath.TrimEnd('/') + "/index.html";
        }

        outputPath = outputPath.Replace('/', Path.DirectorySeparatorChar);
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

    internal static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

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
