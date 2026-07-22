using System.Net;
using System.Text;
using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

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
            var safeEmbed = RenderingSafeUrl.ForEmbed(embedUrl);
            if (string.IsNullOrWhiteSpace(safeEmbed))
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>($"<div class=\"video-embed\"><iframe src=\"{WebUtility.HtmlEncode(safeEmbed)}\" frameborder=\"0\" allowfullscreen></iframe></div>");
        }

        var safeUrl = RenderingSafeUrl.ForMedia(url);
        if (string.IsNullOrWhiteSpace(safeUrl))
        {
            return Task.FromResult<string?>(null);
        }

        var captionText = video.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;
        var sb = new StringBuilder();
        sb.Append("<video src=\"").Append(WebUtility.HtmlEncode(safeUrl)).Append("\" controls></video>");
        if (!string.IsNullOrWhiteSpace(captionText))
        {
            sb.Append("<p>").Append(captionText).Append("</p>");
        }

        return Task.FromResult<string?>(sb.ToString());
    }
}
