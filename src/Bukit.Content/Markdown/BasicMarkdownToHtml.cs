using System.Net;
using System.Text.RegularExpressions;

namespace Bukit.Content.Markdown;

public static class BasicMarkdownToHtml
{
    private static readonly Regex ImageRegex = new(
        @"!\[(?<alt>[^\]]*)\]\((?<url>[^\s\)]+)(?:\s+""(?<title>[^""]*)"")?\)",
        RegexOptions.Compiled);

    public static string Convert(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var htmlLines = new List<string>(capacity: lines.Length);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("### "))
            {
                htmlLines.Add($"<h3>{WebUtility.HtmlEncode(line[4..].Trim())}</h3>");
                continue;
            }

            if (line.StartsWith("## "))
            {
                htmlLines.Add($"<h2>{WebUtility.HtmlEncode(line[3..].Trim())}</h2>");
                continue;
            }

            if (line.StartsWith("# "))
            {
                htmlLines.Add($"<h1>{WebUtility.HtmlEncode(line[2..].Trim())}</h1>");
                continue;
            }

            var trimmed = line.Trim();
            if (ImageRegex.IsMatch(trimmed))
            {
                htmlLines.Add(ReplaceImages(trimmed));
                continue;
            }

            htmlLines.Add($"<p>{WebUtility.HtmlEncode(trimmed)}</p>");
        }

        return string.Join("\n", htmlLines);
    }

    private static string ReplaceImages(string line)
    {
        return ImageRegex.Replace(line, m =>
        {
            var alt = WebUtility.HtmlEncode(m.Groups["alt"].Value);
            var url = WebUtility.HtmlEncode(m.Groups["url"].Value);
            var title = m.Groups["title"].Value;
            var titleAttr = string.IsNullOrEmpty(title)
                ? string.Empty
                : $" title=\"{WebUtility.HtmlEncode(title)}\"";
            return $"<img src=\"{url}\" alt=\"{alt}\"{titleAttr} />";
        });
    }
}

