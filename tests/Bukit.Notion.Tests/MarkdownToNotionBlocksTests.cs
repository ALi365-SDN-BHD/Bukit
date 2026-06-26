using System.Text.Json;
using Bukit.Notion.Conversion;
using Bukit.Notion.Client;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class MarkdownToNotionBlocksTests
{
    [Fact]
    public void Convert_CreatesStructuredBlocksForCommonMarkdown()
    {
        IReadOnlyList<NotionBlock> blocks = MarkdownToNotionBlocks.Convert("""
# Heading 1

## Heading 2

- Bullet
1. Numbered
> Quote

```csharp
Console.WriteLine("hello");
```
""");

        Assert.Equal(
            ["heading_1", "heading_2", "bulleted_list_item", "numbered_list_item", "quote", "code"],
            blocks.Select(ReadBlockType).ToArray());
        using JsonDocument code = JsonDocument.Parse(blocks[^1].Json);
        Assert.Equal("csharp", code.RootElement.GetProperty("code").GetProperty("language").GetString());
    }

    [Fact]
    public void Convert_ChunksLongParagraphsAtNotionTextLimit()
    {
        string content = new('a', 2_500);

        IReadOnlyList<NotionBlock> blocks = MarkdownToNotionBlocks.Convert(content);

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block => Assert.Equal("paragraph", ReadBlockType(block)));
        Assert.Equal(2_000, ReadTextContent(blocks[0]).Length);
        Assert.Equal(500, ReadTextContent(blocks[1]).Length);
    }

    private static string ReadBlockType(NotionBlock block)
    {
        using JsonDocument document = JsonDocument.Parse(block.Json);
        return document.RootElement.GetProperty("type").GetString()!;
    }

    private static string ReadTextContent(NotionBlock block)
    {
        using JsonDocument document = JsonDocument.Parse(block.Json);
        JsonElement root = document.RootElement;
        string type = root.GetProperty("type").GetString()!;
        JsonElement richText = root.GetProperty(type).GetProperty("rich_text")[0];
        return richText.GetProperty("text").GetProperty("content").GetString()!;
    }
}
