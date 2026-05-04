namespace Bukit.Content;

public interface IContentBodyStore
{
    Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default);
}
