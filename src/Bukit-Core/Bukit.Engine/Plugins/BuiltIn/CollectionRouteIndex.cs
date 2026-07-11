using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;

internal sealed class CollectionRouteIndex
{
    private const string CacheKey = "__collection_route_index";

    private readonly Dictionary<string, IReadOnlyList<RoutedContentDocument>> _byCollection;

    private CollectionRouteIndex(
        IReadOnlyList<RoutedContentDocument> allOrdered,
        Dictionary<string, IReadOnlyList<RoutedContentDocument>> byCollection)
    {
        AllOrdered = allOrdered;
        _byCollection = byCollection;
    }

    internal IReadOnlyList<RoutedContentDocument> AllOrdered { get; }

    internal static CollectionRouteIndex GetOrBuild(BuildContext context)
    {
        if (context.Data.TryGetValue(CacheKey, out var cached) && cached is CollectionRouteIndex existing)
        {
            return existing;
        }

        var index = Create(context.RoutedDocuments);
        context.Data[CacheKey] = index;
        return index;
    }

    internal static CollectionRouteIndex Create(IReadOnlyList<RoutedContentDocument> routed)
    {
        var ordered = routed
            .Where(x => !string.IsNullOrWhiteSpace(GetCollection(x.Document)))
            .OrderByDescending(x => x.Document.PublishAt)
            .ToList();

        var byCollection = ordered
            .GroupBy(x => GetCollection(x.Document), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<RoutedContentDocument>)g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        return new CollectionRouteIndex(ordered, byCollection);
    }

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
