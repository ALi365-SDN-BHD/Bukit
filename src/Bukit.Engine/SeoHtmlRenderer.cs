using System.Net;
using System.Text;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static class SeoHtmlRenderer
{
    internal static string InjectIntoHead(string html, SeoModel? seo, AnalyticsModel analytics)
    {
        if (seo is null || string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var headClose = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headClose < 0)
        {
            return html;
        }

        var beforeHeadClose = html[..headClose];
        var afterHeadClose = html[headClose..];
        beforeHeadClose = RemoveManagedSeoTags(beforeHeadClose);
        var seoHtml = RenderHead(seo, analytics);
        return beforeHeadClose.TrimEnd() + Environment.NewLine + seoHtml + afterHeadClose;
    }

    internal static string RenderHead(SeoModel seo, AnalyticsModel analytics)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  <link rel=\"canonical\" href=\"{Attr(seo.Canonical)}\" />");
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

        if (analytics.Enabled && !string.IsNullOrWhiteSpace(analytics.GoogleAnalyticsId))
        {
            var id = Attr(analytics.GoogleAnalyticsId!);
            sb.AppendLine($"  <script async src=\"https://www.googletagmanager.com/gtag/js?id={id}\"></script>");
            sb.AppendLine("  <script>");
            sb.AppendLine("    window.dataLayer = window.dataLayer || [];");
            sb.AppendLine("    function gtag(){dataLayer.push(arguments);}");
            sb.AppendLine("    gtag('js', new Date());");
            sb.AppendLine($"    gtag('config', '{id}');");
            sb.AppendLine("  </script>");
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
            var tagEnd = FindTagEnd(html, tagStart);
            if (tagEnd < 0)
            {
                sb.Append(html, tagStart, html.Length - tagStart);
                break;
            }

            var tag = html.Substring(tagStart, tagEnd - tagStart + 1);
            var blockEnd = tagEnd;
            var block = tag;
            if (IsStartTag(tag, "script"))
            {
                var scriptClose = FindClosingScriptEnd(html, tagEnd + 1);
                if (scriptClose >= 0)
                {
                    blockEnd = scriptClose;
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

    private static int FindTagEnd(string html, int tagStart)
    {
        var quote = '\0';
        for (var i = tagStart + 1; i < html.Length; i++)
        {
            var c = html[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c == '>')
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindClosingScriptEnd(string html, int searchStart)
    {
        var closeStart = html.IndexOf("</script", searchStart, StringComparison.OrdinalIgnoreCase);
        return closeStart < 0 ? -1 : FindTagEnd(html, closeStart);
    }

    private static bool IsManagedTag(string tag, string block)
    {
        if (IsStartTag(tag, "link"))
        {
            var rel = GetAttribute(tag, "rel");
            return string.Equals(rel, "canonical", StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(rel, "alternate", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(GetAttribute(tag, "hreflang")));
        }

        if (IsStartTag(tag, "meta"))
        {
            var name = GetAttribute(tag, "name");
            var property = GetAttribute(tag, "property");
            return string.Equals(name, "description", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "robots", StringComparison.OrdinalIgnoreCase) ||
                   StartsWithToken(name, "twitter:") ||
                   StartsWithToken(property, "og:") ||
                   StartsWithToken(property, "article:");
        }

        if (IsStartTag(tag, "script"))
        {
            var type = GetAttribute(tag, "type");
            var src = GetAttribute(tag, "src");
            return string.Equals(type, "application/ld+json", StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(src) && src.Contains("googletagmanager.com/gtag/js", StringComparison.OrdinalIgnoreCase)) ||
                   block.Contains("gtag('config'", StringComparison.OrdinalIgnoreCase) ||
                   block.Contains("gtag(\"config\"", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsStartTag(string tag, string name)
    {
        var index = 1;
        while (index < tag.Length && char.IsWhiteSpace(tag[index]))
        {
            index++;
        }

        if (index >= tag.Length || tag[index] == '/')
        {
            return false;
        }

        return tag.AsSpan(index).StartsWith(name.AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               (index + name.Length >= tag.Length ||
                char.IsWhiteSpace(tag[index + name.Length]) ||
                tag[index + name.Length] is '>' or '/');
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
