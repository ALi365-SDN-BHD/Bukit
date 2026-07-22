using Bukit.Content.Notion;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class DefaultNotionPageFetcher : INotionPageFetcher
{
    public async Task<NotionFetchedPage?> FetchAsync(
        NotionApiClient client,
        string pageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await NotionCompatibilityQueries.FetchPageAsync(client, pageId, cancellationToken);
            return new NotionFetchedPage(
                page.PageId,
                page.Title,
                page.Slug,
                page.NotionUrl,
                page.Fields);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"[warn] pages-index: failed to fetch Notion page '{pageId}': {exception.Message}");
            return null;
        }
    }
}
