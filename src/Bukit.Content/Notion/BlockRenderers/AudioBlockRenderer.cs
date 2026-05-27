using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion audio blocks as HTML5 audio with link fallback.</summary>
public sealed class AudioBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("audio", out var audio) || audio.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var url = ExtractFileUrl(audio);
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<string?>(null);
        }

        var encodedUrl = WebUtility.HtmlEncode(url);
        var captionText = audio.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;

        var sb = new StringBuilder();
        sb.Append($"<audio controls src=\"{encodedUrl}\"></audio>");
        sb.Append($"<p><a href=\"{encodedUrl}\">Audio</a></p>");
        if (!string.IsNullOrWhiteSpace(captionText))
        {
            sb.Append($"<p>{captionText}</p>");
        }

        return Task.FromResult<string?>(sb.ToString());
    }
}
