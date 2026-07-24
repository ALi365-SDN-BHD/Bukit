using System.Text.Json;
using Bukit.Notion.Blocks;
using Bukit.Notion.Conversion;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionBlockJsonWriterTests
{
    [Fact]
    public void SerializeBlocks_WritesBoldAndItalicAnnotationsWithoutNesting()
    {
        var json = NotionBlockJsonWriter.SerializeBlocks(
            [
                new ParagraphBlock(
                    [
                        new RichTextSegment("Bold", Bold: true),
                        new RichTextSegment("Italic", Italic: true)
                    ])
            ]);

        using var document = JsonDocument.Parse(json);
        var block = Assert.Single(document.RootElement.EnumerateArray());
        var richText = block.GetProperty("paragraph").GetProperty("rich_text").EnumerateArray().ToArray();

        Assert.Equal(2, richText.Length);
        AssertRichTextAnnotation(richText[0], bold: true, italic: false);
        AssertRichTextAnnotation(richText[1], bold: false, italic: true);
    }

    [Fact]
    public void SerializeBlocks_WritesSupportedDirectBlockShapes()
    {
        var json = NotionBlockJsonWriter.SerializeBlocks(
            [
                new Heading1Block(new string('H', 2100)),
                new BulletedListItemBlock("Bullet"),
                new NumberedListItemBlock("Number"),
                new ImageBlock("https://example.com/hero.png", "Hero"),
                new ToggleBlock("Question", [new ParagraphBlock("Answer")]),
                new CodeBlock("Console.WriteLine(\"hi\");", "csharp"),
                new CalloutBlock("Heads up", "💡")
            ]);

        using var document = JsonDocument.Parse(json);
        var blocks = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(7, blocks.Length);
        Assert.Equal("heading_1", blocks[0].GetProperty("type").GetString());
        Assert.Equal(2000, GetSingleRichTextContent(blocks[0], "heading_1").Length);
        Assert.EndsWith("...", GetSingleRichTextContent(blocks[0], "heading_1"));

        Assert.Equal("bulleted_list_item", blocks[1].GetProperty("type").GetString());
        Assert.Equal("Bullet", GetSingleRichTextContent(blocks[1], "bulleted_list_item"));

        Assert.Equal("numbered_list_item", blocks[2].GetProperty("type").GetString());
        Assert.Equal("Number", GetSingleRichTextContent(blocks[2], "numbered_list_item"));

        Assert.Equal("image", blocks[3].GetProperty("type").GetString());
        Assert.Equal("https://example.com/hero.png",
            blocks[3].GetProperty("image").GetProperty("external").GetProperty("url").GetString());
        Assert.Equal("Hero", GetCaptionContent(blocks[3]));

        Assert.Equal("toggle", blocks[4].GetProperty("type").GetString());
        Assert.Equal("Question", GetSingleRichTextContent(blocks[4], "toggle"));
        var toggleChildren = blocks[4].GetProperty("toggle").GetProperty("children").EnumerateArray().ToArray();
        Assert.Single(toggleChildren);
        Assert.Equal("paragraph", toggleChildren[0].GetProperty("type").GetString());
        Assert.Equal("Answer", GetSingleRichTextContent(toggleChildren[0], "paragraph"));

        Assert.Equal("code", blocks[5].GetProperty("type").GetString());
        Assert.Equal("Console.WriteLine(\"hi\");", GetSingleRichTextContent(blocks[5], "code"));
        Assert.Equal("csharp", blocks[5].GetProperty("code").GetProperty("language").GetProperty("name").GetString());

        Assert.Equal("callout", blocks[6].GetProperty("type").GetString());
        Assert.Equal("Heads up", GetSingleRichTextContent(blocks[6], "callout"));
        Assert.Equal("💡", blocks[6].GetProperty("callout").GetProperty("icon").GetProperty("emoji").GetString());
    }

    [Fact]
    public void TruncateBlockText_LimitsValuesToNotionMaxLength()
    {
        var truncated = NotionBlockJsonWriter.TruncateBlockText(new string('x', 2100));

        Assert.Equal(2000, truncated.Length);
        Assert.EndsWith("...", truncated);
    }

    [Fact]
    public void SerializeBlocks_WritesSecondaryHeadingQuoteAndLinkSegments()
    {
        var json = NotionBlockJsonWriter.SerializeBlocks(
            [
                new Heading2Block("Heading 2"),
                new Heading3Block("Heading 3"),
                new QuoteBlock("Quoted text"),
                new ParagraphBlock([new RichTextSegment("Docs", LinkUrl: "https://example.com/docs")])
            ]);

        using var document = JsonDocument.Parse(json);
        var blocks = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(4, blocks.Length);
        Assert.Equal("heading_2", blocks[0].GetProperty("type").GetString());
        Assert.Equal("Heading 2", GetSingleRichTextContent(blocks[0], "heading_2"));

        Assert.Equal("heading_3", blocks[1].GetProperty("type").GetString());
        Assert.Equal("Heading 3", GetSingleRichTextContent(blocks[1], "heading_3"));

        Assert.Equal("quote", blocks[2].GetProperty("type").GetString());
        Assert.Equal("Quoted text", GetSingleRichTextContent(blocks[2], "quote"));

        Assert.Equal("paragraph", blocks[3].GetProperty("type").GetString());
        Assert.Equal("Docs", GetSingleRichTextContent(blocks[3], "paragraph"));
        var paragraphSegment = blocks[3]
            .GetProperty("paragraph")
            .GetProperty("rich_text")
            .EnumerateArray()
            .Single();
        Assert.Equal(
            "https://example.com/docs",
            paragraphSegment.GetProperty("text").GetProperty("link").GetProperty("url").GetString());
    }

    [Fact]
    public void ToBlocksJson_ConvertsAndSerializesBlocksEndToEnd()
    {
        const string html = "<h3>Nested</h3><p>Alpha <a href=\"https://example.com\">Beta</a></p>";

        var json = HtmlToNotionBlockConverter.ToBlocksJson(html);

        using var document = JsonDocument.Parse(json);
        var blocks = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(2, blocks.Length);
        Assert.Equal("heading_3", blocks[0].GetProperty("type").GetString());
        Assert.Equal("Nested", GetSingleRichTextContent(blocks[0], "heading_3"));

        Assert.Equal("paragraph", blocks[1].GetProperty("type").GetString());
        var segments = blocks[1].GetProperty("paragraph").GetProperty("rich_text").EnumerateArray().ToArray();
        Assert.Equal(2, segments.Length);
        Assert.Equal("Alpha", segments[0].GetProperty("text").GetProperty("content").GetString());
        Assert.Equal("Beta", segments[1].GetProperty("text").GetProperty("content").GetString());
        Assert.Equal(
            "https://example.com",
            segments[1].GetProperty("text").GetProperty("link").GetProperty("url").GetString());
    }

    private static void AssertRichTextAnnotation(JsonElement element, bool bold, bool italic)
    {
        var annotations = element.GetProperty("annotations");
        Assert.False(annotations.TryGetProperty("annotations", out _));
        Assert.Equal(bold, annotations.GetProperty("bold").GetBoolean());
        Assert.Equal(italic, annotations.GetProperty("italic").GetBoolean());
        Assert.False(annotations.GetProperty("strikethrough").GetBoolean());
        Assert.False(annotations.GetProperty("underline").GetBoolean());
        Assert.False(annotations.GetProperty("code").GetBoolean());
        Assert.Equal("default", annotations.GetProperty("color").GetString());
    }

    private static string GetSingleRichTextContent(JsonElement block, string blockType)
    {
        var richText = block.GetProperty(blockType).GetProperty("rich_text").EnumerateArray().ToArray();
        return Assert.Single(richText).GetProperty("text").GetProperty("content").GetString()!;
    }

    private static string GetCaptionContent(JsonElement imageBlock)
    {
        var caption = imageBlock.GetProperty("image").GetProperty("caption").EnumerateArray().ToArray();
        return Assert.Single(caption).GetProperty("text").GetProperty("content").GetString()!;
    }
}
