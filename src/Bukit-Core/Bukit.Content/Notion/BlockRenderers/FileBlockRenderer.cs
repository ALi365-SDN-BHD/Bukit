using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using System.Net;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion file blocks as downloadable links.</summary>
public sealed class FileBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("file", out var file) || file.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var url = ExtractFileUrl(file);
        var safeUrl = SafeUrl.ForLink(url);
        if (string.IsNullOrWhiteSpace(safeUrl))
        {
            return Task.FromResult<string?>(null);
        }

        var captionText = file.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;
        var linkText = string.IsNullOrWhiteSpace(captionText) ? "File" : captionText;
        var rel = SafeUrl.IsExternal(safeUrl) ? " rel=\"noopener noreferrer\"" : "";
        return Task.FromResult<string?>($"<p class=\"notion-file\"><a href=\"{WebUtility.HtmlEncode(safeUrl)}\"{rel}>{linkText}</a></p>");
    }
}
