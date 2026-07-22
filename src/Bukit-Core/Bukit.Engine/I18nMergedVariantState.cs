using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record I18nMergedVariantState(
    IReadOnlyList<RoutedContentDocument> RoutedDocuments,
    IReadOnlyList<RoutedContentDocument> DerivedDocuments,
    CanonicalContentGraph ContentGraph,
    IContentBodyStore BodyStore,
    IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex,
    IReadOnlyDictionary<string, Bukit.Rendering.SeoModel> SeoModels)
{
    internal static I18nMergedVariantState Create(IReadOnlyList<BuildVariantResult> results)
    {
        var routedDocuments = new List<RoutedContentDocument>();
        var derivedDocuments = new List<RoutedContentDocument>();
        var records = new List<ContentRecord>();
        var entities = new List<EntityRecord>();
        var seoModels = new Dictionary<string, Bukit.Rendering.SeoModel>(StringComparer.OrdinalIgnoreCase);
        var bodySources = new Dictionary<string, (ContentDocument Document, IContentBodyStore Store)>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            routedDocuments.AddRange(result.RoutedDocuments.Select(x => x with { Route = MergeRoute(result, x.Route) }));
            derivedDocuments.AddRange(result.DerivedDocuments.Select(x => x with { Route = MergeRoute(result, x.Route) }));
            foreach (var routedDocument in result.RoutedDocuments.Concat(result.DerivedDocuments))
            {
                bodySources[BuildBodyStoreKey(routedDocument.Document)] = (routedDocument.Document, result.BodyStore);
            }

            records.AddRange((result.ContentGraph ?? CanonicalContentGraph.Empty).Records);
            entities.AddRange((result.ContentGraph ?? CanonicalContentGraph.Empty).Entities);
            foreach (var (key, model) in result.SeoModels)
            {
                seoModels[BuildMergedKey(result.Language, key)] = model;
            }
        }

        return new I18nMergedVariantState(
            routedDocuments,
            derivedDocuments,
            new CanonicalContentGraph(records, entities),
            new MergedVariantContentBodyStore(bodySources),
            BuildSeoIndex(results),
            seoModels);
    }

    internal static IReadOnlyDictionary<string, SeoIndexEntry> BuildSeoIndex(
        IReadOnlyList<BuildVariantResult> results)
    {
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            foreach (var (key, entry) in result.SeoIndex)
            {
                seoIndex[BuildMergedKey(result.Language, key)] = entry with
                {
                    Route = MergeRoute(result, entry.Route)
                };
            }
        }

        return seoIndex;
    }

    private static RouteInfo MergeRoute(BuildVariantResult result, RouteInfo route)
        => new(
            I18nRootProjectionPath.CombineBaseUrl(result.BaseUrl, route.Url),
            Path.Combine(result.Language, route.OutputPath),
            route.Template);

    private static string BuildBodyStoreKey(ContentDocument document)
    {
        var language = document.Record.Presentation.Language;
        if (string.IsNullOrWhiteSpace(language) ||
            string.Equals(language, "und", StringComparison.OrdinalIgnoreCase))
        {
            language = ContentFieldReader.GetText(document.CustomFields, "language") ?? string.Empty;
        }

        return document.Id + "\n" + language;
    }

    private static string BuildMergedKey(string language, string key) => language + "/" + key;

    private sealed class MergedVariantContentBodyStore : IContentBodyStore
    {
        private readonly IReadOnlyDictionary<string, (ContentDocument Document, IContentBodyStore Store)> _sources;

        internal MergedVariantContentBodyStore(
            IReadOnlyDictionary<string, (ContentDocument Document, IContentBodyStore Store)> sources)
        {
            _sources = sources;
        }

        public Task<ContentBody> GetAsync(
            ContentDocument document,
            CancellationToken cancellationToken = default)
        {
            if (_sources.TryGetValue(BuildBodyStoreKey(document), out var source))
            {
                return source.Store.GetAsync(source.Document, cancellationToken);
            }

            return NullContentBodyStore.Instance.GetAsync(document, cancellationToken);
        }
    }
}
