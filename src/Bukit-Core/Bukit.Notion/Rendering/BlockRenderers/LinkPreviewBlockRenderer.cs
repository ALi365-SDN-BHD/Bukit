using System.Net;
using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

/// <summary>Renders Notion link_preview blocks as links.</summary>
public sealed class LinkPreviewBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("link_preview", out var preview) || preview.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var url = GetString(preview, "url");
        var safeUrl = RenderingSafeUrl.ForLink(url);
        if (string.IsNullOrWhiteSpace(safeUrl))
        {
            return Task.FromResult<string?>(null);
        }

        var encodedUrl = WebUtility.HtmlEncode(safeUrl);
        var rel = RenderingSafeUrl.IsExternal(safeUrl) ? " rel=\"noopener noreferrer\"" : "";
        return Task.FromResult<string?>($"<a href=\"{encodedUrl}\"{rel} class=\"bookmark notion-link-preview\">{encodedUrl}</a>");
    }
}
