using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Notion;
using Bukit.Shared;

namespace Bukit.Content;

public sealed class CompositeContentProvider : IContentProvider
{
    private readonly IReadOnlyList<(string SourceKey, string SourceMode, string? Collection, IReadOnlyList<string>? AddToCollections, IContentProvider Provider)> _providers;
    private readonly ContentModelSchema? _schema;

    public CompositeContentProvider(IReadOnlyList<(string SourceKey, string SourceMode, IContentProvider Provider)> providers)
        : this(providers, schema: null)
    {
    }

    internal CompositeContentProvider(
        IReadOnlyList<(string SourceKey, string SourceMode, IContentProvider Provider)> providers,
        ContentModelSchema? schema)
    {
        _providers = providers
            .Select(x => (x.SourceKey, x.SourceMode, Collection: (string?)null, AddToCollections: (IReadOnlyList<string>?)null, x.Provider))
            .ToList();
        _schema = schema;
    }

    public CompositeContentProvider(IReadOnlyList<(string SourceKey, string SourceMode, string? Collection, IReadOnlyList<string>? AddToCollections, IContentProvider Provider)> providers)
        : this(providers, schema: null)
    {
    }

    internal CompositeContentProvider(
        IReadOnlyList<(string SourceKey, string SourceMode, string? Collection, IReadOnlyList<string>? AddToCollections, IContentProvider Provider)> providers,
        ContentModelSchema? schema)
    {
        _providers = providers;
        _schema = schema;
    }

    public async Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new Task<RawContentLoadResult>[_providers.Count];
        for (var i = 0; i < _providers.Count; i++)
        {
            tasks[i] = LoadProviderRawAsync(_providers[i].Provider, cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            await DisposeCompletedStoresAsync(tasks).ConfigureAwait(false);
            throw;
        }

        var relationSources = new NotionRelationProjectionSource[_providers.Count];
        for (var i = 0; i < _providers.Count; i++)
        {
            var resolver = _providers[i].Provider is INotionRelationFallbackResolverProvider provider
                ? provider.RelationFallbackResolver
                : null;
            relationSources[i] = new NotionRelationProjectionSource(_providers[i].SourceKey, (await tasks[i]).Documents, resolver);
        }

        IReadOnlyList<NotionRelationProjectionSource> projectedSources;
        try
        {
            projectedSources = await NotionCrossSourceRelationProjector.ProjectAsync(relationSources, _schema, cancellationToken);
        }
        catch
        {
            await DisposeCompletedStoresAsync(tasks).ConfigureAwait(false);
            throw;
        }

        var all = new List<RawContentDocument>();
        var orderedStores = new List<(string SourceKey, IContentBodyStore Store)>(_providers.Count);
        try
        {
            for (var i = 0; i < _providers.Count; i++)
            {
                var (sourceKey, sourceMode, collection, addToCollections, _) = _providers[i];
                var result = await tasks[i];
                var items = projectedSources[i].Documents;
                orderedStores.Add((sourceKey, result.BodyStore));

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fields = item.CustomFields is null
                        ? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, ContentField>(item.CustomFields, StringComparer.OrdinalIgnoreCase);

                    fields["sourceKey"] = new ContentField("text", sourceKey);
                    fields["sourceMode"] = new ContentField("text", sourceMode);
                    fields["sourceId"] = new ContentField("text", item.Id);
                    if (!string.IsNullOrWhiteSpace(collection))
                    {
                        fields["collection"] = new ContentField("text", collection.Trim());
                    }

                    // The opaque provider token keeps duplicate source keys routed to
                    // their own body stores; public document identity stays unchanged.
                    var internalBodyKey = item.Body.BodyKey is null
                        ? $"{sourceKey}:{item.Id}"
                        : $"{sourceKey}:{item.Body.BodyKey}";
                    var routedBodyKey = CompositeContentBodyStore.PrefixBodyKey(i, internalBodyKey);

                    all.Add(item with
                    {
                        SourceId = $"{sourceKey}:{item.Id}",
                        Body = item.Body with { BodyKey = routedBodyKey },
                        CustomFields = fields,
                        Properties = RawContentValue.FromFields(fields),
                        Source = item.Source with { SourceKey = sourceKey }
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
                            ["collection"] = new ContentField("text", extraCollection.Trim())
                        };

                        all.Add(item with
                        {
                            SourceId = $"{sourceKey}:{item.Id}:{extraCollection.Trim()}",
                            Body = item.Body with { BodyKey = routedBodyKey },
                            CustomFields = extraFields,
                            Properties = RawContentValue.FromFields(extraFields),
                            Source = item.Source with { SourceKey = sourceKey }
                        });
                    }
                }
            }
        }
        catch
        {
            await DisposeCompletedStoresAsync(tasks).ConfigureAwait(false);
            throw;
        }

        return new RawContentLoadResult(all, new CompositeContentBodyStore(orderedStores));
    }

    private static async Task DisposeCompletedStoresAsync(Task<RawContentLoadResult>[] tasks)
    {
        var stores = new List<IContentBodyStore>();
        foreach (var task in tasks)
        {
            if (task.IsCompletedSuccessfully)
            {
                stores.Add(task.Result.BodyStore);
            }
        }

        await DisposeStoresAsync(stores).ConfigureAwait(false);
    }

    private static async Task DisposeStoresAsync(IEnumerable<IContentBodyStore> stores)
    {
        var disposed = new HashSet<IContentBodyStore>(ReferenceEqualityComparer.Instance);
        foreach (var store in stores)
        {
            if (!disposed.Add(store))
            {
                continue;
            }

            try
            {
                if (store is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (store is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
                // Failure cleanup must surface the original exception, not a dispose fault.
            }
        }
    }

    private static async Task<RawContentLoadResult> LoadProviderRawAsync(
        IContentProvider provider,
        CancellationToken cancellationToken)
    {
        return await provider.LoadRawAsync(cancellationToken);
    }
}
