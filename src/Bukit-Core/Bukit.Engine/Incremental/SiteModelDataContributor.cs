using Bukit.Rendering;

namespace Bukit.Engine.Incremental;

/// <summary>
/// The shared data collections of the site model (<c>site.modules</c>, <c>site.data</c>,
/// <c>site.data_index</c>, <c>site.related_pages</c>, <c>site.data_files</c>). They are not part
/// of the base render hash: each route depends only on the collections its last render read
/// (see <see cref="RenderDependencySnapshot"/>).
/// </summary>
internal sealed class SiteModelDataContributor : IRenderDependencyContributor
{
    public string Name => "site-model-data";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        foreach (var (_, write) in EnumerateCollections(context))
        {
            write(writer);
        }
    }

    /// <summary>
    /// Each collection under its read key (see <see cref="RenderReadRecorder"/>), in a stable
    /// order. Keys are lower case, so names differing only in case share one key.
    /// </summary>
    internal static IEnumerable<(string Key, Action<RenderDependencyHashWriter> Write)> EnumerateCollections(
        RenderDependencyContext context)
    {
        var excludedSource = context.Config.Content.RouteMetadata?.Source;
        var siteModel = context.SiteModel;
        foreach (var module in Ordered(siteModel.Modules, excludedSource))
        {
            yield return (CollectionKey("site.modules.", module.Key), writer => AppendModule(writer, module.Key, module.Value));
        }

        foreach (var entry in Ordered(siteModel.Data, excludedSource))
        {
            if (entry.Key.StartsWith("__", StringComparison.Ordinal))
            {
                var publicAlias = entry.Key switch
                {
                    "__data_files" => "data_files",
                    "__related_pages" => "related_pages",
                    _ => null
                };
                if (publicAlias is not null)
                {
                    yield return ($"site.{publicAlias}", writer => writer.AppendLabeledCanonicalValue($"site.{publicAlias}", entry.Value));
                }

                continue;
            }

            yield return (CollectionKey("site.data.", entry.Key), writer => writer.AppendLabeledCanonicalValue($"site.data.{entry.Key}", entry.Value));
        }

        foreach (var entry in Ordered(siteModel.DataIndex, excludedSource))
        {
            yield return (CollectionKey("site.data_index.", entry.Key), writer => writer.AppendLabeledCanonicalValue($"site.data_index.{entry.Key}", entry.Value));
        }
    }

    /// <summary>
    /// The route metadata source under its read keys. It stays out of the base hash and of
    /// <see cref="EnumerateCollections"/> (route metadata is folded in per route), but a template
    /// can still read it as <c>site.data.&lt;source&gt;</c>, and routes that read it depend on it.
    /// </summary>
    internal static IEnumerable<(string Key, Action<RenderDependencyHashWriter> Write)> EnumerateRouteMetadataSourceCollections(
        RenderDependencyContext context)
    {
        var source = context.Config.Content.RouteMetadata?.Source;
        if (string.IsNullOrWhiteSpace(source))
        {
            yield break;
        }

        var siteModel = context.SiteModel;
        foreach (var module in Matching(siteModel.Modules, source))
        {
            yield return (CollectionKey("site.modules.", module.Key), writer => AppendModule(writer, module.Key, module.Value));
        }

        foreach (var entry in Matching(siteModel.Data, source))
        {
            yield return (CollectionKey("site.data.", entry.Key), writer => writer.AppendLabeledCanonicalValue($"site.data.{entry.Key}", entry.Value));
        }

        foreach (var entry in Matching(siteModel.DataIndex, source))
        {
            yield return (CollectionKey("site.data_index.", entry.Key), writer => writer.AppendLabeledCanonicalValue($"site.data_index.{entry.Key}", entry.Value));
        }
    }

    private static IEnumerable<KeyValuePair<string, TValue>> Matching<TValue>(
        IReadOnlyDictionary<string, TValue>? values,
        string source) =>
        values is null
            ? []
            : values
                .Where(entry => string.Equals(entry.Key, source, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Key, StringComparer.Ordinal);

    private static string CollectionKey(string scope, string key) => scope + key.ToLowerInvariant();

    private static IEnumerable<KeyValuePair<string, TValue>> Ordered<TValue>(
        IReadOnlyDictionary<string, TValue>? values,
        string? excludedSource)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .Where(entry => !string.Equals(entry.Key, excludedSource, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal);
    }

    private static void AppendModule(RenderDependencyHashWriter writer, string key, IReadOnlyList<ModuleInfo> items)
    {
        writer.AppendLabeledCanonicalValue("site.modules.key", key);
        writer.AppendLabeledCanonicalValue("site.modules.count", items.Count);
        foreach (var item in items.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            writer.AppendLabeledCanonicalValue("site.modules.item", new Dictionary<string, object?>
            {
                ["id"] = item.Id,
                ["title"] = item.Title,
                ["slug"] = item.Slug,
                ["content"] = item.Content,
                ["fields"] = item.Fields
            });
        }
    }
}
