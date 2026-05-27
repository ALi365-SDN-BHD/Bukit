using Bukit.Engine.Abstractions.Content;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion to_do blocks as checkbox + text with optional block-level color.</summary>
public sealed class ToDoBlockRenderer : INotionBlockRenderer
{
    public async Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("to_do", out var toDo) || toDo.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var richText = toDo.TryGetProperty("rich_text", out var rt) ? rt : default;
        var inner = NotionRichTextRenderer.Render(richText);
        var isChecked = toDo.TryGetProperty("checked", out var checkedEl) && checkedEl.ValueKind == JsonValueKind.True;
        var checkedAttr = isChecked ? " checked" : string.Empty;

        var color = GetBlockColor(toDo);
        var cssClasses = "to-do";
        if (color is not null)
        {
            cssClasses += $" notion-{color}";
        }

        var childrenHtml = string.Empty;
        var hasChildren = block.TryGetProperty("has_children", out var hc) && hc.ValueKind == JsonValueKind.True;
        if (hasChildren)
        {
            var id = GetString(block, "id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                var renderedChildren = await context.RenderChildrenAsync(id, cancellationToken);
                if (!string.IsNullOrWhiteSpace(renderedChildren))
                {
                    childrenHtml = $"<div class=\"to-do-children\">{renderedChildren}</div>";
                }
            }
        }

        return $"<div class=\"{cssClasses}\"><input type=\"checkbox\" disabled{checkedAttr} /><span>{inner}</span></div>{childrenHtml}";
    }
}
