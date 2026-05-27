using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion video blocks as &lt;video&gt; or YouTube &lt;iframe&gt;.</summary>
public sealed class VideoBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("video", out var video) || video.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var url = ExtractFileUrl(video);
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<string?>(null);
        }

        if (IsYouTubeUrl(url, out var embedUrl))
        {
            return Task.FromResult<string?>($"<div class=\"video-embed\"><iframe src=\"{WebUtility.HtmlEncode(embedUrl)}\" frameborder=\"0\" allowfullscreen></iframe></div>");
        }

        var captionText = video.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;
        var sb = new StringBuilder();
        sb.Append("<video src=\"").Append(WebUtility.HtmlEncode(url)).Append("\" controls></video>");
        if (!string.IsNullOrWhiteSpace(captionText))
        {
            sb.Append("<p>").Append(captionText).Append("</p>");
        }

        return Task.FromResult<string?>(sb.ToString());
    }
}
