using System.Net;
using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

/// <summary>
/// Renders child_page / child_database blocks as simple labeled links/text.
/// </summary>
public sealed class ChildEntityBlockRenderer(string typeName) : INotionBlockRenderer
{
    private readonly string _typeName = typeName;

    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty(_typeName, out var container) || container.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var title = GetString(container, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            title = _typeName;
        }

        var cssClass = _typeName.Replace('_', '-');
        return Task.FromResult<string?>($"<p class=\"notion-{cssClass}\">{WebUtility.HtmlEncode(title)}</p>");
    }
}
