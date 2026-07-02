using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using System.Net;
using System.Text;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

public sealed class AudioBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("audio", out var audio) || audio.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var url = ExtractFileUrl(audio);
        var safeUrl = SafeUrl.ForMedia(url);
        if (string.IsNullOrWhiteSpace(safeUrl))
        {
            return Task.FromResult<string?>(null);
        }

        var encodedUrl = WebUtility.HtmlEncode(safeUrl);
        var captionText = audio.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;

        var sb = new StringBuilder();
        sb.Append($"<audio controls src=\"{encodedUrl}\"></audio>");

        var isExternal = SafeUrl.IsExternal(safeUrl);
        var rel = isExternal ? " rel=\"noopener noreferrer\"" : "";
        sb.Append($"<p><a href=\"{encodedUrl}\"{rel}>Audio</a></p>");

        if (!string.IsNullOrWhiteSpace(captionText))
        {
            sb.Append($"<p>{captionText}</p>");
        }

        return Task.FromResult<string?>(sb.ToString());
    }
}
