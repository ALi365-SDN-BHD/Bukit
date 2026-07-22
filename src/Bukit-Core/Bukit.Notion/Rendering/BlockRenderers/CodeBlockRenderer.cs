using System.Net;
using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

/// <summary>Renders Notion code blocks as &lt;pre&gt;&lt;code&gt;.</summary>
public sealed class CodeBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var lang = GetString(code, "language") ?? string.Empty;
        var richText = code.TryGetProperty("rich_text", out var rt) ? rt : default;
        var raw = ExtractPlainText(richText);
        var encoded = WebUtility.HtmlEncode(raw);

        var classAttr = string.IsNullOrWhiteSpace(lang) ? string.Empty : $" class=\"language-{WebUtility.HtmlEncode(lang)}\"";
        var codeHtml = $"<pre><code{classAttr}>{encoded}</code></pre>";

        var captionText = code.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;
        if (string.IsNullOrWhiteSpace(captionText))
        {
            return Task.FromResult<string?>(codeHtml);
        }

        return Task.FromResult<string?>($"<figure>{codeHtml}<figcaption>{captionText}</figcaption></figure>");
    }
}
