namespace Bukit.Shared.Notion;

internal static class NotionBlockJsonWriter
{
    internal static string SerializeBlocks(List<NotionBlock> blocks)
        => Bukit.Notion.Conversion.NotionBlockJsonWriter.SerializeBlocks(
            blocks.Select(NotionCompatibilityMapper.ToIndependent).ToList());

    internal static string TruncateBlockText(string text)
        => Bukit.Notion.Conversion.NotionBlockJsonWriter.TruncateBlockText(text);
}
