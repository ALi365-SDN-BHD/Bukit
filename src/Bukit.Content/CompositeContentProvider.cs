using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Content;

public sealed class CompositeContentProvider : IContentProvider, IRawContentProvider
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

    public async Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new Task<RawContentLoadResult>[_providers.Count];
        for (var i = 0; i < _providers.Count; i++)
        {
            var provider = _providers[i].Provider;
            tasks[i] = provider is IRawContentProvider rawProvider
                ? rawProvider.LoadRawAsync(cancellationToken)
                : LoadLegacyRawAsync(provider, cancellationToken);
        }

        await Task.WhenAll(tasks);

        var all = new List<RawContentDocument>();
        var stores = new Dictionary<string, IContentBodyStore>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _providers.Count; i++)
        {
            var (sourceKey, sourceMode, collection, addToCollections, _) = _providers[i];
            var result = await tasks[i];
            stores[sourceKey] = result.BodyStore;

            foreach (var document in result.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var merged = WithCompositeSource(document, sourceKey, sourceMode, collection);
                all.Add(merged);

                if (addToCollections is null)
                {
                    continue;
                }

                foreach (var extraCollection in addToCollections)
                {
                    if (!string.IsNullOrWhiteSpace(extraCollection))
                    {
                        all.Add(WithCompositeSource(document, sourceKey, sourceMode, extraCollection.Trim()));
                    }
                }
            }
        }

        return new RawContentLoadResult(all, new CompositeContentBodyStore(stores));
    }

    private static async Task<RawContentLoadResult> LoadLegacyRawAsync(IContentProvider provider, CancellationToken cancellationToken)
    {
        var result = await provider.LoadAsync(cancellationToken);
        var documents = result.Items.Select(item => new RawContentDocument(
            item.Id,
            "legacy",
            item.Title,
            item.Slug,
            item.PublishAt,
            new RawBody(item.ContentHtml, item.BodyKey, null, null),
            item.Meta.ToDictionary(kv => kv.Key, kv => ToRawContentValue(kv.Value), StringComparer.OrdinalIgnoreCase),
            new ContentSourceInfo("legacy", null, null, item.Id, null, null, "loaded"),
            item.Fields ?? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase))).ToArray();

        return new RawContentLoadResult(documents, result.BodyStore);
    }

    private static RawContentDocument WithCompositeSource(
        RawContentDocument document,
        string sourceKey,
        string sourceMode,
        string? collection)
    {
        var properties = new Dictionary<string, RawContentValue>(document.Properties, StringComparer.OrdinalIgnoreCase)
        {
            ["sourceMode"] = new("text", sourceMode)
        };
        if (!string.IsNullOrWhiteSpace(collection))
        {
            properties["collection"] = new RawContentValue("text", collection.Trim());
        }

        return document with
        {
            SourceId = $"{sourceKey}:{document.SourceId}",
            Body = document.Body with
            {
                BodyKey = document.Body.BodyKey is null ? $"{sourceKey}:{document.SourceId}" : $"{sourceKey}:{document.Body.BodyKey}"
            },
            Properties = properties,
            Source = document.Source with { SourceKey = sourceKey }
        };
    }

    private static RawContentValue ToRawContentValue(object? value)
    {
        return value switch
        {
            bool => new RawContentValue("bool", value),
            int or long or double or float => new RawContentValue("number", value),
            IEnumerable<string> => new RawContentValue("list", value),
            IEnumerable<object> => new RawContentValue("list", value),
            _ => new RawContentValue("text", value)
        };
    }
}
