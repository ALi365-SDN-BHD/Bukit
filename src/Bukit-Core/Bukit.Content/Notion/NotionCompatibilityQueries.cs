using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content.Notion;

internal sealed record NotionCompatibilityPage(
    string PageId,
    string Title,
    string Slug,
    string NotionUrl,
    IReadOnlyDictionary<string, ContentField> Fields);

internal static class NotionCompatibilityQueries
{
    internal static async Task<NotionCompatibilityPage> FetchPageAsync(
        NotionApiClient client,
        string pageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        var page = await NotionPageQuery.FetchAsync(client.Transport, pageId, cancellationToken);
        return new NotionCompatibilityPage(
            page.PageId,
            page.Title,
            page.Slug,
            page.NotionUrl,
            page.Fields);
    }

    internal static Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadDatabaseOptionsAsync(
        NotionApiClient client,
        string databaseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        return NotionDatabaseOptionReader.ReadAsync(client.Transport, databaseId, cancellationToken);
    }

    internal static string NormalizeFieldKey(string text)
        => NotionDatabaseOptionReader.NormalizeFieldKey(text);
}
