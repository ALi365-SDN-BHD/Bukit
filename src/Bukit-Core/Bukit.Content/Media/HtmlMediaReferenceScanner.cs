using System.Runtime.CompilerServices;

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
    private static readonly string[] ImageExtensions =
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".avif", ".bmp", ".ico", ".tiff", ".tif"
    };

    public static IReadOnlyList<HtmlMediaReference> Find(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return Array.Empty<HtmlMediaReference>();
        }

        List<HtmlMediaReference>? references = null;
        var span = html.AsSpan();
        var length = html.Length;
        var i = 0;

        while (i < length)
        {
            var remaining = span.Slice(i);
            var nextLt = remaining.IndexOf('<');
            if (nextLt < 0)
            {
                break;
            }

            i += nextLt;
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

            // Fast path: if there's no '=' in the tag, no attribute values can exist.
            var tagBody = html.AsSpan(nameEnd, tagEnd - nameEnd);
            if (tagBody.IndexOf('=') < 0)
            {
                i = tagEnd + 1;
                continue;
            }

            ScanAttributes(html, nameEnd, tagEnd, tagName, ref references);

            i = tagEnd + 1;
        }

        return (IReadOnlyList<HtmlMediaReference>?)references ?? Array.Empty<HtmlMediaReference>();
    }

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
        ref List<HtmlMediaReference>? references)
    {
        var i = attrsStart;
        while (i < tagEnd)
        {
            // Skip whitespace and '/' (e.g. self-closing slashes).
            while (i < tagEnd && (IsAsciiWhitespace(html[i]) || html[i] == '/'))
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
            while (i < tagEnd && IsAsciiWhitespace(html[i]))
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
            while (i < tagEnd && IsAsciiWhitespace(html[i]))
            {
                i++;
            }

            if (i >= tagEnd) return;

            // Parse value. Only quoted values are supported (matching the original regex).
            var quote = html[i];
            if (quote != '"' && quote != '\'')
            {
                // Unquoted value — skip to next whitespace or end of tag.
                while (i < tagEnd && !IsAsciiWhitespace(html[i]))
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
                references ??= new List<HtmlMediaReference>(4);
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
        kind = default;

        // Dispatch on tag name first to avoid checking attributes that don't apply.
        if (tagName.Equals("img", StringComparison.OrdinalIgnoreCase))
        {
            if (attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("data-src", StringComparison.OrdinalIgnoreCase))
            {
                kind = HtmlMediaReferenceKind.Url;
                return true;
            }
            if (attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase))
            {
                kind = HtmlMediaReferenceKind.Srcset;
                return true;
            }
            return false;
        }

        if (tagName.Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            if (attributeName.Equals("poster", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("data-src", StringComparison.OrdinalIgnoreCase))
            {
                kind = HtmlMediaReferenceKind.Url;
                return true;
            }
            if (attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase))
            {
                kind = HtmlMediaReferenceKind.Srcset;
                return true;
            }
            return false;
        }

        if (tagName.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            if (attributeName.Equals("href", StringComparison.OrdinalIgnoreCase))
            {
                if (IsImageHrefValue(html, valueStart, valueLength))
                {
                    kind = HtmlMediaReferenceKind.Url;
                    return true;
                }
                return false;
            }
            if (attributeName.Equals("data-src", StringComparison.OrdinalIgnoreCase))
            {
                kind = HtmlMediaReferenceKind.Url;
                return true;
            }
            if (attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase))
            {
                kind = HtmlMediaReferenceKind.Srcset;
                return true;
            }
            return false;
        }

        // Unknown tag: only data-src and srcset are relevant.
        if (attributeName.Equals("data-src", StringComparison.OrdinalIgnoreCase))
        {
            kind = HtmlMediaReferenceKind.Url;
            return true;
        }
        if (attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase))
        {
            kind = HtmlMediaReferenceKind.Srcset;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTagNameStart(char c)
        => (uint)((c | 0x20) - 'a') <= (uint)('z' - 'a');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTagNameChar(char c)
        => IsTagNameStart(c)
           || (uint)(c - '0') <= (uint)('9' - '0')
           || c == ':' || c == '_' || c == '-';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAttributeNameStart(char c)
        => (uint)((c | 0x20) - 'a') <= (uint)('z' - 'a') || c == '_' || c == ':';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAttributeNameChar(char c)
        => IsAttributeNameStart(c)
           || (uint)(c - '0') <= (uint)('9' - '0')
           || c == '-' || c == '.';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiWhitespace(char c)
        => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f';

    /// <summary>
    /// Returns true if <paramref name="html"/>[valueStart..valueStart+valueLength] looks like
    /// an http(s) URL whose path ends in a known image extension (optionally followed by a query).
    /// Allocation-free; does not invoke <see cref="System.Net.WebUtility.HtmlDecode"/> because the
    /// scheme and extension portions of a URL never contain HTML entities in practice.
    /// </summary>
    private static bool IsImageHrefValue(string html, int valueStart, int valueLength)
    {
        if (valueLength < 8) return false; // shortest viable: "http://a"

        var value = html.AsSpan(valueStart, valueLength);

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var questionMark = value.IndexOf('?');
        var pathEnd = questionMark >= 0 ? questionMark : value.Length;
        if (pathEnd < 5) return false;

        var extStart = -1;
        for (var j = pathEnd - 1; j >= 0; j--)
        {
            var ch = value[j];
            if (ch == '.')
            {
                extStart = j;
                break;
            }
            if (ch == '/' || ch == ':')
            {
                break;
            }
        }

        if (extStart < 0) return false;

        var ext = value.Slice(extStart, pathEnd - extStart);
        foreach (var imageExt in ImageExtensions)
        {
            if (ext.Equals(imageExt.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
