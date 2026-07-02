using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text.Json;
using static Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>
/// Renders Notion <c>link_to_page</c> blocks as an anchor or text reference.
/// The linked page's URL cannot be resolved without a page-to-slug mapping, so we
/// render the page/database ID as a data attribute for downstream resolution.
/// </summary>
public sealed class LinkToPageBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("link_to_page", out var ltp) || ltp.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var linkType = GetString(ltp, "type"); // "page_id" or "database_id"
        string? targetId = null;
        if (linkType == "page_id")
        {
            targetId = GetString(ltp, "page_id");
        }
        else if (linkType == "database_id")
        {
            targetId = GetString(ltp, "database_id");
        }

        if (string.IsNullOrWhiteSpace(targetId))
        {
            return Task.FromResult<string?>(null);
        }

        var encoded = WebUtility.HtmlEncode(targetId);
        return Task.FromResult<string?>(
            $"<p class=\"notion-link-to-page\"><a data-notion-id=\"{encoded}\">\uD83D\uDD17 {encoded}</a></p>");
    }
}
