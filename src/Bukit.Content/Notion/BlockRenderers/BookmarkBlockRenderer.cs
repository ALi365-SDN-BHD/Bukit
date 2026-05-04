using System.Net;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion bookmark blocks as styled &lt;a&gt; links.</summary>
public sealed class BookmarkBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("bookmark", out var bookmark) || bookmark.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var url = GetString(bookmark, "url");
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<string?>(null);
        }

        var captionText = bookmark.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;
        var linkText = string.IsNullOrWhiteSpace(captionText) ? WebUtility.HtmlEncode(url) : captionText;

        var color = GetBlockColor(bookmark);
        var cssClasses = color is not null ? $"bookmark notion-{color}" : "bookmark";

        return Task.FromResult<string?>($"<a href=\"{WebUtility.HtmlEncode(url)}\" class=\"{cssClasses}\">{linkText}</a>");
    }
}
