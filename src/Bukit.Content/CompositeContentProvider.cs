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

                var fields = new Dictionary<string, ContentField>(item.Fields ?? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)
                {
                    ["sourceKey"] = new("text", sourceKey),
                    ["sourceMode"] = new("text", sourceMode),
                    ["sourceId"] = new("text", item.Id)
                };
                if (!string.IsNullOrWhiteSpace(collection))
                {
                    fields["collection"] = new ContentField("text", collection.Trim());
                }

                all.Add(item with
                {
                    Id = $"{sourceKey}:{item.Id}",
                    BodyKey = item.BodyKey is null
                        ? $"{sourceKey}:{item.Id}"
                        : $"{sourceKey}:{item.BodyKey}",
                    Fields = fields
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

                    var extraFields = new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase)
                    {
                        ["collection"] = new("text", extraCollection.Trim())
                    };

                    all.Add(item with
                    {
                        Id = $"{sourceKey}:{item.Id}:{extraCollection.Trim()}",
                        BodyKey = item.BodyKey is null
                            ? $"{sourceKey}:{item.Id}"
                            : $"{sourceKey}:{item.BodyKey}",
                        Fields = extraFields
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
            if (provider is not IRawContentProvider rawProvider)
            {
                throw new ContentException($"Composite content source '{_providers[i].SourceKey}' must implement vNext raw content loading.");
            }

            tasks[i] = rawProvider.LoadRawAsync(cancellationToken);
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
                        all.Add(WithCompositeSource(document, sourceKey, sourceMode, extraCollection.Trim(), includeCollectionInId: true));
                    }
                }
            }
        }

        return new RawContentLoadResult(all, new CompositeContentBodyStore(stores));
    }

    private static RawContentDocument WithCompositeSource(
        RawContentDocument document,
        string sourceKey,
        string sourceMode,
        string? collection,
        bool includeCollectionInId = false)
    {
        var properties = new Dictionary<string, RawContentValue>(document.Properties, StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = new("text", sourceKey),
            ["sourceId"] = new("text", document.SourceId),
            ["sourceMode"] = new("text", sourceMode)
        };
        if (!string.IsNullOrWhiteSpace(collection))
        {
            properties["collection"] = new RawContentValue("text", collection.Trim());
        }

        var fields = new Dictionary<string, ContentField>(document.CustomFields, StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = new("text", sourceKey),
            ["sourceId"] = new("text", document.SourceId),
            ["sourceMode"] = new("text", sourceMode)
        };
        if (!string.IsNullOrWhiteSpace(collection))
        {
            fields["collection"] = new ContentField("text", collection.Trim());
        }

        var sourceId = includeCollectionInId && !string.IsNullOrWhiteSpace(collection)
            ? $"{sourceKey}:{document.SourceId}:{collection.Trim()}"
            : $"{sourceKey}:{document.SourceId}";

        return document with
        {
            SourceId = sourceId,
            Body = document.Body with
            {
                BodyKey = document.Body.BodyKey is null ? $"{sourceKey}:{document.SourceId}" : $"{sourceKey}:{document.Body.BodyKey}"
            },
            Properties = properties,
            Source = document.Source with { SourceKey = sourceKey },
            CustomFields = fields
        };
    }

}
