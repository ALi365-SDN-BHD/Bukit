using Bukit.Engine.Abstractions.Content;
using System.Text.Json;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>
/// A no-op renderer that produces an empty string (renders nothing).
/// Used for Notion-only interactive blocks that have no meaningful static HTML
/// representation, such as <c>breadcrumb</c> and <c>template</c>.
/// </summary>
public sealed class NoOpBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(string.Empty);
    }
}
