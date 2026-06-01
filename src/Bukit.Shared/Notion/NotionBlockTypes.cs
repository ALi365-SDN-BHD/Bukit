namespace Bukit.Shared.Notion;

public abstract record NotionBlock;

public sealed record Heading1Block(string Text) : NotionBlock;
public sealed record Heading2Block(string Text) : NotionBlock;
public sealed record Heading3Block(string Text) : NotionBlock;
public sealed record ParagraphBlock(List<RichTextSegment> Segments) : NotionBlock
{
    public ParagraphBlock(string text) : this([new RichTextSegment(text)]) { }
}
public sealed record BulletedListItemBlock(List<RichTextSegment> Segments) : NotionBlock
{
    public BulletedListItemBlock(string text) : this([new RichTextSegment(text)]) { }
}
public sealed record NumberedListItemBlock(List<RichTextSegment> Segments) : NotionBlock
{
    public NumberedListItemBlock(string text) : this([new RichTextSegment(text)]) { }
}
public sealed record QuoteBlock(List<RichTextSegment> Segments) : NotionBlock
{
    public QuoteBlock(string text) : this([new RichTextSegment(text)]) { }
}
public sealed record ImageBlock(string Url, string? Caption = null) : NotionBlock;
public sealed record ToggleBlock(string Heading, List<NotionBlock> Children) : NotionBlock;
public sealed record CodeBlock(string Code, string Language = "plain text") : NotionBlock;
public sealed record CalloutBlock(string Text, string Icon = "📝") : NotionBlock;

public sealed record RichTextSegment(
    string Text,
    bool Bold = false,
    bool Italic = false,
    string? LinkUrl = null);
