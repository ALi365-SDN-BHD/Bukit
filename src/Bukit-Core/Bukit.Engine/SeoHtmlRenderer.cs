using System.Net;
using System.Text;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static class SeoHtmlRenderer
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
            var rel = GetAttribute(tag, "rel");
            return string.Equals(rel, "canonical", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rel, "prev", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rel, "next", StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(rel, "alternate", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(GetAttribute(tag, "hreflang")));
        }

        if (HtmlHeadScanner.IsStartTag(tag, "meta"))
        {
            var name = GetAttribute(tag, "name");
            var property = GetAttribute(tag, "property");
            return string.Equals(name, "description", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "robots", StringComparison.OrdinalIgnoreCase) ||
                   StartsWithToken(name, "twitter:") ||
                   StartsWithToken(property, "og:") ||
                   StartsWithToken(property, "article:");
        }

        if (HtmlHeadScanner.IsStartTag(tag, "script"))
        {
            var type = GetAttribute(tag, "type");
            return string.Equals(type, "application/ld+json", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string? GetAttribute(string tag, string name)
    {
        var index = 1;
        while (index < tag.Length)
        {
            while (index < tag.Length && !char.IsWhiteSpace(tag[index]) && tag[index] != '>')
            {
                index++;
            }

            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            if (index >= tag.Length || tag[index] is '>' or '/')
            {
                return null;
            }

            var attrStart = index;
            while (index < tag.Length && (char.IsLetterOrDigit(tag[index]) || tag[index] is '-' or '_' or ':'))
            {
                index++;
            }

            if (attrStart == index)
            {
                index++;
                continue;
            }

            var attrName = tag[attrStart..index];
            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            if (index >= tag.Length || tag[index] != '=')
            {
                if (string.Equals(attrName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                continue;
            }

            index++;
            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            string value;
            if (index < tag.Length && tag[index] is '"' or '\'')
            {
                var quote = tag[index++];
                var valueStart = index;
                while (index < tag.Length && tag[index] != quote)
                {
                    index++;
                }

                value = tag[valueStart..Math.Min(index, tag.Length)];
                if (index < tag.Length)
                {
                    index++;
                }
            }
            else
            {
                var valueStart = index;
                while (index < tag.Length && !char.IsWhiteSpace(tag[index]) && tag[index] != '>')
                {
                    index++;
                }

                value = tag[valueStart..index];
            }

            if (string.Equals(attrName, name, StringComparison.OrdinalIgnoreCase))
            {
                return WebUtility.HtmlDecode(value);
            }
        }

        return null;
    }

    private static bool StartsWithToken(string? value, string prefix)
        => !string.IsNullOrWhiteSpace(value) &&
           value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
