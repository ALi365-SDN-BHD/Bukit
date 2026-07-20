using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Engine.Analytics;

internal static class AnalyticsManagedBlockFilter
{
    private static readonly Regex ManagedMarkerPattern = new(
        @"\A<!-- bukit:analytics:(?<key>[A-Za-z0-9.-]+:[A-Za-z0-9._-]+):(?<location>head|body):(?<edge>start|end) -->\z",
        RegexOptions.CultureInvariant);

    internal static string Remove(string html)
    {
        var comments = CollectHtmlComments(html);
        var markers = new List<ManagedMarkerToken>();
        foreach (var comment in comments)
        {
            if (TryParseManagedMarker(comment, out var marker))
            {
                markers.Add(new ManagedMarkerToken(comment, marker));
            }
        }

        if (markers.Count < 2)
        {
            return html;
        }

        var closeMarkerIndices = new int[markers.Count];
        var parentStartIndices = new int[markers.Count];
        Array.Fill(closeMarkerIndices, -1);
        Array.Fill(parentStartIndices, -1);

        // Pair marker edges without trusting their key or location. This preserves
        // the existing conservative boundaries for nested, crossed, and mismatched groups.
        var openStarts = new Stack<int>();
        for (var index = 0; index < markers.Count; index++)
        {
            if (markers[index].Marker.Edge == "start")
            {
                if (openStarts.TryPeek(out var parentStartIndex))
                {
                    parentStartIndices[index] = parentStartIndex;
                }

                openStarts.Push(index);
            }
            else if (openStarts.TryPop(out var startIndex))
            {
                closeMarkerIndices[startIndex] = index;
            }
        }

        var removals = new List<(int Start, int End)>();
        var hasClosedAncestor = new bool[markers.Count];
        for (var index = 0; index < markers.Count; index++)
        {
            var start = markers[index].Marker;
            if (start.Edge != "start")
            {
                continue;
            }

            var parentStartIndex = parentStartIndices[index];
            hasClosedAncestor[index] = parentStartIndex >= 0 &&
                                       (closeMarkerIndices[parentStartIndex] >= 0 || hasClosedAncestor[parentStartIndex]);

            var closeMarkerIndex = closeMarkerIndices[index];
            // A removable block must be a direct pair outside every closed group.
            // Unclosed ancestors are preserved but cannot shield a later valid pair.
            if (closeMarkerIndex != index + 1 || hasClosedAncestor[index])
            {
                continue;
            }

            var end = markers[closeMarkerIndex].Marker;
            if (end.Key == start.Key && end.Location == start.Location)
            {
                removals.Add((markers[index].Comment.Start, markers[closeMarkerIndex].Comment.End));
            }
        }

        if (removals.Count == 0)
        {
            return html;
        }

        var result = new StringBuilder(html.Length);
        var copyStart = 0;
        foreach (var removal in removals)
        {
            result.Append(html, copyStart, removal.Start - copyStart);
            copyStart = removal.End;
        }

        result.Append(html, copyStart, html.Length - copyStart);
        return result.ToString();
    }

    private static IReadOnlyList<HtmlCommentToken> CollectHtmlComments(string html)
    {
        var comments = new List<HtmlCommentToken>();
        var index = 0;
        while (index < html.Length)
        {
            var tagStart = html.IndexOf('<', index);
            if (tagStart < 0)
            {
                break;
            }

            if (HtmlHeadScanner.IsCommentStart(html, tagStart))
            {
                var commentEnd = HtmlHeadScanner.FindCommentEnd(html, tagStart, html.Length);
                if (commentEnd < 0)
                {
                    break;
                }

                comments.Add(new HtmlCommentToken(
                    tagStart,
                    commentEnd,
                    html.Substring(tagStart, commentEnd - tagStart)));
                index = commentEnd;
                continue;
            }

            var tagEnd = HtmlHeadScanner.FindTagEnd(html, tagStart);
            if (tagEnd < 0)
            {
                break;
            }

            var tag = html.Substring(tagStart, tagEnd - tagStart + 1);
            var rawTextElement = HtmlHeadScanner.GetRawTextElementName(tag);
            if (rawTextElement is not null)
            {
                var rawTextEnd = HtmlHeadScanner.FindClosingElementEnd(
                    html,
                    tagEnd + 1,
                    html.Length,
                    rawTextElement);
                if (rawTextEnd < 0)
                {
                    break;
                }

                index = rawTextEnd + 1;
                continue;
            }

            index = tagEnd + 1;
        }

        return comments;
    }

    private static bool TryParseManagedMarker(HtmlCommentToken comment, out ManagedMarker marker)
    {
        var match = ManagedMarkerPattern.Match(comment.Text);
        if (!match.Success)
        {
            marker = default;
            return false;
        }

        marker = new ManagedMarker(
            match.Groups["key"].Value,
            match.Groups["location"].Value,
            match.Groups["edge"].Value);
        return true;
    }

    private readonly record struct HtmlCommentToken(int Start, int End, string Text);

    private readonly record struct ManagedMarker(string Key, string Location, string Edge);

    private readonly record struct ManagedMarkerToken(HtmlCommentToken Comment, ManagedMarker Marker);
}
