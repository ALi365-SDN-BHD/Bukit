namespace Bukit.Notion.Conversion;

public static class NotionTextChunker
{
    public const int MaxTextContentLength = 2_000;
    public const int MaxRichTextArrayItems = 100;
    public const int MaxRichTextPropertyLength = MaxTextContentLength * MaxRichTextArrayItems;

    public static IReadOnlyList<string> Chunk(string content, int maxLength = MaxTextContentLength)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        var chunks = new List<string>();
        for (int index = 0; index < content.Length; index += maxLength)
        {
            int length = Math.Min(maxLength, content.Length - index);
            chunks.Add(content.Substring(index, length));
        }

        return chunks;
    }
}
