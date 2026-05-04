namespace Bukit.Content;

public sealed class EmptyContentBodyStore : IContentBodyStore
{
    public static EmptyContentBodyStore Instance { get; } = new();

    private EmptyContentBodyStore()
    {
    }

    public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(item.ContentHtml))
        {
            return Task.FromResult(new ContentBody(item.ContentHtml));
        }

        throw new InvalidOperationException($"No content body available for item '{item.Id}'.");
    }
}
