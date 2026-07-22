using System.Net;
using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

/// <summary>Renders Notion callout blocks as styled &lt;div&gt; with icon and content.</summary>
public sealed class CalloutBlockRenderer : INotionBlockRenderer
{
    public async Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("callout", out var callout) || callout.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string iconHtml = string.Empty;
        if (callout.TryGetProperty("icon", out var icon) && icon.ValueKind == JsonValueKind.Object)
        {
            var iconType = GetString(icon, "type");
            if (iconType == "emoji" && icon.TryGetProperty("emoji", out var emojiEl) && emojiEl.ValueKind == JsonValueKind.String)
            {
                iconHtml = $"<span class=\"callout-icon\">{WebUtility.HtmlEncode(emojiEl.GetString() ?? string.Empty)}</span>";
            }
            else if (iconType == "external" &&
                     icon.TryGetProperty("external", out var extIcon) &&
                     extIcon.TryGetProperty("url", out var iconUrl) &&
                     iconUrl.ValueKind == JsonValueKind.String)
            {
                iconHtml = $"<span class=\"callout-icon\"><img src=\"{WebUtility.HtmlEncode(iconUrl.GetString() ?? string.Empty)}\" alt=\"\" /></span>";
            }
            else if (iconType == "file" &&
                     icon.TryGetProperty("file", out var fileIcon) &&
                     fileIcon.TryGetProperty("url", out var fileUrl) &&
                     fileUrl.ValueKind == JsonValueKind.String)
            {
                iconHtml = $"<span class=\"callout-icon\"><img src=\"{WebUtility.HtmlEncode(fileUrl.GetString() ?? string.Empty)}\" alt=\"\" /></span>";
            }
            else if (iconType == "custom_emoji" &&
                     icon.TryGetProperty("custom_emoji", out var customEmoji) &&
                     customEmoji.TryGetProperty("url", out var customEmojiUrl) &&
                     customEmojiUrl.ValueKind == JsonValueKind.String)
            {
                iconHtml = $"<span class=\"callout-icon\"><img src=\"{WebUtility.HtmlEncode(customEmojiUrl.GetString() ?? string.Empty)}\" alt=\"\" /></span>";
            }
        }

        var richText = callout.TryGetProperty("rich_text", out var rt) ? rt : default;
        var inner = NotionRichTextRenderer.Render(richText);

        // Read block-level color for callout background
        var color = NotionBlockHelpers.GetBlockColor(callout);
        var cssClasses = "callout";
        if (color is not null)
        {
            cssClasses += $" notion-{WebUtility.HtmlEncode(color)}";
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
                    childrenHtml = $"<div class=\"callout-children\">{renderedChildren}</div>";
                }
            }
        }

        return $"<div class=\"{cssClasses}\">{iconHtml}<div class=\"callout-content\">{inner}{childrenHtml}</div></div>";
    }
}
