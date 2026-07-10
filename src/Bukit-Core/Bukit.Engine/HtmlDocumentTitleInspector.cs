using System.Net;

namespace Bukit.Engine;

internal sealed record HtmlDocumentTitleInspection(
    bool HasHead,
    string? HeadHtml,
    IReadOnlyList<string> Titles)
{
    internal int Count => Titles.Count;
    internal string? PrimaryTitle => Titles.Count == 0 ? null : Titles[0];
}

internal static class HtmlDocumentTitleInspector
{
    internal static HtmlDocumentTitleInspection Inspect(string html)
    {
        if (!HtmlHeadScanner.TryFindHead(html, out var head))
        {
            return new HtmlDocumentTitleInspection(false, null, Array.Empty<string>());
        }

        var titles = new List<string>();
        var searchStart = head.ContentStart;
        while (searchStart < head.ContentEnd)
        {
            var titleStart = HtmlHeadScanner.FindStartTag(
                html,
                "title",
                searchStart,
                head.ContentEnd);
            if (titleStart < 0)
            {
                break;
            }

            var titleStartEnd = HtmlHeadScanner.FindTagEnd(html, titleStart);
            if (titleStartEnd < 0 || titleStartEnd >= head.ContentEnd)
            {
                break;
            }

            var titleClose = HtmlHeadScanner.FindClosingTagStart(
                html,
                "title",
                titleStartEnd + 1,
                head.ContentEnd);
            if (titleClose < 0)
            {
                titles.Add(Normalize(html[(titleStartEnd + 1)..head.ContentEnd]));
                break;
            }

            titles.Add(Normalize(html[(titleStartEnd + 1)..titleClose]));
            var titleCloseEnd = HtmlHeadScanner.FindTagEnd(html, titleClose);
            searchStart = titleCloseEnd < 0 ? head.ContentEnd : titleCloseEnd + 1;
        }

        return new HtmlDocumentTitleInspection(
            true,
            html[head.Start..head.End],
            titles);
    }

    private static string Normalize(string value)
        => SeoDocumentTitleResolver.Normalize(WebUtility.HtmlDecode(value));
}
