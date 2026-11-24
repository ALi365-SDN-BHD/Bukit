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
    /// Attributes are found with <see cref="HtmlMediaReferenceScanner"/>, so text and code samples are
    /// never rewritten. Absolute, protocol-relative and document-relative URLs are left unchanged, and
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

        var origin = $"{site.Scheme}://{site.Authority}";
        StringBuilder? sb = null;
        var copied = 0;
        foreach (var reference in references)
        {
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

        // A data: URI holds commas (and base64 may start with '/'), so its candidates cannot be split safely.
        if (value.Contains("data:", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var candidateStart = 0;
        while (candidateStart <= value.Length)
        {
            var urlStart = SkipWhitespace(value, candidateStart);
            if (IsRootRelative(value, urlStart))
            {
                yield return urlStart;
            }

            var comma = value.IndexOf(',', candidateStart);
            if (comma < 0)
            {
                yield break;
            }

            candidateStart = comma + 1;
        }
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
