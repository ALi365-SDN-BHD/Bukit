using System.Net;
using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

/// <summary>Renders Notion pdf blocks as links (safer cross-browser fallback).</summary>
public sealed class PdfBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("pdf", out var pdf) || pdf.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var url = ExtractFileUrl(pdf);
        var safeUrl = RenderingSafeUrl.ForMedia(url);
        if (string.IsNullOrWhiteSpace(safeUrl))
        {
            return Task.FromResult<string?>(null);
        }

        var captionText = pdf.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;
        var linkText = string.IsNullOrWhiteSpace(captionText) ? "PDF" : captionText;
        var rel = RenderingSafeUrl.IsExternal(safeUrl) ? " rel=\"noopener noreferrer\"" : "";
        return Task.FromResult<string?>($"<p class=\"notion-pdf\"><a href=\"{WebUtility.HtmlEncode(safeUrl)}\"{rel}>{linkText}</a></p>");
    }
}
