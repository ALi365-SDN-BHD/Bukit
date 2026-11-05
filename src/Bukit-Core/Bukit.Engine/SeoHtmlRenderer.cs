using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static partial class SeoHtmlRenderer
{
    internal static string InjectIntoHead(string html, SeoModel? seo)
    {
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
        headContent = RemoveManagedSeoTags(headContent);
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

    private static string RemoveManagedSeoTags(string html)
    {
        var sb = new StringBuilder(html.Length);
        var index = 0;
        while (index < html.Length)
        {
            var tagStart = html.IndexOf('<', index);
            if (tagStart < 0)
            {
                sb.Append(html, index, html.Length - index);
                break;
            }

            sb.Append(html, index, tagStart - index);
            if (HtmlHeadScanner.IsCommentStart(html, tagStart))
            {
                var commentEnd = HtmlHeadScanner.FindCommentEnd(html, tagStart, html.Length);
                if (commentEnd < 0)
                {
                    sb.Append(html, tagStart, html.Length - tagStart);
                    break;
                }

                sb.Append(html, tagStart, commentEnd - tagStart);
                index = commentEnd;
                continue;
            }

            var tagEnd = HtmlHeadScanner.FindTagEnd(html, tagStart);
            if (tagEnd < 0)
            {
                sb.Append(html, tagStart, html.Length - tagStart);
                break;
            }

            var tag = html.Substring(tagStart, tagEnd - tagStart + 1);
            var blockEnd = tagEnd;
            var block = tag;
            var rawTextElement = HtmlHeadScanner.GetRawTextElementName(tag);
            if (rawTextElement is not null)
            {
                var elementClose = HtmlHeadScanner.FindClosingElementEnd(
                    html,
                    tagEnd + 1,
                    html.Length,
                    rawTextElement);
                if (elementClose >= 0)
                {
                    blockEnd = elementClose;
                    block = html.Substring(tagStart, blockEnd - tagStart + 1);
                }
            }

            if (!IsManagedTag(tag, block))
            {
                sb.Append(block);
            }

            index = blockEnd + 1;
        }

        return sb.ToString();
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

    private static bool IsManagedTag(string tag, string block)
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

        if (HtmlHeadScanner.IsStartTag(tag, "script"))
        {
            var type = HtmlTagAttributeReader.GetAttribute(tag, "type");
            return string.Equals(type, "application/ld+json", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool StartsWithToken(string? value, string prefix)
        => !string.IsNullOrWhiteSpace(value) &&
           value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
