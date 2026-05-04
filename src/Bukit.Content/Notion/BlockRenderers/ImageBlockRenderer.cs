using System.Net;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion image blocks as &lt;img&gt; or &lt;figure&gt;.</summary>
public sealed class ImageBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("image", out var image) || image.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var url = ExtractFileUrl(image);
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<string?>(null);
        }

        var captionText = image.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;
        var img = $"<img src=\"{WebUtility.HtmlEncode(url)}\" alt=\"\" />";
        if (string.IsNullOrWhiteSpace(captionText))
        {
            return Task.FromResult<string?>(img);
        }

        return Task.FromResult<string?>($"<figure>{img}<figcaption>{captionText}</figcaption></figure>");
    }
}
