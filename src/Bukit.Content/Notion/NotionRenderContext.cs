using System.Text;
using System.Text.Json;

namespace Bukit.Content.Notion;

/// <summary>
/// Provides shared context for block renderers, including access to
/// child-block rendering and the Notion API client.
/// </summary>
public sealed class NotionRenderContext
{
    private readonly NotionBlocksRenderer _renderer;

    internal NotionRenderContext(NotionBlocksRenderer renderer, NotionApiClient client)
    {
        _renderer = renderer;
        Client = client;
    }

    /// <summary>The Notion API client for fetching additional data (e.g. table rows).</summary>
    public NotionApiClient Client { get; }

    /// <summary>
    /// Renders all children of the given block and returns the combined HTML.
    /// </summary>
    public async Task<string> RenderChildrenAsync(string blockId, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        await _renderer.RenderChildrenToBuilderAsync(blockId, sb, cancellationToken);
        return sb.ToString();
    }
}
