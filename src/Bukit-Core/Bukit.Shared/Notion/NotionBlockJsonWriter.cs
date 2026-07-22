namespace Bukit.Shared.Notion;

internal static class NotionBlockJsonWriter
{
    internal static string SerializeBlocks(List<NotionBlock> blocks)
    {
        List<Bukit.Notion.Blocks.NotionBlock> independentBlocks =
        [
            .. blocks.Select(NotionCompatibilityMapper.ToIndependent)
        ];
        return Bukit.Notion.Conversion.NotionBlockJsonWriter.SerializeBlocks(independentBlocks);
    }

    internal static string TruncateBlockText(string text)
        => Bukit.Notion.Conversion.NotionBlockJsonWriter.TruncateBlockText(text);
}
