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

    public CompositeContentProvider(
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

    public CompositeContentProvider(
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

        await Task.WhenAll(tasks);

        var relationSources = new NotionRelationProjectionSource[_providers.Count];
        for (var i = 0; i < _providers.Count; i++)
        {
            var resolver = _providers[i].Provider is INotionRelationFallbackResolverProvider provider
                ? provider.RelationFallbackResolver
                : null;
            relationSources[i] = new NotionRelationProjectionSource(_providers[i].SourceKey, (await tasks[i]).Documents, resolver);
        }
        var projectedSources = await NotionCrossSourceRelationProjector.ProjectAsync(relationSources, _schema, cancellationToken);

        var all = new List<RawContentDocument>();
        var stores = new Dictionary<string, IContentBodyStore>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _providers.Count; i++)
        {
            var (sourceKey, sourceMode, collection, addToCollections, _) = _providers[i];
            var result = await tasks[i];
            var items = projectedSources[i].Documents;
            stores[sourceKey] = result.BodyStore;

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

                all.Add(item with
                {
                    SourceId = $"{sourceKey}:{item.Id}",
                    Body = item.Body with
                    {
                        BodyKey = item.Body.BodyKey is null
                            ? $"{sourceKey}:{item.Id}"
                            : $"{sourceKey}:{item.Body.BodyKey}"
                    },
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
                        Body = item.Body with
                        {
                            BodyKey = item.Body.BodyKey is null
                                ? $"{sourceKey}:{item.Id}"
                                : $"{sourceKey}:{item.Body.BodyKey}"
                        },
                        CustomFields = extraFields,
                        Properties = RawContentValue.FromFields(extraFields),
                        Source = item.Source with { SourceKey = sourceKey }
                    });
                }
            }
        }

        return new RawContentLoadResult(all, new CompositeContentBodyStore(stores));
    }

    private static async Task<RawContentLoadResult> LoadProviderRawAsync(
        IContentProvider provider,
        CancellationToken cancellationToken)
    {
        return await provider.LoadRawAsync(cancellationToken);
    }
}
