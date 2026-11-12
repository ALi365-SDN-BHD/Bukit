using Bukit.Theme;
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
        ILogger logger,
        IReadOnlyList<PublishProjectionResult> projectionResults,
        ReportStageTimer? reportTimer = null)
    {
        var searchSnippetsEnabled = templateResolver.TryResolveKindTemplate(
                "search",
                out var searchTemplate) &&
            TemplateCapabilitiesResolver.SupportsSearchSnippets(
                searchTemplate,
                context.LayoutsDir);
        var pluginContext = routePipelineResult.PluginContext;
        var layout = I18nOutputLayout.Create(
            context.Config.Site,
            I18nOutputMerger.GetLanguages(context.Config.Site),
            context.RootBaseUrl ?? context.Config.Site.BaseUrl);
        var reportDir = layout.VariantReportDir(context.OutputDir, context.Config.Site.Language);
        var auditReadPaths = layout.AuditReadPaths(context.OutputDir, context.Config.Site.Language, context.StaticDir);
        var result = new BuildReportPipeline().Execute(reportTimer, new BuildReportPipelineContext(
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
            ContentGraph: context.ContentGraph,
            ProjectionResults: projectionResults,
            ReportDirectory: reportDir,
            AuditReadPaths: auditReadPaths));
        analyticsBuildState.RecordRenderOutcome(
            renderPipelineResult.RenderedCount,
            renderPipelineResult.SkippedCount);
        var analyticsSnapshot = analyticsBuildState.Snapshot();
        Time(reportTimer, "analytics-report", () => AnalyticsReportWriter.WriteIfEnabled(
            context.Config,
            context.OutputDir,
            analyticsSnapshot,
            reportDir));
        return Task.FromResult(result with { SignedLinks = renderPipelineResult.SignedLinks });
    }

    private static void Time(ReportStageTimer? timer, string step, Action action)
    {
        if (timer is null)
        {
            action();
            return;
        }

        timer.Step(step, action);
    }

    internal static IReadOnlyList<PluginOutputTrackingInfo> GetPluginOutputs(
        BuildContext pluginContext)
    {
        if (!pluginContext.TryGet(BuildDataKeys.PluginOutputs, out var outputs))
        {
            return Array.Empty<PluginOutputTrackingInfo>();
        }

        return outputs.ToList();
    }
}
