using System.Text;
using Bukit.Content;

namespace Bukit.Routing;

public static class RouteGenerator
{
    public sealed record CollectionRouteRule(string Permalink, string Template);

    public static RouteInfo Generate(
        ContentItem item,
        string outputPathEncoding = "none",
        IReadOnlyDictionary<string, string>? permalinks = null,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections = null)
    {
        if (TryReadRouteOverride(item, outputPathEncoding, out var overridden))
        {
            return overridden;
        }

        var collectionKey = GetCollection(item);

        if (collections is not null && collections.TryGetValue(collectionKey, out var rule))
        {
            return BuildFromPattern(item, rule.Permalink, rule.Template, outputPathEncoding);
        }

        var type = GetType(item);

        if (permalinks is not null && permalinks.TryGetValue(type, out var pattern) && !string.IsNullOrWhiteSpace(pattern))
        {
            var template = type.Equals("post", StringComparison.OrdinalIgnoreCase) ? "pages/post.html" : "pages/page.html";
            return BuildFromPattern(item, pattern, template, outputPathEncoding);
        }

        var route = type switch
        {
            "post" => new RouteInfo(
                Url: $"/blog/{item.Slug}/",
                OutputPath: Path.Combine("blog", item.Slug, "index.html"),
                Template: "pages/post.html"
            ),
            "page" => new RouteInfo(
                Url: $"/pages/{item.Slug}/",
                OutputPath: Path.Combine("pages", item.Slug, "index.html"),
                Template: "pages/page.html"
            ),
            _ => new RouteInfo(
                Url: $"/pages/{item.Slug}/",
                OutputPath: Path.Combine("pages", item.Slug, "index.html"),
                Template: "pages/page.html"
            )
        };

        return route with
        {
            OutputPath = NormalizeOutputPath(route.OutputPath, outputPathEncoding)
        };
    }

    private static RouteInfo BuildFromPattern(ContentItem item, string pattern, string template, string outputPathEncoding)
    {
        var url = ExpandPermalinkPattern(pattern, item);
        url = NormalizeUrl(url);

        var outputPath = url.TrimStart('/');
        if (outputPath.EndsWith('/'))
        {
            outputPath += "index.html";
        }
        else if (!Path.HasExtension(outputPath))
        {
            outputPath = outputPath.TrimEnd('/') + "/index.html";
        }

        outputPath = outputPath.Replace('/', Path.DirectorySeparatorChar);
        outputPath = NormalizeOutputPath(outputPath, outputPathEncoding);

        return new RouteInfo(url, outputPath, template);
    }

    public static string ExpandPermalinkPattern(string pattern, ContentItem item)
    {
        var result = pattern;
        result = result.Replace("{slug}", item.Slug, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{title}", Slugify(item.Title), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{year}", item.PublishAt.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{month}", item.PublishAt.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{day}", item.PublishAt.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase);

        var typeVal = item.Meta.TryGetValue("type", out var t) && t is not null ? (t.ToString() ?? "page") : "page";
        result = result.Replace("{type}", typeVal, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    private static bool TryReadRouteOverride(ContentItem item, string outputPathEncoding, out RouteInfo route)
    {
        if (TryGetRouteFields(item.Meta, out var url, out var outputPath, out var template))
        {
            url = NormalizeUrl(url);
            outputPath = NormalizeOutputPath(outputPath, outputPathEncoding);
            template = template.Trim();

            if (!string.IsNullOrWhiteSpace(url) &&
                !string.IsNullOrWhiteSpace(outputPath) &&
                !string.IsNullOrWhiteSpace(template))
            {
                route = new RouteInfo(url, outputPath, template);
                return true;
            }
        }

        route = default!;
        return false;
    }

    private static bool TryGetRouteFields(
        IReadOnlyDictionary<string, object> meta,
        out string url,
        out string outputPath,
        out string template)
    {
        url = string.Empty;
        outputPath = string.Empty;
        template = string.Empty;

        if (meta.TryGetValue("route", out var routeObj) && routeObj is IReadOnlyDictionary<string, object> routeMap)
        {
            url = GetOptionalString(routeMap, "url");
            outputPath = GetOptionalString(routeMap, "outputPath");
            template = GetOptionalString(routeMap, "template");
            return !(string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(template));
        }

        if (meta.TryGetValue("url", out var u) && u is string us) url = us;
        if (meta.TryGetValue("outputPath", out var o) && o is string os) outputPath = os;
        if (meta.TryGetValue("template", out var t) && t is string ts) template = ts;

        return !(string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(template));
    }

    private static string GetOptionalString(IReadOnlyDictionary<string, object> map, string key)
    {
        return map.TryGetValue(key, out var v) && v is string s ? s : string.Empty;
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim();
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

    private static string NormalizeOutputPath(string outputPath, string outputPathEncoding)
    {
        var trimmed = outputPath.Trim().TrimStart('/', '\\');
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
                "urlencode" => UrlEncodeSegment(p),
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

    private static string UrlEncodeSegment(string segment)
    {
        return Uri.EscapeDataString(segment);
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

            if (IsWindowsInvalidChar(ch))
            {
                continue;
            }

            sb.Append(ch);
        }

        var cleaned = CompressDashes(sb.ToString());
        cleaned = cleaned.TrimEnd(' ', '.');

        return string.IsNullOrWhiteSpace(cleaned) ? "page" : cleaned;
    }

    private static bool IsWindowsInvalidChar(char ch)
    {
        return ch is '<' or '>' or ':' or '"' or '|' or '?' or '*';
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

    private static string Slugify(string text)
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

    private static string GetType(ContentItem item)
    {
        if (item.Meta.TryGetValue("type", out var v) && v is not null)
        {
            return v.ToString() ?? "page";
        }

        return "page";
    }

    private static string GetCollection(ContentItem item)
    {
        if (item.Meta.TryGetValue("collection", out var collection) && collection is not null && !string.IsNullOrWhiteSpace(collection.ToString()))
        {
            return collection.ToString()!;
        }

        return GetType(item);
    }
}
