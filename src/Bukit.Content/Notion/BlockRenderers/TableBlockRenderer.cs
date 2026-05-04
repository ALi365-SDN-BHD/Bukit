using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>Renders Notion table blocks (with table_row children) as &lt;table&gt;.</summary>
public sealed class TableBlockRenderer : INotionBlockRenderer
{
    public async Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("table", out var table) || table.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var hasColumnHeader = table.TryGetProperty("has_column_header", out var hch) && hch.ValueKind == JsonValueKind.True;
        var hasRowHeader = table.TryGetProperty("has_row_header", out var hrh) && hrh.ValueKind == JsonValueKind.True;
        var hasChildren = block.TryGetProperty("has_children", out var hc) && hc.ValueKind == JsonValueKind.True;
        if (!hasChildren)
        {
            return null;
        }

        var id = GetString(block, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        // Fetch table rows
        var rows = new List<List<string>>();
        string? cursor = null;
        while (true)
        {
            var fetchUrl = NotionApiUrls.BlockChildren(id);
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                fetchUrl += $"&start_cursor={WebUtility.UrlEncode(cursor)}";
            }

            using var doc = await context.Client.GetAsync(fetchUrl, cancellationToken);
            var root = doc.RootElement;
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var row in results.EnumerateArray())
            {
                if (GetString(row, "type") != "table_row")
                {
                    continue;
                }

                if (!row.TryGetProperty("table_row", out var tableRow) || tableRow.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!tableRow.TryGetProperty("cells", out var cells) || cells.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var cellList = new List<string>();
                foreach (var cell in cells.EnumerateArray())
                {
                    cellList.Add(NotionRichTextRenderer.Render(cell));
                }

                rows.Add(cellList);
            }

            if (root.TryGetProperty("has_more", out var hasMore) && hasMore.ValueKind == JsonValueKind.True)
            {
                cursor = GetString(root, "next_cursor");
                if (string.IsNullOrWhiteSpace(cursor))
                {
                    break;
                }

                continue;
            }

            break;
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine("<table>");
        for (var i = 0; i < rows.Count; i++)
        {
            var isHeaderRow = hasColumnHeader && i == 0;
            sb.Append("<tr>");
            for (var j = 0; j < rows[i].Count; j++)
            {
                // Use <th> for header row cells, or first cell when has_row_header is true
                var isHeaderCell = isHeaderRow || (hasRowHeader && j == 0);
                var cellTag = isHeaderCell ? "th" : "td";
                sb.Append($"<{cellTag}>{rows[i][j]}</{cellTag}>");
            }

            sb.AppendLine("</tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }
}
