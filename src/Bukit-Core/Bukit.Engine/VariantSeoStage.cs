using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.RouteMetadata;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record SeoStageResult(
    SeoPipelineResult SeoResult,
    IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> SeoAlternates,
    int MaxDegreeOfParallelism);

internal static class VariantSeoStage
{
    internal static Task<SeoStageResult> ExecuteAsync(
        BuildVariantContext context,
        VariantRenderAssetPlan renderAssetPlan,
        BuildRoutePipelineResult routePipelineResult,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata,
        ILogger logger)
    {
        var routeResult = routePipelineResult.RouteResult;
        var pluginContext = routePipelineResult.PluginContext;
        var seoAlternates = SeoAlternatesService.AddVariantRouteAlternates(
            context.Config,
            context.SeoAlternates,
            routeResult.ListRouteGraph,
            context.RootBaseUrl,
            context.DefaultLanguage);
        var maxDegreeOfParallelism = Math.Clamp(
            context.Overrides.Jobs ?? Environment.ProcessorCount,
            1,
            Math.Max(1, Environment.ProcessorCount * 2));
        var searchAction = SearchActionDescriptorResolver.Resolve(
            context.Config,
            context.BaseUrl,
            renderAssetPlan.RenderDocuments.Select(document => document.Route)
                .Concat(renderAssetPlan.ListRoutes)
                .Concat(pluginContext.StaticHtmlRoutes));
        var breadcrumbs = BreadcrumbDescriptorResolver.Resolve(
            context.Config,
            context.BaseUrl,
            renderAssetPlan.RenderDocuments,
            routeResult.ListRouteGraph,
            routePipelineResult.StaticEntries,
            routeMetadata);
        var seoResult = new SeoPipeline().Execute(
            context.Config,
            context.BaseUrl,
            renderAssetPlan.RenderDocuments,
            renderAssetPlan.ListRoutes,
            seoAlternates,
            logger,
            routeResult.ListRouteGraph,
            routeMetadata,
            searchAction,
            breadcrumbs);
        pluginContext.SeoIndex = seoResult.SeoIndex.Entries;
        pluginContext.Data[BuildContextDataKeys.SeoModels] = seoResult.SeoIndex.Models;

        return Task.FromResult(new SeoStageResult(
            seoResult,
            seoAlternates,
            maxDegreeOfParallelism));
    }
}
