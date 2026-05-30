using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Content;

public sealed class CompositeContentProvider : IContentProvider
{
    private readonly IReadOnlyList<(string SourceKey, string SourceMode, string? Collection, IReadOnlyList<string>? AddToCollections, IContentProvider Provider)> _providers;

    public CompositeContentProvider(IReadOnlyList<(string SourceKey, string SourceMode, IContentProvider Provider)> providers)
    {
        _providers = providers
            .Select(x => (x.SourceKey, x.SourceMode, Collection: (string?)null, AddToCollections: (IReadOnlyList<string>?)null, x.Provider))
            .ToList();
    }

    public CompositeContentProvider(IReadOnlyList<(string SourceKey, string SourceMode, string? Collection, IReadOnlyList<string>? AddToCollections, IContentProvider Provider)> providers)
    {
        _providers = providers;
    }

    public async Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new Task<ContentLoadResult>[_providers.Count];
        for (var i = 0; i < _providers.Count; i++)
        {
            tasks[i] = _providers[i].Provider.LoadAsync(cancellationToken);
        }

        await Task.WhenAll(tasks);

        var all = new List<ContentItem>();
        var stores = new Dictionary<string, IContentBodyStore>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _providers.Count; i++)
        {
            var (sourceKey, sourceMode, collection, addToCollections, _) = _providers[i];
            var result = await tasks[i];
            var items = result.Items;
            stores[sourceKey] = result.BodyStore;

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in item.Meta)
                {
                    meta[kv.Key] = kv.Value;
                }

                meta["sourceKey"] = sourceKey;
                meta["sourceMode"] = sourceMode;
                meta["sourceId"] = item.Id;
                if (!string.IsNullOrWhiteSpace(collection))
                {
                    meta["collection"] = collection.Trim();
                }

                all.Add(item with
                {
                    Id = $"{sourceKey}:{item.Id}",
                    BodyKey = item.BodyKey is null
                        ? $"{sourceKey}:{item.Id}"
                        : $"{sourceKey}:{item.BodyKey}",
                    Meta = meta
                });

                if (addToCollections is null)
                {
                    continue;
                }

                foreach (var extraCollection in addToCollections)
                {
                    if (string.IsNullOrWhiteSpace(extraCollection))
                    {
                        continue;
                    }

                    var extraMeta = new Dictionary<string, object>(meta, StringComparer.OrdinalIgnoreCase)
                    {
                        ["collection"] = extraCollection.Trim()
                    };

                    all.Add(item with
                    {
                        Id = $"{sourceKey}:{item.Id}:{extraCollection.Trim()}",
                        BodyKey = item.BodyKey is null
                            ? $"{sourceKey}:{item.Id}"
                            : $"{sourceKey}:{item.BodyKey}",
                        Meta = extraMeta
                    });
                }
            }
        }

        return new ContentLoadResult(all, new CompositeContentBodyStore(stores));
    }
}
