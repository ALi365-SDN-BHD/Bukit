using System.Text.Json;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion divider blocks as &lt;hr /&gt;.</summary>
public sealed class DividerBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>("<hr />");
    }
}
