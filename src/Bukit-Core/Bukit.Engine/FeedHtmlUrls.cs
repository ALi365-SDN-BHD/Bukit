using System.Text;
using Bukit.Content.Media;

namespace Bukit.Engine;

/// <summary>
/// Makes root-relative URLs in feed HTML absolute. Feed readers show item HTML without the page's
/// origin, so a root-relative <c>href="/post/"</c> resolves against the reader, not the site.
/// </summary>
internal static class FeedHtmlUrls
{
    /// <summary>
    /// Prefixes root-relative link and media URLs (anchor <c>href</c>, image <c>src</c>/<c>data-src</c>,
    /// video <c>poster</c> and <c>srcset</c> candidates) with the origin of <paramref name="siteUrl"/>.
    /// Attributes are found with <see cref="HtmlMediaReferenceScanner"/>, so text is never rewritten, and
    /// markup inside comments and raw-text elements (<c>script</c>, <c>style</c>, <c>textarea</c>,
    /// <c>title</c>) is left as written. Absolute, protocol-relative and document-relative URLs are left unchanged, and
    /// the HTML is returned unchanged when the site URL has no usable origin.
    /// </summary>
    internal static string AbsolutizeRootRelative(string html, string siteUrl)
    {
        ArgumentNullException.ThrowIfNull(html);
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var site) ||
            (site.Scheme != Uri.UriSchemeHttp && site.Scheme != Uri.UriSchemeHttps))
        {
            return html;
        }

        var references = HtmlMediaReferenceScanner.Find(html, includeAllAnchorHrefs: true);
        if (references.Count == 0)
        {
            return html;
        }

        var inert = InertRanges(html);
        var origin = $"{site.Scheme}://{site.Authority}";
        StringBuilder? sb = null;
        var copied = 0;
        var range = 0;
        foreach (var reference in references)
        {
            while (range < inert.Count && inert[range].End <= reference.ValueStart)
            {
                range++;
            }

            if (range < inert.Count && inert[range].Start <= reference.ValueStart)
            {
                continue;
            }

            foreach (var offset in RootRelativeOffsets(reference))
            {
                sb ??= new StringBuilder(html.Length + 64);
                var insertAt = reference.ValueStart + offset;
                sb.Append(html, copied, insertAt - copied).Append(origin);
                copied = insertAt;
            }
        }

        return sb is null ? html : sb.Append(html, copied, html.Length - copied).ToString();
    }

    /// <summary>Applies <see cref="AbsolutizeRootRelative"/> to each post's content HTML.</summary>
    internal static IReadOnlyList<RssGenerator.Post> AbsolutizeContent(IReadOnlyList<RssGenerator.Post> posts, string siteUrl)
        => [.. posts.Select(post => post.ContentHtml is null
            ? post
            : post with { ContentHtml = AbsolutizeRootRelative(post.ContentHtml, siteUrl) })];

    /// <summary>Offsets inside the attribute value where a root-relative URL starts, in order.</summary>
    private static IEnumerable<int> RootRelativeOffsets(HtmlMediaReference reference)
    {
        var value = reference.Value;
        if (reference.Kind == HtmlMediaReferenceKind.Url)
        {
            var start = SkipWhitespace(value, 0);
            if (IsRootRelative(value, start))
            {
                yield return start;
            }

            yield break;
        }

        // Split candidates as the HTML srcset parser does: a URL runs to the next whitespace (it may
        // contain commas), trailing commas on it end the candidate, and descriptors run to the next comma.
        var index = 0;
        while (true)
        {
            while (index < value.Length && (char.IsWhiteSpace(value[index]) || value[index] == ','))
            {
                index++;
            }

            if (index >= value.Length)
            {
                yield break;
            }

            if (IsRootRelative(value, index))
            {
                yield return index;
            }

            while (index < value.Length && !char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            if (value[index - 1] == ',')
            {
                continue;
            }

            var comma = value.IndexOf(',', index);
            if (comma < 0)
            {
                yield break;
            }

            index = comma + 1;
        }
    }

    /// <summary>
    /// Ranges, in document order, whose apparent tags are text rather than elements: comments and the
    /// content of raw-text elements. An unclosed one runs to the end of the HTML.
    /// </summary>
    private static List<(int Start, int End)> InertRanges(string html)
    {
        var ranges = new List<(int Start, int End)>();
        var index = html.IndexOf('<');
        while (index >= 0)
        {
            int start, end;
            if (HtmlHeadScanner.IsCommentStart(html, index))
            {
                start = index;
                end = HtmlHeadScanner.FindCommentEnd(html, index, html.Length);
            }
            else if (index + 1 < html.Length && (char.IsAsciiLetter(html[index + 1]) || html[index + 1] == '/'))
            {
                var tagEnd = HtmlHeadScanner.FindTagEnd(html, index);
                if (tagEnd < 0)
                {
                    break;
                }

                var rawText = html[index + 1] is 's' or 'S' or 't' or 'T'
                    ? HtmlHeadScanner.GetRawTextElementName(html.Substring(index, tagEnd - index + 1))
                    : null;
                if (rawText is null)
                {
                    index = html.IndexOf('<', tagEnd + 1);
                    continue;
                }

                // The start tag's own attributes stay rewritable; only its content is text.
                start = tagEnd + 1;
                end = HtmlHeadScanner.FindClosingElementEnd(html, start, html.Length, rawText);
            }
            else
            {
                index = html.IndexOf('<', index + 1);
                continue;
            }

            if (end < 0)
            {
                ranges.Add((start, html.Length));
                break;
            }

            ranges.Add((start, end));
            index = html.IndexOf('<', end);
        }

        return ranges;
    }

    private static int SkipWhitespace(string value, int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsRootRelative(string value, int index)
        => index < value.Length && value[index] == '/' && (index + 1 >= value.Length || value[index + 1] != '/');
}
