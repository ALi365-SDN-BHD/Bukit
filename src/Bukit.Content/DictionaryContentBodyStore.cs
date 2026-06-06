using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

public sealed class DictionaryContentBodyStore : IContentBodyStore
{
    private readonly IReadOnlyDictionary<string, ContentBody> _bodies;

    public DictionaryContentBodyStore(IReadOnlyDictionary<string, ContentBody> bodies)
    {
        _bodies = bodies;
    }

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(document.ContentHtml))
        {
            return Task.FromResult(new ContentBody(document.ContentHtml));
        }

        if (string.IsNullOrEmpty(document.BodyKey) || !_bodies.TryGetValue(document.BodyKey, out var body))
        {
            throw new InvalidOperationException($"No content body found for document '{document.Id}'.");
        }

        return Task.FromResult(body);
    }
}
