using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion toggle blocks as &lt;details&gt;/&lt;summary&gt;.</summary>
public sealed class ToggleBlockRenderer : INotionBlockRenderer
{
    public async Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("toggle", out var toggle) || toggle.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var richText = toggle.TryGetProperty("rich_text", out var rt) ? rt : default;
        var summary = NotionRichTextRenderer.Render(richText);

        var hasChildren = block.TryGetProperty("has_children", out var hc) && hc.ValueKind == JsonValueKind.True;
        var childrenHtml = string.Empty;
        if (hasChildren)
        {
            var id = GetString(block, "id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                childrenHtml = await context.RenderChildrenAsync(id, cancellationToken);
            }
        }

        var color = NotionBlockHelpers.GetBlockColor(toggle);
        var colorClass = color is not null ? $" class=\"notion-{color}\"" : string.Empty;

        return $"<details{colorClass}><summary>{summary}</summary>{childrenHtml}</details>";
    }
}
