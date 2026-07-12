using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.RouteMetadata;

namespace Bukit.Engine;

internal sealed partial class VariantBuildPipeline
{
    private static RoutePipelineResult AddDerivedListRoutesToGraph(
        RoutePipelineResult routeResult,
        BuildContext pluginContext,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
    {
        var graph = ListRouteGraphBuilder.AddDerivedTaxonomyRoutes(
            routeResult.ListRouteGraph,
            pluginContext.DerivedDocuments);
        graph = ListRouteGraphBuilder.ApplyRouteMetadata(graph, routeMetadata);
        pluginContext.Data[ListRouteGraphBuilder.BuildContextDataKey] = graph;

        return routeResult with
        {
            ListRoutes = graph.Routes.Select(route => route.ToRouteInfo()).ToArray(),
            ListRouteGraph = graph
        };
    }

    private static void ValidatePostDeriveRoutes(BuildRoutePipelineResult result)
    {
        var specialRoutes = result.RouteResult.ListRouteGraph.Routes
            .Where(route => route.Kind is not ListRouteKind.TaxonomyIndex and not ListRouteKind.TaxonomyTermPage)
            .Select(route => route.ToRouteInfo())
            .ToArray();

        RouteInventoryValidator.ValidateFinalRoutes(
            result.RouteResult.RoutedDocuments,
            result.PluginContext.DerivedDocuments,
            specialRoutes,
            result.StaticHtmlRoutes);
    }
}
