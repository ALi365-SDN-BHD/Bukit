using System.Net;
using System.Text;

namespace Bukit.Content.Markdown;

internal static class MarkdownTextHelper
{
    internal static string ExtractSummaryFromMarkdown(string markdown, int maxLength)
    {
        return ExtractSummaryFromHtml(BasicMarkdownToHtml.Convert(markdown), maxLength);
    }

    internal static async Task<string> RenderHtmlFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var markdown = await File.ReadAllTextAsync(filePath, cancellationToken);
        return RenderHtml(markdown);
    }

    internal static string RenderHtml(string markdown)
    {
        var bodyMarkdown = markdown;
        if (MarkdownFrontMatterParser.TryExtractFrontMatter(markdown, out _, out var body))
        {
            bodyMarkdown = body;
        }

        return BasicMarkdownToHtml.Convert(bodyMarkdown);
    }

    private static string ExtractSummaryFromHtml(string html, int maxLength)
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

    internal static string? ExtractTitle(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# "))
            {
                return trimmed[2..].Trim();
            }
        }

        return null;
    }
}
