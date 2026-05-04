using System.Text.Json;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>
/// Renders Notion <c>table_of_contents</c> blocks as a <c>&lt;nav&gt;</c> placeholder.
/// The actual TOC content cannot be generated without knowing all headings on the page,
/// so we render an empty semantic container that CSS / JS can populate client-side.
/// </summary>
public sealed class TableOfContentsBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        // Render as an empty nav placeholder; the theme or JS can populate it.
        return Task.FromResult<string?>("<nav class=\"notion-toc\"></nav>");
    }
}
