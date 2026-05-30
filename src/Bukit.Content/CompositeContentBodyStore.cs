using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

public sealed class CompositeContentBodyStore : IContentBodyStore
{
    private readonly IReadOnlyDictionary<string, IContentBodyStore> _stores;

    public CompositeContentBodyStore(IReadOnlyDictionary<string, IContentBodyStore> stores)
    {
        _stores = stores;
    }

    public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(item.ContentHtml))
        {
            return Task.FromResult(new ContentBody(item.ContentHtml));
        }

        var separatorIndex = item.Id.IndexOf(':');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException($"Unable to resolve content body store for item '{item.Id}'.");
        }

        var sourceKey = item.Id[..separatorIndex];
        if (!_stores.TryGetValue(sourceKey, out var store))
        {
            throw new InvalidOperationException($"No content body store registered for source '{sourceKey}'.");
        }

        if (item.Meta.TryGetValue("sourceId", out var sourceIdObj) &&
            sourceIdObj is not null &&
            !string.IsNullOrWhiteSpace(sourceIdObj.ToString()))
        {
            var originalBodyKey = item.BodyKey is not null && item.BodyKey.StartsWith(sourceKey + ":", StringComparison.Ordinal)
                ? item.BodyKey.Substring(sourceKey.Length + 1)
                : item.BodyKey;
            var sourceItem = item with { Id = sourceIdObj.ToString()!, BodyKey = originalBodyKey };
            return store.GetAsync(sourceItem, cancellationToken);
        }

        return store.GetAsync(item, cancellationToken);
    }
}
