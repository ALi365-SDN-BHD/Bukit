using NewBlocks = Bukit.Notion.Blocks;

namespace Bukit.Shared.Notion;

internal static class NotionCompatibilityMapper
{
    internal static NotionBlock ToLegacy(NewBlocks.NotionBlock block)
        => block switch
        {
            NewBlocks.Heading1Block value => new Heading1Block(value.Text),
            NewBlocks.Heading2Block value => new Heading2Block(value.Text),
            NewBlocks.Heading3Block value => new Heading3Block(value.Text),
            NewBlocks.ParagraphBlock value => new ParagraphBlock([.. value.Segments.Select(ToLegacy)]),
            NewBlocks.BulletedListItemBlock value => new BulletedListItemBlock([.. value.Segments.Select(ToLegacy)]),
            NewBlocks.NumberedListItemBlock value => new NumberedListItemBlock([.. value.Segments.Select(ToLegacy)]),
            NewBlocks.QuoteBlock value => new QuoteBlock([.. value.Segments.Select(ToLegacy)]),
            NewBlocks.ImageBlock value => new ImageBlock(value.Url, value.Caption),
            NewBlocks.ToggleBlock value => new ToggleBlock(value.Heading, [.. value.Children.Select(ToLegacy)]),
            NewBlocks.CodeBlock value => new CodeBlock(value.Code, value.Language),
            NewBlocks.CalloutBlock value => new CalloutBlock(value.Text, value.Icon),
            _ => throw new NotSupportedException($"Unsupported Notion block type: {block.GetType().FullName}")
        };

    internal static NewBlocks.NotionBlock ToIndependent(NotionBlock block)
        => block switch
        {
            Heading1Block value => new NewBlocks.Heading1Block(value.Text),
            Heading2Block value => new NewBlocks.Heading2Block(value.Text),
            Heading3Block value => new NewBlocks.Heading3Block(value.Text),
            ParagraphBlock value => new NewBlocks.ParagraphBlock([.. value.Segments.Select(ToIndependent)]),
            BulletedListItemBlock value => new NewBlocks.BulletedListItemBlock([.. value.Segments.Select(ToIndependent)]),
            NumberedListItemBlock value => new NewBlocks.NumberedListItemBlock([.. value.Segments.Select(ToIndependent)]),
            QuoteBlock value => new NewBlocks.QuoteBlock([.. value.Segments.Select(ToIndependent)]),
            ImageBlock value => new NewBlocks.ImageBlock(value.Url, value.Caption),
            ToggleBlock value => new NewBlocks.ToggleBlock(value.Heading, [.. value.Children.Select(ToIndependent)]),
            CodeBlock value => new NewBlocks.CodeBlock(value.Code, value.Language),
            CalloutBlock value => new NewBlocks.CalloutBlock(value.Text, value.Icon),
            _ => throw new NotSupportedException($"Unsupported Notion block type: {block.GetType().FullName}")
        };

    private static RichTextSegment ToLegacy(NewBlocks.RichTextSegment segment)
        => new(segment.Text, segment.Bold, segment.Italic, segment.LinkUrl);

    private static NewBlocks.RichTextSegment ToIndependent(RichTextSegment segment)
        => new(segment.Text, segment.Bold, segment.Italic, segment.LinkUrl);
}
