using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

public sealed class EmptyContentBodyStore : IContentBodyStore
{
    public static EmptyContentBodyStore Instance { get; } = new();

    private EmptyContentBodyStore()
    {
    }

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(document.ContentHtml))
        {
            return Task.FromResult(new ContentBody(document.ContentHtml));
        }

        throw new InvalidOperationException($"No content body available for document '{document.Id}'.");
    }
}
