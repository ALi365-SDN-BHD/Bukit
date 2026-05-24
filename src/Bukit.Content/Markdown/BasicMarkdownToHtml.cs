using Markdig;
using System.Text.RegularExpressions;

namespace Bukit.Content.Markdown;

public static class BasicMarkdownToHtml
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

    private static readonly Regex StandaloneImageParagraphRegex = new(
        @"^<p>(?<image><img\b[^>]* />)</p>$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex StandaloneMarkdownImageRegex = new(
        @"^\s*!\[[^\]]*\]\([^)]+\)\s*$",
        RegexOptions.Compiled);

    public static string Convert(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var normalized = NormalizeStandaloneImageBlocks(markdown.Replace("\r\n", "\n"));
        var html = global::Markdig.Markdown.ToHtml(normalized, Pipeline)
            .TrimEnd('\r', '\n');

        return StandaloneImageParagraphRegex.Replace(html, "${image}");
    }

    private static string NormalizeStandaloneImageBlocks(string markdown)
    {
        var lines = markdown.Split('\n');
        var normalized = new List<string>(capacity: lines.Length + 4);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!StandaloneMarkdownImageRegex.IsMatch(line))
            {
                normalized.Add(line);
                continue;
            }

            if (normalized.Count > 0 && !string.IsNullOrWhiteSpace(normalized[^1]))
            {
                normalized.Add(string.Empty);
            }

            normalized.Add(line.Trim());

            if (i + 1 < lines.Length && !string.IsNullOrWhiteSpace(lines[i + 1]))
            {
                normalized.Add(string.Empty);
            }
        }

        return string.Join("\n", normalized);
    }
}
