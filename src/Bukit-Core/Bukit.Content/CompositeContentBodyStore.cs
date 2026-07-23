using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

internal sealed class CompositeContentBodyStore : IContentBodyStore
{
    private readonly IReadOnlyDictionary<string, IContentBodyStore> _stores;

    public CompositeContentBodyStore(IReadOnlyDictionary<string, IContentBodyStore> stores)
    {
        _stores = stores;
    }

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(document.Body.Html))
        {
            return Task.FromResult(new ContentBody(document.Body.Html));
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

        var sourceId = ContentFieldReader.GetText(document.CustomFields, "sourceId");
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var originalBodyKey = document.Body.BodyKey is not null && document.Body.BodyKey.StartsWith(sourceKey + ":", StringComparison.Ordinal)
                ? document.Body.BodyKey.Substring(sourceKey.Length + 1)
                : document.Body.BodyKey;
            var sourceDocument = document with
            {
                Record = document.Record with { Identity = document.Record.Identity with { Id = sourceId } },
                Body = document.Body with { BodyKey = originalBodyKey }
            };
            return store.GetAsync(sourceDocument, cancellationToken);
        }

        return store.GetAsync(document, cancellationToken);
    }
}
