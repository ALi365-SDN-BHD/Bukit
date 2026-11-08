using System.Net;

namespace Bukit.Engine;

internal readonly record struct HtmlHeadRange(
    int Start,
    int ContentStart,
    int ContentEnd,
    int End);

internal static class HtmlHeadScanner
{
    private static readonly string[] RawTextElementNames = ["script", "style", "title", "textarea"];

    internal static bool TryFindHead(string html, out HtmlHeadRange range)
    {
        range = default;
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var headStart = FindStartTag(html, "head", 0, html.Length);
        if (headStart < 0)
        {
            return false;
        }

        var headStartEnd = FindTagEnd(html, headStart);
        if (headStartEnd < 0)
        {
            return false;
        }

        var headClose = FindClosingTagStart(html, "head", headStartEnd + 1, html.Length);
        if (headClose < 0)
        {
            return false;
        }

        var headCloseEnd = FindTagEnd(html, headClose);
        if (headCloseEnd < 0)
        {
            return false;
        }

        range = new HtmlHeadRange(
            headStart,
            headStartEnd + 1,
            headClose,
            headCloseEnd + 1);
        return true;
    }

    /// <summary>
    /// Returns the end offset of the first encoding declaration inside the head, so
    /// callers can keep it within the first 1024 bytes. Returns -1 when none exists.
    /// </summary>
    internal static int FindEncodingDeclarationEnd(string html, HtmlHeadRange head)
    {
        var index = head.ContentStart;
        while (index < head.ContentEnd)
        {
            var tagStart = html.IndexOf('<', index, head.ContentEnd - index);
            if (tagStart < 0)
            {
                return -1;
            }

            if (IsCommentStart(html, tagStart))
            {
                var commentEnd = FindCommentEnd(html, tagStart, head.ContentEnd);
                if (commentEnd < 0)
                {
                    return -1;
                }

                index = commentEnd;
                continue;
            }

            var tagEnd = FindTagEnd(html, tagStart);
            if (tagEnd < 0 || tagEnd >= head.ContentEnd)
            {
                return -1;
            }

            var tag = html.Substring(tagStart, tagEnd - tagStart + 1);
            if (IsStartTag(tag, "meta") && IsEncodingDeclaration(tag))
            {
                return tagEnd + 1;
            }

            var rawTextElement = GetRawTextElementName(tag);
            if (rawTextElement is not null)
            {
                var closeStart = FindClosingTagStartRaw(
                    html,
                    rawTextElement,
                    tagEnd + 1,
                    head.ContentEnd);
                if (closeStart < 0)
                {
                    return -1;
                }

                var closeEnd = FindTagEnd(html, closeStart);
                if (closeEnd < 0 || closeEnd >= head.ContentEnd)
                {
                    return -1;
                }

                index = closeEnd + 1;
                continue;
            }

            index = tagEnd + 1;
        }

        return -1;
    }

    private static bool IsEncodingDeclaration(string tag)
    {
        if (HtmlTagAttributeReader.GetAttribute(tag, "charset") is not null)
        {
            return true;
        }

        var httpEquiv = HtmlTagAttributeReader.GetAttribute(tag, "http-equiv");
        var content = HtmlTagAttributeReader.GetAttribute(tag, "content");
        return string.Equals(httpEquiv?.Trim(), "content-type", StringComparison.OrdinalIgnoreCase) &&
               content is not null &&
               content.Contains("charset", StringComparison.OrdinalIgnoreCase);
    }

    internal static int FindStartTag(string html, string name, int searchStart, int searchEnd)
    {
        var index = Math.Max(0, searchStart);
        var limit = Math.Min(searchEnd, html.Length);
        while (index < limit)
        {
            var tagStart = html.IndexOf('<', index);
            if (tagStart < 0 || tagStart >= limit)
            {
                return -1;
            }

            if (IsCommentStart(html, tagStart))
            {
                var commentEnd = FindCommentEnd(html, tagStart, limit);
                if (commentEnd < 0)
                {
                    return -1;
                }

                index = commentEnd;
                continue;
            }

            var tagEnd = FindTagEnd(html, tagStart);
            if (tagEnd < 0 || tagEnd >= limit)
            {
                return -1;
            }

            var tag = html.Substring(tagStart, tagEnd - tagStart + 1);
            if (IsStartTag(tag, name))
            {
                return tagStart;
            }

            var rawTextElement = GetRawTextElementName(tag);
            if (rawTextElement is not null)
            {
                var closeStart = FindClosingTagStartRaw(
                    html,
                    rawTextElement,
                    tagEnd + 1,
                    limit);
                if (closeStart < 0)
                {
                    return -1;
                }

                var closeEnd = FindTagEnd(html, closeStart);
                if (closeEnd < 0 || closeEnd >= limit)
                {
                    return -1;
                }

                index = closeEnd + 1;
                continue;
            }

            index = tagEnd + 1;
        }

        return -1;
    }

    internal static int FindClosingTagStart(string html, string name, int searchStart, int searchEnd)
    {
        if (IsRawTextElementName(name))
        {
            return FindClosingTagStartRaw(html, name, searchStart, searchEnd);
        }

        var index = Math.Max(0, searchStart);
        var limit = Math.Min(searchEnd, html.Length);
        while (index < limit)
        {
            var tagStart = html.IndexOf('<', index);
            if (tagStart < 0 || tagStart >= limit)
            {
                return -1;
            }

            if (IsCommentStart(html, tagStart))
            {
                var commentEnd = FindCommentEnd(html, tagStart, limit);
                if (commentEnd < 0)
                {
                    return -1;
                }

                index = commentEnd;
                continue;
            }

            var tagEnd = FindTagEnd(html, tagStart);
            if (tagEnd < 0 || tagEnd >= limit)
            {
                return -1;
            }

            var tag = html.Substring(tagStart, tagEnd - tagStart + 1);
            if (IsEndTag(tag, name))
            {
                return tagStart;
            }

            var rawTextElement = GetRawTextElementName(tag);
            if (rawTextElement is not null)
            {
                var closeStart = FindClosingTagStartRaw(
                    html,
                    rawTextElement,
                    tagEnd + 1,
                    limit);
                if (closeStart < 0)
                {
                    return -1;
                }

                var closeEnd = FindTagEnd(html, closeStart);
                if (closeEnd < 0 || closeEnd >= limit)
                {
                    return -1;
                }

                index = closeEnd + 1;
                continue;
            }

            index = tagEnd + 1;
        }

        return -1;
    }

    internal static int FindClosingElementEnd(string html, int searchStart, int searchEnd, string elementName)
    {
        var closeStart = FindClosingTagStart(html, elementName, searchStart, searchEnd);
        return closeStart < 0 ? -1 : FindTagEnd(html, closeStart);
    }

    internal static int FindTagEnd(string html, int tagStart)
    {
        // A quote only opens an attribute value right after '=' (whitespace allowed),
        // matching HTML parsing; a stray quote elsewhere must not swallow the rest of the document.
        var quote = '\0';
        var afterEquals = false;
        for (var index = tagStart + 1; index < html.Length; index++)
        {
            var current = html[index];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '"' or '\'' && afterEquals)
            {
                quote = current;
                afterEquals = false;
                continue;
            }

            if (current == '=')
            {
                afterEquals = true;
                continue;
            }

            if (!char.IsWhiteSpace(current))
            {
                afterEquals = false;
            }

            if (current == '>')
            {
                return index;
            }
        }

        return -1;
    }

    internal static bool IsStartTag(string tag, string name) => IsTag(tag, name, isEndTag: false);

    internal static bool IsCommentStart(string html, int tagStart)
        => tagStart >= 0 &&
           tagStart + 4 <= html.Length &&
           html.AsSpan(tagStart, 4).SequenceEqual("<!--".AsSpan());

    internal static int FindCommentEnd(string html, int commentStart, int searchEnd)
    {
        var limit = Math.Min(searchEnd, html.Length);
        var marker = html.IndexOf("-->", commentStart + 4, StringComparison.Ordinal);
        return marker < 0 || marker + 3 > limit ? -1 : marker + 3;
    }

    internal static string? GetRawTextElementName(string tag)
    {
        foreach (var name in RawTextElementNames)
        {
            if (IsStartTag(tag, name))
            {
                return name;
            }
        }

        return null;
    }

    private static bool IsEndTag(string tag, string name) => IsTag(tag, name, isEndTag: true);

    private static bool IsRawTextElementName(string name)
        => name.Equals("script", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("style", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("title", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("textarea", StringComparison.OrdinalIgnoreCase);

    private static int FindClosingTagStartRaw(string html, string name, int searchStart, int searchEnd)
    {
        var index = Math.Max(0, searchStart);
        var limit = Math.Min(searchEnd, html.Length);
        while (index < limit)
        {
            var tagStart = html.IndexOf("</", index, StringComparison.Ordinal);
            if (tagStart < 0 || tagStart >= limit)
            {
                return -1;
            }

            var tagEnd = FindTagEnd(html, tagStart);
            if (tagEnd < 0 || tagEnd >= limit)
            {
                return -1;
            }

            var tag = html.Substring(tagStart, tagEnd - tagStart + 1);
            if (IsEndTag(tag, name))
            {
                return tagStart;
            }

            index = tagEnd + 1;
        }

        return -1;
    }

    private static bool IsTag(string tag, string name, bool isEndTag)
    {
        var index = 1;
        while (index < tag.Length && char.IsWhiteSpace(tag[index]))
        {
            index++;
        }

        if (isEndTag)
        {
            if (index >= tag.Length || tag[index] != '/')
            {
                return false;
            }

            index++;
            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }
        }
        else if (index >= tag.Length || tag[index] == '/')
        {
            return false;
        }

        return tag.AsSpan(index).StartsWith(name.AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               (index + name.Length >= tag.Length ||
                char.IsWhiteSpace(tag[index + name.Length]) ||
                tag[index + name.Length] is '>' or '/');
    }
}

internal static class HtmlTagAttributeReader
{
    internal static string? GetAttribute(string tag, string name)
    {
        var index = 1;
        while (index < tag.Length && !char.IsWhiteSpace(tag[index]) && tag[index] != '>')
        {
            index++;
        }

        while (index < tag.Length)
        {
            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            if (index >= tag.Length || tag[index] == '>')
            {
                return null;
            }

            if (tag[index] == '/')
            {
                index++;
                continue;
            }

            var attrStart = index;
            while (index < tag.Length &&
                   !char.IsWhiteSpace(tag[index]) &&
                   tag[index] != '=' &&
                   tag[index] != '>' &&
                   tag[index] != '/')
            {
                index++;
            }

            var attrName = tag[attrStart..index];
            if (attrName.Length == 0)
            {
                index++;
                continue;
            }

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
}
