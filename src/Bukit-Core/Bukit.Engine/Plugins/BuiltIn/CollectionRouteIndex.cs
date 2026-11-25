using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Primitives;
namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;

internal sealed class CollectionRouteIndex
{
    private readonly Dictionary<string, IReadOnlyList<RoutedContentDocument>> _byCollection;

    private CollectionRouteIndex(
        IReadOnlyList<RoutedContentDocument> allOrdered,
        Dictionary<string, IReadOnlyList<RoutedContentDocument>> byCollection)
    {
        AllOrdered = allOrdered;
        _byCollection = byCollection;
    }

    internal IReadOnlyList<RoutedContentDocument> AllOrdered { get; }

    /// <param name="tieBreak">Required so every caller of the shared per-build cache agrees on one order.</param>
    internal static CollectionRouteIndex GetOrBuild(BuildContext context, ContentTieBreak? tieBreak)
    {
        return context.GetOrAdd(BuildDataKeys.CollectionRouteIndex, () => Create(context.RoutedDocuments, tieBreak));
    }

    /// <param name="tieBreak">Orders entries with the same publish time (content.ordering); null keeps URL order.</param>
    internal static CollectionRouteIndex Create(IReadOnlyList<RoutedContentDocument> routed, ContentTieBreak? tieBreak)
    {
        var ordered = OrderEntries(routed.Where(x => !string.IsNullOrWhiteSpace(GetCollection(x.Document))), tieBreak).ToList();

        var byCollection = ordered
            .GroupBy(x => GetCollection(x.Document), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<RoutedContentDocument>)g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        return new CollectionRouteIndex(ordered, byCollection);
    }

    /// <summary>The collection order: newest first, then content.ordering, URL and id, so it is stable byte for byte.</summary>
    internal static IOrderedEnumerable<RoutedContentDocument> OrderEntries(IEnumerable<RoutedContentDocument> items, ContentTieBreak? tieBreak)
        => items
            .OrderByDescending(x => x.Document.PublishAt)
            .ThenByTieBreak(tieBreak, x => x.Document, x => x.Route.Url)
            .ThenBy(x => x.Route.Url, StringComparer.Ordinal)
            .ThenBy(x => x.Document.Id, StringComparer.Ordinal);

    internal IReadOnlyList<RoutedContentDocument> GetByCollection(string collectionKey)
    {
        if (string.IsNullOrWhiteSpace(collectionKey))
        {
            return Array.Empty<RoutedContentDocument>();
        }

        return _byCollection.TryGetValue(collectionKey, out var items)
            ? items
            : Array.Empty<RoutedContentDocument>();
    }

    internal IReadOnlyList<RoutedContentDocument> GetByRoutePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Array.Empty<RoutedContentDocument>();
        }

        return AllOrdered
            .Where(x => x.Route.Url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    internal static string GetCollection(ContentDocument document)
    {
        return ContentFieldReader.GetCollection(document);
    }
}
