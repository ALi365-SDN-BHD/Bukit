using System.Text.Json;

namespace Bukit.Content.Notion;

public static class NotionRichTextRenderer
{
    public static string Render(JsonElement richTextArray)
        => Bukit.Notion.Rendering.NotionRichTextRenderer.Render(richTextArray);
}
