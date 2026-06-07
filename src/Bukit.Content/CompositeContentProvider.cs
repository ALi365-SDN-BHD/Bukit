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

    public async Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new Task<RawContentLoadResult>[_providers.Count];
        for (var i = 0; i < _providers.Count; i++)
        {
            tasks[i] = LoadProviderRawAsync(_providers[i].Provider, cancellationToken);
        }

        await Task.WhenAll(tasks);

        var all = new List<RawContentDocument>();
        var stores = new Dictionary<string, IContentBodyStore>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _providers.Count; i++)
        {
            var (sourceKey, sourceMode, collection, addToCollections, _) = _providers[i];
            var result = await tasks[i];
            var items = result.Documents;
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
                    Id = $"{sourceKey}:{item.Id}",
                    Body = item.Body with
                    {
                        BodyKey = item.Body.BodyKey is null
                            ? $"{sourceKey}:{item.Id}"
                            : $"{sourceKey}:{item.Body.BodyKey}"
                    },
                    CustomFields = fields,
                    Properties = RawContentValue.FromFields(fields)
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
                        Id = $"{sourceKey}:{item.Id}:{extraCollection.Trim()}",
                        Body = item.Body with
                        {
                            BodyKey = item.Body.BodyKey is null
                                ? $"{sourceKey}:{item.Id}"
                                : $"{sourceKey}:{item.Body.BodyKey}"
                        },
                        CustomFields = extraFields,
                        Properties = RawContentValue.FromFields(extraFields)
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
