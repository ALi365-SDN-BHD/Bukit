using System.Net;
using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

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
        var safeUrl = RenderingSafeUrl.ForLink(url);
        if (string.IsNullOrWhiteSpace(safeUrl))
        {
            return Task.FromResult<string?>(null);
        }

        var captionText = bookmark.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;
        var linkText = string.IsNullOrWhiteSpace(captionText) ? WebUtility.HtmlEncode(safeUrl) : captionText;

        var color = GetBlockColor(bookmark);
        var cssClasses = color is not null ? $"bookmark notion-{WebUtility.HtmlEncode(color)}" : "bookmark";

        var rel = RenderingSafeUrl.IsExternal(safeUrl) ? " rel=\"noopener noreferrer\"" : "";
        return Task.FromResult<string?>($"<a href=\"{WebUtility.HtmlEncode(safeUrl)}\"{rel} class=\"{cssClasses}\">{linkText}</a>");
    }
}
