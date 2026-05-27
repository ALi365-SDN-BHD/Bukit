namespace Bukit.Engine.Abstractions.Content;

public sealed class NullContentBodyStore : IContentBodyStore
{
    public static NullContentBodyStore Instance { get; } = new();

    private NullContentBodyStore()
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
