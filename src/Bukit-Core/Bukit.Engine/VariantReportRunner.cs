using Bukit.Engine.Analytics;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

/// <summary>
/// The timed report steps after rendering: content projections, media output validation, the
/// output record and the report stage, each logged as <c>event=build.report.timing</c>.
/// </summary>
internal static class VariantReportRunner
{
    internal static async Task<BuildVariantResult> RunAsync(
        BuildVariantContext context,
        BuildRoutePipelineResult routePipelineResult,
        SeoStageResult seoStage,
        RenderPipelineResult renderPipelineResult,
        BuildStageMetricsCollector variantStageMetrics,
        ThemeTemplateResolver templateResolver,
        AnalyticsBuildState analyticsBuildState,
        ManifestSetupResult manifestSetup,
        ContentMediaOutput? media,
        IReadOnlyList<AssetOutputItem> plannedOutputs,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var searchSnippets = templateResolver.TryResolveKindTemplate("search", out var searchTemplate) &&
            TemplateCapabilitiesResolver.SupportsSearchSnippets(searchTemplate, context.LayoutsDir);
        var reportTimer = new ReportStageTimer(
            logger,
            ReportStageTimer.VariantScope,
            context.DefaultLanguage is null ? null : context.Config.Site.Language);
        var projectionResults = await reportTimer.StepAsync(
            "projections",
            () => WriteProjectionsAsync(context, routePipelineResult, seoStage, searchSnippets, logger, cancellationToken)).ConfigureAwait(false);
        if (media is not null)
        {
            reportTimer.Step("media-validate", () => media.ValidateOutputs(plannedOutputs));
        }

        reportTimer.Step("output-record", () => PublicOutputLifecycle.Record(manifestSetup.Manifest, context.OutputDir, plannedOutputs));
        var result = await VariantReportStage.ExecuteAsync(
            context, routePipelineResult, seoStage.SeoResult, renderPipelineResult,
            variantStageMetrics, templateResolver, analyticsBuildState, logger, projectionResults,
            reportTimer).ConfigureAwait(false);
        reportTimer.Complete();
        return result;
    }

    private static Task<IReadOnlyList<PublishProjectionResult>> WriteProjectionsAsync(
        BuildVariantContext context,
        BuildRoutePipelineResult routePipelineResult,
        SeoStageResult seoStage,
        bool searchSnippets,
        ILogger logger,
        CancellationToken cancellationToken) =>
        new DefaultContentProjectionWriter().WriteAsync(new PublishProjectionContext(
            context.Config, context.OutputDir, context.ContentGraph, seoStage.SeoResult.SeoIndex.Entries,
            seoStage.SeoResult.SeoIndex.Models, routePipelineResult.PluginContext.RoutedDocuments,
            context.BodyStore, context.BaseUrl, searchSnippets, logger,
            routePipelineResult.RouteResult.ListRouteGraph,
            DerivedDocuments: routePipelineResult.PluginContext.DerivedDocuments), cancellationToken);
}
