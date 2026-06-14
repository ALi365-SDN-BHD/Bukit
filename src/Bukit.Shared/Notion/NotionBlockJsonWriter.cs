using System.Text;
using System.Text.Json;

namespace Bukit.Shared.Notion;

internal static class NotionBlockJsonWriter
{
    internal static string SerializeBlocks(List<NotionBlock> blocks)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartArray();
        foreach (var block in blocks)
            WriteBlock(writer, block);
        writer.WriteEndArray();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteBlock(Utf8JsonWriter writer, NotionBlock block)
    {
        switch (block)
        {
            case Heading1Block h1:
                WriteHeadingBlock(writer, "heading_1", h1.Text);
                break;
            case Heading2Block h2:
                WriteHeadingBlock(writer, "heading_2", h2.Text);
                break;
            case Heading3Block h3:
                WriteHeadingBlock(writer, "heading_3", h3.Text);
                break;
            case ParagraphBlock p:
                WriteRichTextBlock(writer, "paragraph", p.Segments);
                break;
            case BulletedListItemBlock li:
                WriteRichTextBlock(writer, "bulleted_list_item", li.Segments);
                break;
            case NumberedListItemBlock li:
                WriteRichTextBlock(writer, "numbered_list_item", li.Segments);
                break;
            case QuoteBlock q:
                WriteRichTextBlock(writer, "quote", q.Segments);
                break;
            case ImageBlock img:
                writer.WriteStartObject();
                writer.WriteString("object", "block");
                writer.WriteString("type", "image");
                writer.WriteStartObject("image");
                writer.WriteString("type", "external");
                writer.WriteStartObject("external");
                writer.WriteString("url", img.Url);
                writer.WriteEndObject();
                if (!string.IsNullOrWhiteSpace(img.Caption))
                {
                    writer.WriteStartArray("caption");
                    WriteTextObject(writer, img.Caption);
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case ToggleBlock toggle:
                writer.WriteStartObject();
                writer.WriteString("object", "block");
                writer.WriteString("type", "toggle");
                writer.WriteStartObject("toggle");
                writer.WriteStartArray("rich_text");
                WriteTextObject(writer, toggle.Heading);
                writer.WriteEndArray();
                if (toggle.Children.Count > 0)
                {
                    writer.WriteStartArray("children");
                    foreach (var child in toggle.Children)
                        WriteBlock(writer, child);
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case CodeBlock code:
                writer.WriteStartObject();
                writer.WriteString("object", "block");
                writer.WriteString("type", "code");
                writer.WriteStartObject("code");
                writer.WriteStartArray("rich_text");
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteStartObject("text");
                writer.WriteString("content", TruncateBlockText(code.Code));
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteStartObject("language");
                writer.WriteString("name", code.Language);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case CalloutBlock callout:
                writer.WriteStartObject();
                writer.WriteString("object", "block");
                writer.WriteString("type", "callout");
                writer.WriteStartObject("callout");
                writer.WriteStartArray("rich_text");
                WriteTextObject(writer, callout.Text);
                writer.WriteEndArray();
                writer.WriteStartObject("icon");
                writer.WriteString("type", "emoji");
                writer.WriteString("emoji", callout.Icon);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
        }
    }

    private static void WriteHeadingBlock(Utf8JsonWriter writer, string type, string text)
    {
        writer.WriteStartObject();
        writer.WriteString("object", "block");
        writer.WriteString("type", type);
        writer.WriteStartObject(type);
        writer.WriteStartArray("rich_text");
        WriteTextObject(writer, TruncateBlockText(text));
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteRichTextBlock(Utf8JsonWriter writer, string type, List<RichTextSegment> segments)
    {
        writer.WriteStartObject();
        writer.WriteString("object", "block");
        writer.WriteString("type", type);
        writer.WriteStartObject(type);
        writer.WriteStartArray("rich_text");
        foreach (var seg in segments)
            WriteRichTextSegment(writer, seg);
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteRichTextSegment(Utf8JsonWriter writer, RichTextSegment seg)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteStartObject("text");
        writer.WriteString("content", TruncateBlockText(seg.Text));
        if (seg.LinkUrl != null)
        {
            writer.WriteStartObject("link");
            writer.WriteString("url", seg.LinkUrl);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        if (seg.Bold || seg.Italic)
        {
            writer.WriteStartObject("annotations");
            writer.WriteBoolean("bold", seg.Bold);
            writer.WriteBoolean("italic", seg.Italic);
            writer.WriteBoolean("strikethrough", false);
            writer.WriteBoolean("underline", false);
            writer.WriteBoolean("code", false);
            writer.WriteString("color", "default");
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    internal static string TruncateBlockText(string text)
        => text.Length <= 2000 ? text : text[..1997] + "...";

    private static void WriteTextObject(Utf8JsonWriter writer, string value)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteStartObject("text");
        writer.WriteString("content", TruncateBlockText(value));
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
