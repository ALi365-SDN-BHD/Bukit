using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

public sealed class DictionaryContentBodyStore : IContentBodyStore
{
    private readonly IReadOnlyDictionary<string, ContentBody> _bodies;

    public DictionaryContentBodyStore(IReadOnlyDictionary<string, ContentBody> bodies)
    {
        _bodies = bodies;
    }

    public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(item.ContentHtml))
        {
            return Task.FromResult(new ContentBody(item.ContentHtml));
        }

        if (string.IsNullOrEmpty(item.BodyKey) || !_bodies.TryGetValue(item.BodyKey, out var body))
        {
            throw new InvalidOperationException($"No content body found for item '{item.Id}'.");
        }

        return Task.FromResult(body);
    }
}
