using Bukit.Engine.Analytics;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class VariantReportStage
{
    internal static Task<BuildVariantResult> ExecuteAsync(
        BuildVariantContext context,
        BuildRoutePipelineResult routePipelineResult,
        SeoPipelineResult seoResult,
        RenderPipelineResult renderPipelineResult,
        BuildStageMetricsCollector variantStageMetrics,
        ThemeTemplateResolver templateResolver,
        AnalyticsBuildState analyticsBuildState,
        ILogger logger)
    {
        var searchSnippetsEnabled = templateResolver.TryResolveKindTemplate(
                "search",
                out var searchTemplate) &&
            TemplateCapabilitiesResolver.SupportsSearchSnippets(
                searchTemplate,
                context.LayoutsDir);
        var pluginContext = routePipelineResult.PluginContext;
        var result = new BuildReportPipeline().Execute(new BuildReportPipelineContext(
            Config: context.Config,
            Language: context.Config.Site.Language,
            OutputDir: context.OutputDir,
            BaseUrl: context.BaseUrl,
            SearchSnippetsEnabled: searchSnippetsEnabled,
            BodyStore: context.BodyStore,
            RoutedDocuments: pluginContext.RoutedDocuments,
            ListRouteGraph: routePipelineResult.RouteResult.ListRouteGraph,
            DerivedDocuments: pluginContext.DerivedDocuments,
            DerivedRoutes: pluginContext.DerivedRoutes,
            SeoIndex: seoResult.SeoIndex.Entries,
            SeoModels: seoResult.SeoIndex.Models,
            PluginExecutions: pluginContext.PluginExecutions.ToList(),
            StaticRoutes: pluginContext.StaticHtmlRoutes,
            PluginOutputs: GetPluginOutputs(pluginContext),
            RenderedCount: renderPipelineResult.RenderedCount,
            SkippedCount: renderPipelineResult.SkippedCount,
            RenderReasons: new Dictionary<string, int>(
                renderPipelineResult.RenderReasons,
                StringComparer.OrdinalIgnoreCase),
            StageMetrics: variantStageMetrics.Snapshot(),
            Logger: logger,
            DefaultLanguage: context.DefaultLanguage,
            ContentGraph: context.ContentGraph));
        analyticsBuildState.RecordRenderOutcome(
            renderPipelineResult.RenderedCount,
            renderPipelineResult.SkippedCount);
        AnalyticsReportWriter.WriteIfEnabled(
            context.Config,
            context.OutputDir,
            analyticsBuildState.Snapshot());
        return Task.FromResult(result);
    }

    private static IReadOnlyList<PluginOutputTrackingInfo> GetPluginOutputs(
        BuildContext pluginContext)
    {
        if (!pluginContext.Data.TryGetValue("__plugin_outputs", out var outputsObject) ||
            outputsObject is not HashSet<PluginOutputTrackingInfo> outputs)
        {
            return Array.Empty<PluginOutputTrackingInfo>();
        }

        return outputs.ToList();
    }
}
