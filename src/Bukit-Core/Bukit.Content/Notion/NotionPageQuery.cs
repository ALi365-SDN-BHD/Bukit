using System.Text;
using Bukit.Engine.Abstractions.Content;
using Bukit.Notion;
using Bukit.Notion.Transport;

namespace Bukit.Content.Notion;

internal sealed record NotionPageSnapshot(
    string PageId,
    string Title,
    string Slug,
    string NotionUrl,
    IReadOnlyDictionary<string, ContentField> Fields);

internal static class NotionPageQuery
{
    internal static Task<NotionPageSnapshot> FetchAsync(
        NotionClient client,
        string pageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        return FetchCoreAsync((url, token) => client.GetAsync(url, token), pageId, cancellationToken);
    }

    /// <summary>Reads a page through the content client: online, recorded, or from the offline snapshot.</summary>
    internal static Task<NotionPageSnapshot> FetchAsync(
        NotionContentClient client,
        string pageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        return FetchCoreAsync(client.GetAsync, pageId, cancellationToken);
    }

    private static async Task<NotionPageSnapshot> FetchCoreAsync(
        Func<string, CancellationToken, Task<System.Text.Json.JsonDocument>> getAsync,
        string pageId,
        CancellationToken cancellationToken)
    {
        using var document = await getAsync(NotionApiUrls.Pages(pageId), cancellationToken);
        var page = document.RootElement;
        var notionUrl = NotionContentSource.GetString(page, "url")?.Trim() ?? string.Empty;
        var properties = page.TryGetProperty("properties", out var propertyElement)
            ? propertyElement
            : default;
        var title = ExtractPageTitle(properties)?.Trim();
        title = string.IsNullOrWhiteSpace(title) ? pageId : title;
        var slug = NotionContentSource.Slugify(title)
            ?? pageId.Replace("-", string.Empty, StringComparison.Ordinal);
        var fields = NotionContentPropertyParser.ExtractAllFields(properties);
        fields = NotionFieldProjectionHelper.InjectPageCoverAndIcon(fields, page);
        return new NotionPageSnapshot(pageId, title, slug, notionUrl, fields);
    }

    private static string ExtractPageTitle(System.Text.Json.JsonElement properties)
    {
        if (properties.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var property in properties.EnumerateObject())
        {
            if (NotionContentSource.GetString(property.Value, "type") != "title" ||
                !property.Value.TryGetProperty("title", out var titleParts) ||
                titleParts.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                continue;
            }

            var builder = new StringBuilder();
            foreach (var part in titleParts.EnumerateArray())
            {
                var text = NotionContentSource.GetString(part, "plain_text")?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(text);
            }

            return builder.ToString();
        }

        return string.Empty;
    }
}
