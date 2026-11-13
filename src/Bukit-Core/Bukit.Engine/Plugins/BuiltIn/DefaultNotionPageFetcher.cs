using Bukit.Content.Notion;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class DefaultNotionPageFetcher : INotionPageFetcher
{
    public async Task<NotionFetchedPage?> FetchAsync(
        NotionContentClient client,
        string pageId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(client);
            var page = await NotionPageQuery.FetchAsync(client, pageId, cancellationToken);
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
        catch (Bukit.Shared.NotionSnapshotException)
        {
            // Offline: a page the snapshot lacks fails the build instead of vanishing from pages_index.
            throw;
        }
        catch (Exception exception)
        {
            logger.Warn(
                $"pages-index: failed to fetch Notion page '{pageId}': {exception.Message}");
            return null;
        }
    }
}
