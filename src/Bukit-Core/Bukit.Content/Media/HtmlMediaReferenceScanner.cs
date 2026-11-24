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

    public static IReadOnlyList<HtmlMediaReference> Find(string html, bool includeAllAnchorHrefs = false)
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

            ScanAttributes(html, nameEnd, tagEnd, tagName, includeAllAnchorHrefs, ref references);

            i = tagEnd + 1;
        }

        return (IReadOnlyList<HtmlMediaReference>?)references ?? Array.Empty<HtmlMediaReference>();
    }

    /// <summary>
    /// Read-only deploy scan: every URL-bearing attribute value (quoted or unquoted), each srcset
    /// candidate, and raw style attribute values. Reuses the same tag and attribute tokenizer as
    /// <see cref="Find"/>; text nodes are never returned.
    /// </summary>
    internal static IReadOnlyList<string> EnumerateUrlAttributeValues(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return [];
        }

        var values = new List<string>();
        var span = html.AsSpan();
        var length = html.Length;
        var i = 0;
        while (i < length)
        {
            var found = FindNextOpeningTag(html, span, ref i, out var nameEnd, out var tagEnd, out var tagName);
            if (found < 0)
            {
                break;
            }

            if (found == 0)
            {
                continue;
            }

            if (html.AsSpan(nameEnd, tagEnd - nameEnd).IndexOf('=') >= 0)
            {
                List<HtmlMediaReference>? unused = null;
                ScanAttributes(html, nameEnd, tagEnd, tagName, includeAllAnchorHrefs: false, ref unused, values);
            }

            i = tagEnd + 1;
        }

        return values;
    }

    /// <returns>-1 when no further tag starts, 0 when a '&lt;' was skipped, 1 when a tag was found.</returns>
    private static int FindNextOpeningTag(
        string html,
        ReadOnlySpan<char> span,
        ref int i,
        out int nameEnd,
        out int tagEnd,
        out ReadOnlySpan<char> tagName)
    {
        nameEnd = 0;
        tagEnd = 0;
        tagName = default;
        var length = html.Length;
        var nextLt = span.Slice(i).IndexOf('<');
        if (nextLt < 0)
        {
            return -1;
        }

        i += nextLt;
        var tagStart = i + 1;
        if (tagStart >= length || !IsTagNameStart(html[tagStart]))
        {
            i++;
            return 0;
        }

        nameEnd = tagStart;
        while (nameEnd < length && IsTagNameChar(html[nameEnd]))
        {
            nameEnd++;
        }

        tagName = html.AsSpan(tagStart, nameEnd - tagStart);
        tagEnd = FindTagEnd(html, nameEnd);
        return tagEnd >= 0 ? 1 : -1;
    }

    private static readonly HashSet<string> CollectedUrlAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "src", "href", "poster", "data-src", "action", "formaction", "cite", "data", "url",
        "xlink:href", "content", "srcset", "style"
    };

    private static bool IsCollectedUrlAttribute(ReadOnlySpan<char> name)
        => CollectedUrlAttributes.Contains(name.ToString());

    /// <summary>Materializes the value only when URL values are being collected for this attribute.</summary>
    private static void CollectUrlValueIfRequested(List<string>? urlValues, ReadOnlySpan<char> name, ReadOnlySpan<char> value)
    {
        if (urlValues is null || !IsCollectedUrlAttribute(name))
        {
            return;
        }

        CollectUrlValue(name, value.ToString(), urlValues);
    }

    private static void CollectUrlValue(ReadOnlySpan<char> name, string value, List<string> values)
    {
        if (name.Equals("srcset", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var candidate in value.Split(','))
            {
                var url = candidate.Trim().Split(' ', 2)[0];
                if (url.Length > 0)
                {
                    values.Add(url);
                }
            }

            return;
        }

        values.Add(value);
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
        bool includeAllAnchorHrefs,
        ref List<HtmlMediaReference>? references,
        List<string>? urlValues = null)
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

            // Parse value. Quoted values classify media references; the URL-attribute collector
            // additionally records unquoted values (D11 needs them only in the deploy scan).
            var quote = html[i];
            if (quote != '"' && quote != '\'')
            {
                var unquotedStart = i;
                while (i < tagEnd && !IsAsciiWhitespace(html[i]))
                {
                    i++;
                }

                CollectUrlValueIfRequested(urlValues, attributeName, html.AsSpan(unquotedStart, i - unquotedStart));

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

            CollectUrlValueIfRequested(urlValues, attributeName, html.AsSpan(valueStart, valueLength));

            if (TryClassify(tagName, attributeName, html, valueStart, valueLength, includeAllAnchorHrefs, out var kind))
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
        bool includeAllAnchorHrefs,
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
            // Only the poster is an image. Video src/data-src/srcset point at media
            // (for example MP4), which must keep its original URL.
            if (attributeName.Equals("poster", StringComparison.OrdinalIgnoreCase))
            {
                kind = HtmlMediaReferenceKind.Url;
                return true;
            }
            return false;
        }

        if (tagName.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            if (attributeName.Equals("href", StringComparison.OrdinalIgnoreCase))
            {
                if (includeAllAnchorHrefs || IsImageHrefValue(html, valueStart, valueLength))
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

    internal static bool IsImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return IsImageHrefValue(trimmed, 0, trimmed.Length);
    }

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
