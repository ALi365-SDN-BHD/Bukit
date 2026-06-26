namespace Bukit.Notion.Client;

public interface INotionClient
{
    Task<NotionQueryResult> QueryDataSourceAsync(
        string dataSourceId,
        NotionQueryRequest request,
        CancellationToken cancellationToken);

    Task<NotionPageResult> CreatePageAsync(
        NotionCreatePageRequest request,
        CancellationToken cancellationToken);

    Task<NotionPageResult> UpdatePagePropertiesAsync(
        string pageId,
        NotionUpdatePageRequest request,
        CancellationToken cancellationToken);

    Task AppendBlockChildrenAsync(
        string blockId,
        IReadOnlyList<NotionBlock> children,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotionBlockResult>> ListBlockChildrenAsync(
        string blockId,
        CancellationToken cancellationToken);

    Task DeleteBlockAsync(
        string blockId,
        CancellationToken cancellationToken);
}
