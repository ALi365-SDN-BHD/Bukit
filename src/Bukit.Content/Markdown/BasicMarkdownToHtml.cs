using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using System.Text.RegularExpressions;

namespace Bukit.Content.Markdown;

public static class BasicMarkdownToHtml
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
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

    public static IReadOnlyList<TableOfContentsEntry> ExtractTableOfContents(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Array.Empty<TableOfContentsEntry>();
        }

        var entries = new List<TableOfContentsEntry>();
        var seenIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var inFence = false;
        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimStart();
            if (line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || !line.StartsWith('#'))
            {
                continue;
            }

            var level = 0;
            while (level < line.Length && line[level] == '#')
            {
                level++;
            }

            if (level is < 1 or > 6 || level >= line.Length || !char.IsWhiteSpace(line[level]))
            {
                continue;
            }

            var text = line[level..].Trim().TrimEnd('#').Trim();
            if (text.Length == 0)
            {
                continue;
            }

            var baseId = SlugifyHeading(text);
            if (seenIds.TryGetValue(baseId, out var count))
            {
                count++;
                seenIds[baseId] = count;
                entries.Add(new TableOfContentsEntry(level, text, $"{baseId}-{count}"));
            }
            else
            {
                seenIds[baseId] = 0;
                entries.Add(new TableOfContentsEntry(level, text, baseId));
            }
        }

        return entries;
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

    private static string SlugifyHeading(string text)
    {
        var normalized = text.ToLowerInvariant();
        var chars = new List<char>(normalized.Length);
        var lastWasDash = false;
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                chars.Add(ch);
                lastWasDash = false;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '-' or '_')
            {
                if (!lastWasDash && chars.Count > 0)
                {
                    chars.Add('-');
                    lastWasDash = true;
                }
            }
        }

        while (chars.Count > 0 && chars[^1] == '-')
        {
            chars.RemoveAt(chars.Count - 1);
        }

        return chars.Count == 0 ? "section" : new string(chars.ToArray());
    }
}
