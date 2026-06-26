using System.Text.Json.Nodes;
using Bukit.Notion.Client;

namespace Bukit.Notion.Conversion;

public static class MarkdownToNotionBlocks
{
    public static IReadOnlyList<NotionBlock> Convert(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        string[] lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var blocks = new List<NotionBlock>();
        var paragraph = new List<string>();

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(paragraph, blocks);
                continue;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(paragraph, blocks);
                string language = line.Length > 3 ? line[3..].Trim() : string.Empty;
                var codeLines = new List<string>();
                index++;
                while (index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal))
                {
                    codeLines.Add(lines[index]);
                    index++;
                }

                AddTextBlocks(blocks, "code", string.Join('\n', codeLines), string.IsNullOrWhiteSpace(language) ? "plain text" : language);
                continue;
            }

            if (IsStructuredLine(line))
            {
                FlushParagraph(paragraph, blocks);
                TryAddLineBlock(line, blocks);
                continue;
            }

            paragraph.Add(line.Trim());
        }

        FlushParagraph(paragraph, blocks);
        return blocks;
    }

    private static bool TryAddLineBlock(string line, List<NotionBlock> blocks)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("# ", StringComparison.Ordinal))
        {
            AddTextBlocks(blocks, "heading_1", trimmed[2..].Trim());
            return true;
        }

        if (trimmed.StartsWith("## ", StringComparison.Ordinal))
        {
            AddTextBlocks(blocks, "heading_2", trimmed[3..].Trim());
            return true;
        }

        if (trimmed.StartsWith("### ", StringComparison.Ordinal))
        {
            AddTextBlocks(blocks, "heading_3", trimmed[4..].Trim());
            return true;
        }

        if (trimmed.StartsWith("- ", StringComparison.Ordinal))
        {
            AddTextBlocks(blocks, "bulleted_list_item", trimmed[2..].Trim());
            return true;
        }

        if (TryReadNumberedListItem(trimmed, out string? numberedText))
        {
            AddTextBlocks(blocks, "numbered_list_item", numberedText);
            return true;
        }

        if (trimmed.StartsWith("> ", StringComparison.Ordinal))
        {
            AddTextBlocks(blocks, "quote", trimmed[2..].Trim());
            return true;
        }

        return false;
    }

    private static bool IsStructuredLine(string line)
    {
        string trimmed = line.TrimStart();
        return trimmed.StartsWith("# ", StringComparison.Ordinal)
            || trimmed.StartsWith("## ", StringComparison.Ordinal)
            || trimmed.StartsWith("### ", StringComparison.Ordinal)
            || trimmed.StartsWith("- ", StringComparison.Ordinal)
            || trimmed.StartsWith("> ", StringComparison.Ordinal)
            || TryReadNumberedListItem(trimmed, out _);
    }

    private static bool TryReadNumberedListItem(string line, out string text)
    {
        text = string.Empty;
        int dotIndex = line.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex <= 0 || dotIndex == line.Length - 1)
        {
            return false;
        }

        for (int index = 0; index < dotIndex; index++)
        {
            if (!char.IsDigit(line[index]))
            {
                return false;
            }
        }

        if (line[dotIndex + 1] != ' ')
        {
            return false;
        }

        text = line[(dotIndex + 2)..].Trim();
        return !string.IsNullOrWhiteSpace(text);
    }

    private static void FlushParagraph(List<string> paragraph, List<NotionBlock> blocks)
    {
        if (paragraph.Count == 0)
        {
            return;
        }

        AddTextBlocks(blocks, "paragraph", string.Join(' ', paragraph));
        paragraph.Clear();
    }

    private static void AddTextBlocks(List<NotionBlock> blocks, string type, string content, string? codeLanguage = null)
    {
        foreach (string chunk in NotionTextChunker.Chunk(content))
        {
            if (string.IsNullOrWhiteSpace(chunk))
            {
                continue;
            }

            blocks.Add(new NotionBlock(CreateTextBlock(type, chunk, codeLanguage).ToJsonString()));
        }
    }

    private static JsonObject CreateTextBlock(string type, string content, string? codeLanguage)
    {
        var typeObject = new JsonObject
        {
            ["rich_text"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = new JsonObject
                    {
                        ["content"] = content
                    }
                }
            }
        };
        if (type == "code")
        {
            typeObject["language"] = codeLanguage ?? "plain text";
        }

        return new JsonObject
        {
            ["object"] = "block",
            ["type"] = type,
            [type] = typeObject
        };
    }
}
