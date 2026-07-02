using Bukit.Engine.Abstractions.Content;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion synced_block by rendering its current children.</summary>
public sealed class SyncedBlockRenderer : INotionBlockRenderer
{
    public async Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("synced_block", out var _) || block.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var hasChildren = block.TryGetProperty("has_children", out var hc) && hc.ValueKind == JsonValueKind.True;
        if (!hasChildren)
        {
            return null;
        }

        var id = GetString(block, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var childrenHtml = await context.RenderChildrenAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(childrenHtml))
        {
            return null;
        }

        return $"<div class=\"notion-synced-block\">{childrenHtml}</div>";
    }
}
