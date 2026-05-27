using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine;

public sealed record RoutePipelineResult(
    IReadOnlyList<ContentItem> ContentItems,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> Routed,
    IReadOnlyList<RouteInfo> ListRoutes);

public sealed class RoutePipeline
{
    public RoutePipelineResult Execute(AppConfig config, IReadOnlyList<ContentItem> items)
    {
        var contentItems = items.Where(i => !MetaHelpers.IsDataItem(i)).ToList();
        var collectionRules = RouteInventoryValidator.BuildCollectionRules(config.Site);
        var routed = contentItems
            .Select(i => (Item: i, Route: RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .ToList();

        RouteInventoryValidator.ValidateContentRoutes(routed);
        var listRoutes = SeoAlternatesService.BuildListRoutes(config.Site.Collections);
        return new RoutePipelineResult(contentItems, routed, listRoutes);
    }
}
