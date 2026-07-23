using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

internal sealed class DictionaryContentBodyStore : IContentBodyStore
{
    private readonly IReadOnlyDictionary<string, ContentBody> _bodies;

    public DictionaryContentBodyStore(IReadOnlyDictionary<string, ContentBody> bodies)
    {
        _bodies = bodies;
    }

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(document.Body.Html))
        {
            return Task.FromResult(new ContentBody(document.Body.Html));
        }

        if (string.IsNullOrEmpty(document.Body.BodyKey) || !_bodies.TryGetValue(document.Body.BodyKey, out var body))
        {
            throw new InvalidOperationException($"No content body found for document '{document.Id}'.");
        }

        return Task.FromResult(body);
    }
}
