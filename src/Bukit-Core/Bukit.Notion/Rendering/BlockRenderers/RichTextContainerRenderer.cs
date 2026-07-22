using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

/// <summary>
/// Renders blocks that contain rich_text as a simple HTML container tag
/// (paragraph, heading_1/2/3, quote, etc.).
/// Supports block-level color via <c>class="notion-{color}"</c>.
/// </summary>
public sealed class RichTextContainerRenderer(string containerName, string tag) : INotionBlockRenderer
{
    private readonly string _containerName = containerName;
    private readonly string _tag = tag;

    public async Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty(_containerName, out var container) || container.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Check for heading is_toggleable support
        if (_tag is "h1" or "h2" or "h3")
        {
            var isToggleable = container.TryGetProperty("is_toggleable", out var togEl) && togEl.ValueKind == JsonValueKind.True;
            var hasChildren = block.TryGetProperty("has_children", out var hcEl) && hcEl.ValueKind == JsonValueKind.True;

            if (isToggleable && hasChildren)
            {
                return await RenderToggleableHeadingAsync(block, container, context, cancellationToken);
            }
        }

        if (!container.TryGetProperty("rich_text", out var richText))
        {
            return null;
        }

        var inner = NotionRichTextRenderer.Render(richText);
        if (string.IsNullOrWhiteSpace(inner))
        {
            return null;
        }

        var colorClass = GetBlockColorClass(container);
        var currentBlock = $"<{_tag}{colorClass}>{inner}</{_tag}>";

        // paragraph/quote can contain nested children in Notion.
        if (_tag is "p" or "blockquote")
        {
            var childrenHtml = await RenderChildrenIfAnyAsync(block, context, cancellationToken);
            if (!string.IsNullOrWhiteSpace(childrenHtml))
            {
                if (_tag == "blockquote")
                {
                    return $"<{_tag}{colorClass}>{inner}{childrenHtml}</{_tag}>";
                }

                return $"{currentBlock}{childrenHtml}";
            }
        }

        return currentBlock;
    }

    private async Task<string?> RenderToggleableHeadingAsync(
        JsonElement block, JsonElement container, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!container.TryGetProperty("rich_text", out var richText))
        {
            return null;
        }

        var inner = NotionRichTextRenderer.Render(richText);
        if (string.IsNullOrWhiteSpace(inner))
        {
            return null;
        }

        var colorClass = GetBlockColorClass(container);
        var childrenHtml = string.Empty;
        var id = GetString(block, "id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            childrenHtml = await context.RenderChildrenAsync(id, cancellationToken);
        }

        return $"<details{colorClass}><summary><{_tag}>{inner}</{_tag}></summary>{childrenHtml}</details>";
    }

    private static async Task<string> RenderChildrenIfAnyAsync(
        JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        var hasChildren = block.TryGetProperty("has_children", out var hc) && hc.ValueKind == JsonValueKind.True;
        if (!hasChildren)
        {
            return string.Empty;
        }

        var id = GetString(block, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        return await context.RenderChildrenAsync(id, cancellationToken);
    }
}
