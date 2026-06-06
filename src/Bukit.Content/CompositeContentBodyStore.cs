using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

public sealed class CompositeContentBodyStore : IContentBodyStore
{
    private readonly IReadOnlyDictionary<string, IContentBodyStore> _stores;

    public CompositeContentBodyStore(IReadOnlyDictionary<string, IContentBodyStore> stores)
    {
        _stores = stores;
    }

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(document.ContentHtml))
        {
            return Task.FromResult(new ContentBody(document.ContentHtml));
        }

        var separatorIndex = document.Id.IndexOf(':');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException($"Unable to resolve content body store for document '{document.Id}'.");
        }

        var sourceKey = document.Id[..separatorIndex];
        if (!_stores.TryGetValue(sourceKey, out var store))
        {
            throw new InvalidOperationException($"No content body store registered for source '{sourceKey}'.");
        }

        var sourceId = ContentFieldReader.GetText(document.Fields, "sourceId");
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var originalBodyKey = document.BodyKey is not null && document.BodyKey.StartsWith(sourceKey + ":", StringComparison.Ordinal)
                ? document.BodyKey.Substring(sourceKey.Length + 1)
                : document.BodyKey;
            var sourceDocument = document with
            {
                Record = document.Record with { Identity = document.Record.Identity with { Id = sourceId } },
                BodyKey = originalBodyKey
            };
            return store.GetAsync(sourceDocument, cancellationToken);
        }

        return store.GetAsync(document, cancellationToken);
    }
}
