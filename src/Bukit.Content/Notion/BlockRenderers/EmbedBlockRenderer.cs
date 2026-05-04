using System.Net;
using System.Text;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion embed blocks as &lt;iframe&gt; or YouTube embed.</summary>
public sealed class EmbedBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("embed", out var embed) || embed.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var url = GetString(embed, "url");
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<string?>(null);
        }

        var captionText = embed.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;

        if (IsYouTubeUrl(url, out var embedUrl))
        {
            return Task.FromResult<string?>($"<div class=\"video-embed\"><iframe src=\"{WebUtility.HtmlEncode(embedUrl)}\" frameborder=\"0\" allowfullscreen></iframe></div>");
        }

        var sb = new StringBuilder();
        sb.Append("<figure><iframe src=\"").Append(WebUtility.HtmlEncode(url)).Append("\" frameborder=\"0\"></iframe>");
        if (!string.IsNullOrWhiteSpace(captionText))
        {
            sb.Append("<figcaption>").Append(captionText).Append("</figcaption>");
        }

        sb.Append("</figure>");
        return Task.FromResult<string?>(sb.ToString());
    }
}
