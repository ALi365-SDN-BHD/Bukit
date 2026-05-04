using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Shared;

namespace Bukit.Content.Notion;

public sealed class NotionBlocksRenderer
{
    private readonly NotionApiClient _client;
    private readonly NotionBlockRendererRegistry _registry;
    private readonly NotionRenderContext _context;

    public NotionBlocksRenderer(NotionApiClient client)
        : this(client, NotionBlockRendererRegistry.CreateDefault())
    {
    }

    public NotionBlocksRenderer(NotionApiClient client, NotionBlockRendererRegistry registry)
    {
        _client = client;
        _registry = registry;
        _context = new NotionRenderContext(this, _client);
    }

    /// <summary>
    /// Gets the block renderer registry, allowing callers to register
    /// custom transformers before rendering.
    /// </summary>
    public NotionBlockRendererRegistry Registry => _registry;

    public async Task<string> RenderPageAsync(string pageId, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        await RenderChildrenToBuilderAsync(pageId, sb, cancellationToken);
        return sb.ToString();
    }

    /// <summary>
    /// Renders all children of the given block into a StringBuilder.
    /// Called internally by <see cref="NotionRenderContext"/> to support nested rendering.
    /// </summary>
    internal async Task RenderChildrenToBuilderAsync(string blockId, StringBuilder sb, CancellationToken cancellationToken)
    {
        string? startCursor = null;
        string? openList = null;

        while (true)
        {
            var url = NotionApiUrls.BlockChildren(blockId);
            if (!string.IsNullOrWhiteSpace(startCursor))
            {
                url += $"&start_cursor={WebUtility.UrlEncode(startCursor)}";
            }

            using var doc = await _client.GetAsync(url, cancellationToken);
            var root = doc.RootElement;

            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                throw new ContentException("Notion blocks response missing results.");
            }

            foreach (var block in results.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var type = GetString(block, "type");
                if (type is null)
                {
                    continue;
                }

                if (type is "bulleted_list_item" or "numbered_list_item")
                {
                    var listTag = type == "bulleted_list_item" ? "ul" : "ol";
                    if (openList is null)
                    {
                        sb.AppendLine($"<{listTag}>");
                        openList = listTag;
                    }
                    else if (!string.Equals(openList, listTag, StringComparison.Ordinal))
                    {
                        sb.AppendLine($"</{openList}>");
                        sb.AppendLine($"<{listTag}>");
                        openList = listTag;
                    }

                    sb.AppendLine(await RenderListItemAsync(block, type, cancellationToken));
                    continue;
                }

                if (openList is not null)
                {
                    sb.AppendLine($"</{openList}>");
                    openList = null;
                }

                var rendered = await _registry.RenderBlockAsync(type, block, _context, cancellationToken);
                if (!string.IsNullOrWhiteSpace(rendered))
                {
                    sb.AppendLine(rendered);
                }
            }

            if (root.TryGetProperty("has_more", out var hasMoreEl) && hasMoreEl.ValueKind == JsonValueKind.True)
            {
                startCursor = GetString(root, "next_cursor");
                if (string.IsNullOrWhiteSpace(startCursor))
                {
                    break;
                }

                continue;
            }

            break;
        }

        if (openList is not null)
        {
            sb.AppendLine($"</{openList}>");
        }
    }

    private async Task<string?> RenderListItemAsync(JsonElement block, string type, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty(type, out var container) || container.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var richText = container.TryGetProperty("rich_text", out var rt) ? rt : default;
        var inner = NotionRichTextRenderer.Render(richText);

        // Read block-level color for list items
        var color = GetString(container, "color");
        var colorClass = string.Empty;
        if (!string.IsNullOrWhiteSpace(color) &&
            !string.Equals(color, "default", StringComparison.OrdinalIgnoreCase))
        {
            colorClass = $" class=\"notion-{color}\"";
        }

        var hasChildren = block.TryGetProperty("has_children", out var hc) && hc.ValueKind == JsonValueKind.True;
        if (!hasChildren)
        {
            return $"<li{colorClass}>{inner}</li>";
        }

        var id = GetString(block, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return $"<li{colorClass}>{inner}</li>";
        }

        var nested = new StringBuilder();
        await RenderChildrenToBuilderAsync(id, nested, cancellationToken);
        return $"<li{colorClass}>{inner}{nested}</li>";
    }

    private static string? GetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v))
        {
            return null;
        }

        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
