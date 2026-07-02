using Bukit.Engine.Abstractions.Content;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion column block as a column container.</summary>
public sealed class ColumnBlockRenderer : INotionBlockRenderer
{
    public async Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("column", out var column) || block.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Read optional width_ratio (Notion column property, number between 0 and 1)
        double? widthRatio = null;
        if (column.TryGetProperty("width_ratio", out var wr) && wr.ValueKind == JsonValueKind.Number)
        {
            widthRatio = wr.GetDouble();
        }

        var styleAttr = widthRatio.HasValue && widthRatio.Value > 0 && widthRatio.Value <= 1
            ? $" style=\"flex:0 0 {widthRatio.Value * 100:F0}%\""
            : string.Empty;

        var hasChildren = block.TryGetProperty("has_children", out var hc) && hc.ValueKind == JsonValueKind.True;
        if (!hasChildren)
        {
            return $"<div class=\"notion-column\"{styleAttr}></div>";
        }

        var id = GetString(block, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return $"<div class=\"notion-column\"{styleAttr}></div>";
        }

        var childrenHtml = await context.RenderChildrenAsync(id, cancellationToken);
        return $"<div class=\"notion-column\"{styleAttr}>{childrenHtml}</div>";
    }
}
