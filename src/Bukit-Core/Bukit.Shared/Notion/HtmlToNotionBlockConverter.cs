namespace Bukit.Shared.Notion;

public static class HtmlToNotionBlockConverter
{
    public static string ToBlocksJson(string html)
        => Bukit.Notion.Conversion.HtmlToNotionBlockConverter.ToBlocksJson(html);

    public static List<NotionBlock> Convert(string html)
        =>
        [
            .. Bukit.Notion.Conversion.HtmlToNotionBlockConverter.Convert(html)
                .Select(NotionCompatibilityMapper.ToLegacy)
        ];
}
