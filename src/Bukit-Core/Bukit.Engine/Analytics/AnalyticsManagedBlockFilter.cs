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
        if (comments.Count < 2)
        {
            return html;
        }

        var removals = new List<(int Start, int End)>();
        for (var index = 0; index < comments.Count; index++)
        {
            if (!TryParseManagedMarker(comments[index], out var start) || start.Edge != "start")
            {
                continue;
            }

            var depth = 1;
            var groupIsSimplePair = true;
            var groupClosed = false;
            for (var candidateIndex = index + 1; candidateIndex < comments.Count; candidateIndex++)
            {
                if (!TryParseManagedMarker(comments[candidateIndex], out var candidate))
                {
                    continue;
                }

                if (candidate.Edge == "start")
                {
                    depth++;
                    groupIsSimplePair = false;
                    continue;
                }

                depth--;
                if (depth > 0)
                {
                    groupIsSimplePair = false;
                    continue;
                }

                if (groupIsSimplePair &&
                    candidate.Key == start.Key &&
                    candidate.Location == start.Location)
                {
                    removals.Add((comments[index].Start, comments[candidateIndex].End));
                }

                // Nested, crossed, or mismatched marker groups are malformed.
                // Skip the entire balanced group without removing any part.
                index = candidateIndex;
                groupClosed = true;
                break;
            }

            if (!groupClosed)
            {
                // An unclosed marker group makes the remaining marker sequence
                // ambiguous. Preserve it rather than extracting a later pair.
                break;
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
}
