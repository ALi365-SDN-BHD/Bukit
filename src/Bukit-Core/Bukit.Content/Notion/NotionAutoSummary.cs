using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text;

namespace Bukit.Content.Notion;

internal static class NotionAutoSummary
{
    internal static string ExtractFromHtml(string html, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        var text = StripHtmlToText(html);
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return TruncateAtWordBoundary(text, maxLength);
    }

    private static string StripHtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(html.Length);
        var inTag = false;
        for (var i = 0; i < html.Length; i++)
        {
            var ch = html[i];
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (ch == '>')
            {
                inTag = false;
                sb.Append(' ');
                continue;
            }

            if (!inTag)
            {
                sb.Append(ch);
            }
        }

        var decoded = WebUtility.HtmlDecode(sb.ToString());
        return CollapseWhitespace(decoded);
    }

    private static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }

            sb.Append(ch);
            lastWasSpace = false;
        }

        return sb.ToString().Trim();
    }

    private static string TruncateAtWordBoundary(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', maxLength);
        if (cut < maxLength / 2)
        {
            cut = maxLength;
        }

        var trimmed = text[..cut].TrimEnd();
        return string.IsNullOrWhiteSpace(trimmed) ? string.Empty : trimmed + "…";
    }
}
