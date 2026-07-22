using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

/// <summary>Renders Notion column_list by rendering child column blocks.</summary>
public sealed class ColumnListBlockRenderer : INotionBlockRenderer
{
    public async Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("column_list", out var _) || block.ValueKind != JsonValueKind.Object)
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

        return $"<div class=\"notion-columns\">{childrenHtml}</div>";
    }
}
