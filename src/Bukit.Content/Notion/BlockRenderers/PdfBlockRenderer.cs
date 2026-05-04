using System.Net;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

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
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<string?>(null);
        }

        var captionText = pdf.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;
        var linkText = string.IsNullOrWhiteSpace(captionText) ? "PDF" : captionText;
        return Task.FromResult<string?>($"<p class=\"notion-pdf\"><a href=\"{WebUtility.HtmlEncode(url)}\">{linkText}</a></p>");
    }
}
