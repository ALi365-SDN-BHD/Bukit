namespace Bukit.Engine.Abstractions.Content;

public sealed class NullContentBodyStore : IContentBodyStore
{
    public static NullContentBodyStore Instance { get; } = new();

    private NullContentBodyStore()
    {
    }

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(document.Body.Html))
        {
            return Task.FromResult(new ContentBody(document.Body.Html));
        }

        throw new InvalidOperationException($"No content body available for document '{document.Id}'.");
    }
}
