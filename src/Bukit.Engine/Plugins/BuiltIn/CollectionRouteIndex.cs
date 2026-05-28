using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;

internal sealed class CollectionRouteIndex
{
    private const string CacheKey = "__collection_route_index";

    private readonly Dictionary<string, IReadOnlyList<(ContentItem Item, RouteInfo Route)>> _byCollection;

    private CollectionRouteIndex(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> allOrdered,
        Dictionary<string, IReadOnlyList<(ContentItem Item, RouteInfo Route)>> byCollection)
    {
        AllOrdered = allOrdered;
        _byCollection = byCollection;
    }

    internal IReadOnlyList<(ContentItem Item, RouteInfo Route)> AllOrdered { get; }

    internal static CollectionRouteIndex GetOrBuild(BuildContext context)
    {
        if (context.Data.TryGetValue(CacheKey, out var cached) && cached is CollectionRouteIndex existing)
        {
            return existing;
        }

        var index = Create(context.Routed);
        context.Data[CacheKey] = index;
        return index;
    }

    internal static CollectionRouteIndex Create(IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
    {
        var ordered = routed
            .OrderByDescending(x => x.Item.PublishAt)
            .ToList();

        var byCollection = ordered
            .GroupBy(x => GetCollection(x.Item), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<(ContentItem Item, RouteInfo Route)>)g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        return new CollectionRouteIndex(ordered, byCollection);
    }

    internal IReadOnlyList<(ContentItem Item, RouteInfo Route)> GetByCollection(string collectionKey)
    {
        if (string.IsNullOrWhiteSpace(collectionKey))
        {
            return Array.Empty<(ContentItem Item, RouteInfo Route)>();
        }

        return _byCollection.TryGetValue(collectionKey, out var items)
            ? items
            : Array.Empty<(ContentItem Item, RouteInfo Route)>();
    }

    internal IReadOnlyList<(ContentItem Item, RouteInfo Route)> GetByRoutePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Array.Empty<(ContentItem Item, RouteInfo Route)>();
        }

        return AllOrdered
            .Where(x => x.Route.Url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    internal static string GetCollection(ContentItem item)
    {
        return item.GetCollection();
    }
}
