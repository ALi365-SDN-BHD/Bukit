using Bukit.Shared;

namespace Bukit.Content;

public sealed class CompositeContentProvider : IContentProvider
{
    private readonly IReadOnlyList<(string SourceKey, string SourceMode, IContentProvider Provider)> _providers;

    public CompositeContentProvider(IReadOnlyList<(string SourceKey, string SourceMode, IContentProvider Provider)> providers)
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
            var (sourceKey, sourceMode, _) = _providers[i];
            var result = tasks[i].Result;
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

                all.Add(item with
                {
                    Id = $"{sourceKey}:{item.Id}",
                    Meta = meta
                });
            }
        }

        return new ContentLoadResult(all, new CompositeContentBodyStore(stores));
    }
}
