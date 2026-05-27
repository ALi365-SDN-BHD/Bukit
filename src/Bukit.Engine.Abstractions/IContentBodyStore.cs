namespace Bukit.Engine.Abstractions.Content;

public interface IContentBodyStore
{
    Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default);
}
