using System.Text.Json;

namespace Bukit.Content.Notion;

/// <summary>
/// Renders a single Notion block type to HTML.
/// Implement this interface to add support for a new block type
/// or to override the rendering of an existing one.
/// </summary>
public interface INotionBlockRenderer
{
    /// <summary>
    /// Renders the given Notion block to an HTML string.
    /// Returns <c>null</c> if the block cannot be rendered.
    /// </summary>
    /// <param name="block">The raw Notion block JSON element.</param>
    /// <param name="context">Provides access to child-block rendering and the Notion API client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken);
}
