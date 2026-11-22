namespace Bukit.Engine;

/// <summary>
/// One piece of head HTML: a text run or comment (<see cref="Tag"/> is null), or an element start tag
/// together with its raw-text content and closing tag for <c>script</c>, <c>style</c>, <c>title</c> and
/// <c>textarea</c>.
/// </summary>
internal readonly record struct HtmlHeadSegment(int Start, int Length, string? Tag);

internal static class HtmlHeadSegments
{
    /// <summary>
    /// Splits <paramref name="html"/> into consecutive segments that cover it exactly.
    /// An unterminated comment or tag ends the scan with one final text segment.
    /// </summary>
    internal static IEnumerable<HtmlHeadSegment> Enumerate(string html)
    {
        var index = 0;
        while (index < html.Length)
        {
            var tagStart = html.IndexOf('<', index);
            if (tagStart < 0)
            {
                yield return new HtmlHeadSegment(index, html.Length - index, null);
                yield break;
            }

            if (tagStart > index)
            {
                yield return new HtmlHeadSegment(index, tagStart - index, null);
            }

            if (HtmlHeadScanner.IsCommentStart(html, tagStart))
            {
                var commentEnd = HtmlHeadScanner.FindCommentEnd(html, tagStart, html.Length);
                if (commentEnd < 0)
                {
                    yield return new HtmlHeadSegment(tagStart, html.Length - tagStart, null);
                    yield break;
                }

                yield return new HtmlHeadSegment(tagStart, commentEnd - tagStart, null);
                index = commentEnd;
                continue;
            }

            var tagEnd = HtmlHeadScanner.FindTagEnd(html, tagStart);
            if (tagEnd < 0)
            {
                yield return new HtmlHeadSegment(tagStart, html.Length - tagStart, null);
                yield break;
            }

            var tag = html.Substring(tagStart, tagEnd - tagStart + 1);
            var blockEnd = tagEnd;
            var rawTextElement = HtmlHeadScanner.GetRawTextElementName(tag);
            if (rawTextElement is not null)
            {
                var elementClose = HtmlHeadScanner.FindClosingElementEnd(html, tagEnd + 1, html.Length, rawTextElement);
                if (elementClose >= 0)
                {
                    blockEnd = elementClose;
                }
            }

            yield return new HtmlHeadSegment(tagStart, blockEnd - tagStart + 1, tag);
            index = blockEnd + 1;
        }
    }
}

/// <summary>
/// JSON-LD scripts a theme keeps in the head under <c>site.seo.renderMode: inject</c> by marking them
/// <c>data-bukit-seo="keep"</c>. Unmarked head JSON-LD is replaced by the Core-generated JSON-LD.
/// </summary>
internal static class SeoJsonLdScripts
{
    internal const string KeepAttribute = "data-bukit-seo";
    internal const string KeepValue = "keep";

    internal static bool IsJsonLdScript(string tag)
        => HtmlHeadScanner.IsStartTag(tag, "script") &&
           string.Equals(HtmlTagAttributeReader.GetAttribute(tag, "type"), "application/ld+json", StringComparison.OrdinalIgnoreCase);

    internal static bool IsKept(string tag)
        => string.Equals(HtmlTagAttributeReader.GetAttribute(tag, KeepAttribute)?.Trim(), KeepValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>The text between the start tag and the closing tag of a raw-text element segment.</summary>
    internal static string GetContent(string html, HtmlHeadSegment segment)
    {
        var contentStart = segment.Start + segment.Tag!.Length;
        if (segment.Length == segment.Tag.Length)
        {
            return string.Empty;
        }

        // A raw-text segment always ends with its closing tag.
        var closeStart = html.LastIndexOf("</", segment.Start + segment.Length - 1, StringComparison.Ordinal);
        return closeStart < contentStart ? string.Empty : html[contentStart..closeStart];
    }

    /// <summary>Contents of the head's <c>data-bukit-seo="keep"</c> JSON-LD scripts, in document order.</summary>
    internal static IReadOnlyList<string> ExtractKeptHeadJsonLd(string html)
    {
        if (!HtmlHeadScanner.TryFindHead(html, out var head))
        {
            return [];
        }

        var headContent = html[head.ContentStart..head.ContentEnd];
        var kept = new List<string>();
        foreach (var segment in HtmlHeadSegments.Enumerate(headContent))
        {
            if (segment.Tag is { } tag && IsJsonLdScript(tag) && IsKept(tag))
            {
                kept.Add(GetContent(headContent, segment).Trim());
            }
        }

        return kept;
    }
}
