using System.Buffers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Engine.Primitives;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static partial class SeoHtmlRenderer
{
    internal static string InjectIntoHead(string html, SeoModel? seo)
        => InjectIntoHead(html, seo, out _);

    /// <summary>
    /// Replaces the Core-managed head tags with the tags rendered from <paramref name="seo"/>.
    /// Head JSON-LD marked <c>data-bukit-seo="keep"</c> stays where the theme put it; other head
    /// JSON-LD is removed. <paramref name="replacedThemeJsonLd"/> counts removed scripts whose content
    /// differs from every Core-generated JSON-LD block, so callers can warn that theme data was dropped.
    /// </summary>
    internal static string InjectIntoHead(string html, SeoModel? seo, out int replacedThemeJsonLd)
    {
        replacedThemeJsonLd = 0;
        if (seo is null || string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        if (!HtmlHeadScanner.TryFindHead(html, out var head))
        {
            return html;
        }

        var beforeHeadContent = html[..head.ContentStart];
        var headContent = html[head.ContentStart..head.ContentEnd];
        var afterHeadContent = html[head.ContentEnd..];
        headContent = RemoveManagedSeoTags(headContent, seo.JsonLd, out replacedThemeJsonLd);
        var seoHtml = RenderHead(seo);
        return beforeHeadContent + headContent.TrimEnd() + Environment.NewLine + seoHtml + afterHeadContent;
    }

    /// <summary>
    /// Adds <c>rel="alternate"</c> links to the page's Markdown and JSON versions so AI agents
    /// can find them from the HTML. A link the theme already declares, as a root-relative URL or
    /// as the absolute URL under <paramref name="siteUrl"/>, is not repeated.
    /// </summary>
    internal static string InjectRepresentationLinks(string html, string markdownUrl, string jsonUrl, string? siteUrl = null)
    {
        if (string.IsNullOrWhiteSpace(html) || !HtmlHeadScanner.TryFindHead(html, out var head))
        {
            return html;
        }

        var headContent = html[head.ContentStart..head.ContentEnd];
        var declared = LinkHrefRegex().Matches(headContent)
            .Select(match => NormalizeHref(WebUtility.HtmlDecode(match.Groups["href"].Value)))
            .ToHashSet(StringComparer.Ordinal);
        var links = new StringBuilder();
        if (!IsDeclared(declared, markdownUrl, siteUrl))
        {
            links.Append($"  <link rel=\"alternate\" type=\"text/markdown\" href=\"{Attr(markdownUrl)}\" />{Environment.NewLine}");
        }

        if (!IsDeclared(declared, jsonUrl, siteUrl))
        {
            links.Append($"  <link rel=\"alternate\" type=\"application/json\" href=\"{Attr(jsonUrl)}\" />{Environment.NewLine}");
        }

        return links.Length == 0
            ? html
            : html[..head.ContentStart] + headContent.TrimEnd() + Environment.NewLine + links + html[head.ContentEnd..];
    }

    private static bool IsDeclared(HashSet<string> declared, string url, string? siteUrl)
        => declared.Contains(NormalizeHref(url)) ||
           (!string.IsNullOrWhiteSpace(siteUrl) && declared.Contains(NormalizeHref(siteUrl.Trim().TrimEnd('/') + url)));

    // Absolute http(s) URLs compare with a lower-case origin and no default port; paths keep their case.
    private static string NormalizeHref(string href)
    {
        var trimmed = href.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var origin = uri.IsDefaultPort ? $"{uri.Scheme}://{uri.IdnHost}" : $"{uri.Scheme}://{uri.IdnHost}:{uri.Port}";
            return origin + uri.PathAndQuery;
        }

        return trimmed;
    }

    [GeneratedRegex(@"<link\b[^>]*?\shref\s*=\s*(?:""(?<href>[^""]*)""|'(?<href>[^']*)'|(?<href>[^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkHrefRegex();

    internal static string RenderHead(SeoModel seo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  <title>{Attr(SeoDocumentTitleResolver.ResolveEffective(seo))}</title>");
        sb.AppendLine($"  <link rel=\"canonical\" href=\"{Attr(seo.Canonical)}\" />");
        if (!string.IsNullOrWhiteSpace(seo.Prev))
        {
            sb.AppendLine($"  <link rel=\"prev\" href=\"{Attr(seo.Prev!)}\" />");
        }

        if (!string.IsNullOrWhiteSpace(seo.Next))
        {
            sb.AppendLine($"  <link rel=\"next\" href=\"{Attr(seo.Next!)}\" />");
        }

        if (!string.IsNullOrWhiteSpace(seo.Description))
        {
            sb.AppendLine($"  <meta name=\"description\" content=\"{Attr(seo.Description!)}\" />");
        }

        if (!string.IsNullOrWhiteSpace(seo.Robots))
        {
            sb.AppendLine($"  <meta name=\"robots\" content=\"{Attr(seo.Robots!)}\" />");
        }

        WriteMetaProperty(sb, "og:title", seo.Og.Title);
        WriteMetaProperty(sb, "og:description", seo.Og.Description);
        WriteMetaProperty(sb, "og:url", seo.Og.Url);
        WriteMetaProperty(sb, "og:type", seo.Og.Type);
        WriteMetaProperty(sb, "og:image", seo.Og.Image);
        WriteMetaProperty(sb, "og:site_name", seo.Og.SiteName);
        WriteMetaProperty(sb, "og:locale", seo.Og.Locale);

        if (seo.Article.PublishedTime is { } published)
        {
            WriteMetaProperty(sb, "article:published_time", published.ToString("O"));
        }

        if (seo.Article.ModifiedTime is { } modified)
        {
            WriteMetaProperty(sb, "article:modified_time", modified.ToString("O"));
        }

        WriteMetaProperty(sb, "article:author", seo.Article.Author);
        foreach (var tag in seo.Article.Tags)
        {
            WriteMetaProperty(sb, "article:tag", tag);
        }

        WriteMetaName(sb, "twitter:card", seo.Twitter.Card);
        WriteMetaName(sb, "twitter:title", seo.Twitter.Title);
        WriteMetaName(sb, "twitter:description", seo.Twitter.Description);
        WriteMetaName(sb, "twitter:image", seo.Twitter.Image);
        WriteMetaName(sb, "twitter:site", seo.Twitter.Site);
        WriteMetaName(sb, "twitter:creator", seo.Twitter.Creator);

        foreach (var alternate in seo.Alternates)
        {
            sb.AppendLine($"  <link rel=\"alternate\" hreflang=\"{Attr(alternate.Hreflang)}\" href=\"{Attr(alternate.Href)}\" />");
        }

        foreach (var json in seo.JsonLd)
        {
            sb.AppendLine($"  <script type=\"application/ld+json\">{json}</script>");
        }

        return sb.ToString();
    }

    private static string RemoveManagedSeoTags(string html, IReadOnlyList<string> coreJsonLd, out int replacedThemeJsonLd)
    {
        replacedThemeJsonLd = 0;
        var sb = new StringBuilder(html.Length);
        foreach (var segment in HtmlHeadSegments.Enumerate(html))
        {
            if (segment.Tag is not { } tag || !IsManagedTag(tag))
            {
                sb.Append(html, segment.Start, segment.Length);
                continue;
            }

            if (SeoJsonLdScripts.IsJsonLdScript(tag))
            {
                if (SeoJsonLdScripts.IsKept(tag))
                {
                    sb.Append(html, segment.Start, segment.Length);
                    continue;
                }

                // A theme that echoes seo.json_ld loses nothing when Core writes the same block.
                if (!IsCoreJsonLdEcho(SeoJsonLdScripts.GetContent(html, segment).Trim(), coreJsonLd))
                {
                    replacedThemeJsonLd++;
                }
            }
        }

        return sb.ToString();
    }

    // Whitespace and string escaping may differ (pretty-printed or reserialized echoes); property
    // order and values must match. Text that is not valid JSON never matches Core's block.
    private static bool IsCoreJsonLdEcho(string content, IReadOnlyList<string> coreJsonLd)
    {
        if (coreJsonLd.Any(json => string.Equals(json.Trim(), content, StringComparison.Ordinal)))
        {
            return true;
        }

        var normalized = NormalizeJson(content);
        return normalized is not null &&
               coreJsonLd.Any(json => string.Equals(NormalizeJson(json), normalized, StringComparison.Ordinal));
    }

    private static string? NormalizeJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                document.RootElement.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteMetaName(StringBuilder sb, string name, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            sb.AppendLine($"  <meta name=\"{Attr(name)}\" content=\"{Attr(content!)}\" />");
        }
    }

    private static void WriteMetaProperty(StringBuilder sb, string property, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            sb.AppendLine($"  <meta property=\"{Attr(property)}\" content=\"{Attr(content!)}\" />");
        }
    }

    private static string Attr(string value) => WebUtility.HtmlEncode(value);

    private static bool IsManagedTag(string tag)
    {
        if (HtmlHeadScanner.IsStartTag(tag, "title"))
        {
            return true;
        }

        if (HtmlHeadScanner.IsStartTag(tag, "link"))
        {
            var rel = HtmlTagAttributeReader.GetAttribute(tag, "rel");
            return string.Equals(rel, "canonical", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rel, "prev", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rel, "next", StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(rel, "alternate", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(HtmlTagAttributeReader.GetAttribute(tag, "hreflang")));
        }

        if (HtmlHeadScanner.IsStartTag(tag, "meta"))
        {
            var name = HtmlTagAttributeReader.GetAttribute(tag, "name");
            var property = HtmlTagAttributeReader.GetAttribute(tag, "property");
            return string.Equals(name, "description", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "robots", StringComparison.OrdinalIgnoreCase) ||
                   StartsWithToken(name, "twitter:") ||
                   StartsWithToken(property, "og:") ||
                   StartsWithToken(property, "article:");
        }

        return SeoJsonLdScripts.IsJsonLdScript(tag);
    }

    private static bool StartsWithToken(string? value, string prefix)
        => !string.IsNullOrWhiteSpace(value) &&
           value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
