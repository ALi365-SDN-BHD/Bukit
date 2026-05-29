using System.Text.RegularExpressions;

namespace Bukit.Content.Media;

internal enum HtmlMediaReferenceKind
{
    Url,
    Srcset
}

internal readonly record struct HtmlMediaReference(
    HtmlMediaReferenceKind Kind,
    int ValueStart,
    int ValueLength,
    string Value);

internal static class HtmlMediaReferenceScanner
{
    private static readonly Regex AnchorImageHrefValueRegex = new(
        @"^https?://[^""']*?\.(?:jpg|jpeg|png|gif|webp|svg|avif|bmp|ico|tiff|tif)(?:\?[^""']*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<HtmlMediaReference> Find(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return Array.Empty<HtmlMediaReference>();
        }

        var references = new List<HtmlMediaReference>();
        var length = html.Length;
        var i = 0;

        while (i < length)
        {
            if (html[i] != '<')
            {
                i++;
                continue;
            }

            var tagStart = i + 1;
            if (tagStart >= length || !IsTagNameStart(html[tagStart]))
            {
                i++;
                continue;
            }

            // Read tag name.
            var nameEnd = tagStart;
            while (nameEnd < length && IsTagNameChar(html[nameEnd]))
            {
                nameEnd++;
            }
            var tagName = html.AsSpan(tagStart, nameEnd - tagStart);

            // Find end of opening tag (the first unquoted '>' after the tag name).
            var tagEnd = FindTagEnd(html, nameEnd);
            if (tagEnd < 0)
            {
                break;
            }

            // Only scan attributes for tags that may carry media references.
            // (img/video/a have known attrs; any other tag may carry data-src.)
            if (MayHaveMediaAttributes(tagName))
            {
                ScanAttributes(html, nameEnd, tagEnd, tagName, references);
            }

            i = tagEnd + 1;
        }

        return references;
    }

    private static bool MayHaveMediaAttributes(ReadOnlySpan<char> tagName)
        // Always true: any tag may carry data-src. The fast path is short-circuited
        // by ScanAttributes (which iterates only attribute boundaries within the
        // pre-bounded tag region), so this check exists only as a documentation hook.
        => !tagName.IsEmpty;

    private static int FindTagEnd(string html, int from)
    {
        var length = html.Length;
        var inQuote = '\0';
        for (var i = from; i < length; i++)
        {
            var c = html[i];
            if (inQuote != '\0')
            {
                if (c == inQuote)
                {
                    inQuote = '\0';
                }
                continue;
            }

            if (c == '"' || c == '\'')
            {
                inQuote = c;
                continue;
            }

            if (c == '>')
            {
                return i;
            }
        }

        return -1;
    }

    private static void ScanAttributes(
        string html,
        int attrsStart,
        int tagEnd,
        ReadOnlySpan<char> tagName,
        List<HtmlMediaReference> references)
    {
        var i = attrsStart;
        while (i < tagEnd)
        {
            // Skip whitespace and '/' (e.g. self-closing slashes).
            while (i < tagEnd && (char.IsWhiteSpace(html[i]) || html[i] == '/'))
            {
                i++;
            }

            if (i >= tagEnd) return;

            // Read attribute name.
            var nameStart = i;
            if (!IsAttributeNameStart(html[i]))
            {
                i++;
                continue;
            }
            while (i < tagEnd && IsAttributeNameChar(html[i]))
            {
                i++;
            }
            var nameEnd = i;
            var attributeName = html.AsSpan(nameStart, nameEnd - nameStart);

            // Skip whitespace before '='.
            while (i < tagEnd && char.IsWhiteSpace(html[i]))
            {
                i++;
            }

            // Boolean attribute (no value).
            if (i >= tagEnd || html[i] != '=')
            {
                continue;
            }

            i++; // consume '='.

            // Skip whitespace after '='.
            while (i < tagEnd && char.IsWhiteSpace(html[i]))
            {
                i++;
            }

            if (i >= tagEnd) return;

            // Parse value. Only quoted values are supported (matching the original regex).
            var quote = html[i];
            if (quote != '"' && quote != '\'')
            {
                // Unquoted value — skip to next whitespace or end of tag.
                while (i < tagEnd && !char.IsWhiteSpace(html[i]))
                {
                    i++;
                }
                continue;
            }

            i++; // consume opening quote.
            var valueStart = i;
            while (i < tagEnd && html[i] != quote)
            {
                i++;
            }

            if (i >= tagEnd)
            {
                // Unterminated value — stop processing this tag.
                return;
            }

            var valueLength = i - valueStart;
            i++; // consume closing quote.

            if (TryClassify(tagName, attributeName, html, valueStart, valueLength, out var kind))
            {
                references.Add(new HtmlMediaReference(
                    kind,
                    valueStart,
                    valueLength,
                    html.Substring(valueStart, valueLength)));
            }
        }
    }

    private static bool TryClassify(
        ReadOnlySpan<char> tagName,
        ReadOnlySpan<char> attributeName,
        string html,
        int valueStart,
        int valueLength,
        out HtmlMediaReferenceKind kind)
    {
        if (attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase))
        {
            kind = HtmlMediaReferenceKind.Srcset;
            return true;
        }

        if (attributeName.Equals("data-src", StringComparison.OrdinalIgnoreCase))
        {
            kind = HtmlMediaReferenceKind.Url;
            return true;
        }

        if (tagName.Equals("img", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("src", StringComparison.OrdinalIgnoreCase))
        {
            kind = HtmlMediaReferenceKind.Url;
            return true;
        }

        if (tagName.Equals("video", StringComparison.OrdinalIgnoreCase)
            && (attributeName.Equals("poster", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)))
        {
            kind = HtmlMediaReferenceKind.Url;
            return true;
        }

        if (tagName.Equals("a", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("href", StringComparison.OrdinalIgnoreCase))
        {
            var rawValue = html.Substring(valueStart, valueLength);
            if (AnchorImageHrefValueRegex.IsMatch(System.Net.WebUtility.HtmlDecode(rawValue)))
            {
                kind = HtmlMediaReferenceKind.Url;
                return true;
            }
        }

        kind = default;
        return false;
    }

    private static bool IsTagNameStart(char c)
        => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    private static bool IsTagNameChar(char c)
        => IsTagNameStart(c)
           || (c >= '0' && c <= '9')
           || c == ':' || c == '_' || c == '-';

    private static bool IsAttributeNameStart(char c)
        => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_' || c == ':';

    private static bool IsAttributeNameChar(char c)
        => IsAttributeNameStart(c)
           || (c >= '0' && c <= '9')
           || c == '-' || c == '.';
}
