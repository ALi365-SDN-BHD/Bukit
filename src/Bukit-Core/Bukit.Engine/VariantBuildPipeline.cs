using System.Diagnostics;
using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed partial class VariantBuildPipeline
{
    internal async Task<BuildVariantResult> ExecuteAsync(
        BuildVariantContext context,
        DirectoryHashCache templateHashCache,
        Func<string, ITemplateRenderer>? rendererFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var variantTotalStopwatch = Stopwatch.StartNew();
        var variantStageMetrics = new BuildStageMetricsCollector();

        Directory.CreateDirectory(context.OutputDir);

        var bootstrap = await BootstrapThemeAsync(
            context.Config,
            context.RootDir,
            logger);
        var templateResolver = new ThemeTemplateResolver(bootstrap.Manifest);
        templateResolver.ValidateRequiredTemplates();

        var dataModules = await VariantDataSitePlanner.PrepareDataModulesStageAsync(
            context,
            variantStageMetrics);
        var routePipelineResult = await VariantRouteStage.ExecuteAsync(
            context,
            dataModules.DataDocuments,
            templateResolver,
            logger,
            variantStageMetrics,
            cancellationToken);
        var pluginSession = PluginExecutionSession.Create(
            context.Config,
            context.Overrides.ExecutionMode);

        await VariantPluginStages.RunDeriveAsync(
            routePipelineResult.PluginContext,
            pluginSession,
            variantStageMetrics,
            cancellationToken);
        routePipelineResult = routePipelineResult with
        {
            RouteResult = AddDerivedListRoutesToGraph(
                routePipelineResult.RouteResult,
                routePipelineResult.PluginContext,
                dataModules.RouteMetadata)
        };
        ValidatePostDeriveRoutes(routePipelineResult);

        var rendererThemePlan = VariantRendererThemePlanner.Create(
            context,
            bootstrap,
            routePipelineResult.RouteResult,
            rendererFactory);
        var siteModel = VariantDataSitePlanner.BuildSiteModel(
            context.Config,
            context.BaseUrl,
            dataModules.Modules,
            dataModules.SourceData,
            routePipelineResult.PluginContext.Data,
            dataModules.DataIndex,
            context.BuildStartedAt);
        var manifestSetup = VariantManifestPlanner.Create(
            context,
            context.Overrides,
            templateHashCache);
        var renderAssetPlan = VariantRenderAssetPlanner.Create(
            context,
            routePipelineResult.RouteResult,
            routePipelineResult.PluginContext.DerivedDocuments,
            routePipelineResult.StaticEntries,
            siteModel,
            manifestSetup,
            rendererThemePlan.ThemeRootForTokens,
            rendererThemePlan.ParentThemeRootForTokens,
            logger);

        var assetPipelinePreparation = await AssetPipeline.PrepareAsync(
            renderAssetPlan.AssetPipelineContext,
            cancellationToken);

        // Remove only prior owned destinations before aggregate writers inspect existing files.
        PublicOutputLifecycle.PrepareProjectionWrites(context.OutputDir, manifestSetup.Manifest, assetPipelinePreparation.OutputPlan.Items);

        var seoStage = await VariantSeoStage.ExecuteAsync(
            context,
            renderAssetPlan,
            routePipelineResult,
            dataModules.RouteMetadata,
            logger);
        var analyticsTransformPlan = VariantAnalyticsTransformStage.Create(
            context.Overrides,
            routePipelineResult.PluginContext,
            pluginSession,
            seoStage.SeoResult);

        try
        {
            var renderPipelineResult = await ExecuteRenderWithHtmlTransformRecordingAsync(
                analyticsTransformPlan.PluginHtmlTransforms,
                () => VariantRenderStage.ExecuteAsync(
                    context,
                    routePipelineResult,
                    renderAssetPlan,
                    rendererThemePlan.Renderer,
                    siteModel,
                    manifestSetup,
                    seoStage,
                    analyticsTransformPlan.HtmlTransformPipeline,
                    templateResolver,
                    dataModules.RouteMetadata,
                    variantStageMetrics,
                    logger,
                    cancellationToken));

            var assetPipelineResult = await AssetPipeline.ExecutePreparedAsync(
                renderAssetPlan.AssetPipelineContext,
                assetPipelinePreparation,
                cancellationToken);
            variantStageMetrics.Merge(assetPipelineResult.StageMetrics);

            await VariantPluginStages.RunAfterBuildAsync(
                context,
                routePipelineResult.PluginContext,
                pluginSession,
                manifestSetup,
                renderPipelineResult,
                variantStageMetrics,
                logger,
                cancellationToken);

            variantTotalStopwatch.Stop();
            variantStageMetrics.AddDuration(
                "variantTotal",
                variantTotalStopwatch.ElapsedMilliseconds);

            analyticsTransformPlan.PluginHtmlTransforms.RecordExecutions();
            var plannedOutputs = assetPipelinePreparation.OutputPlan.Items.Concat(
                VariantReportStage.GetPluginOutputs(routePipelineResult.PluginContext).Select(output =>
                    new AssetOutputItem(output.Plugin, output.Path, AssetOutputCategory.Plugin, Operation: AssetOutputOperation.Generate))).ToArray();
            AssetOutputPlan.Validate(plannedOutputs, assetPipelinePreparation.OutputPlan.DestinationComparer);
            var searchSnippets = templateResolver.TryResolveKindTemplate("search", out var searchTemplate) &&
                TemplateCapabilitiesResolver.SupportsSearchSnippets(searchTemplate, context.LayoutsDir);
            var projectionResults = new DefaultContentProjectionWriter().Write(new PublishProjectionContext(
                context.Config, context.OutputDir, context.ContentGraph, seoStage.SeoResult.SeoIndex.Entries,
                seoStage.SeoResult.SeoIndex.Models, routePipelineResult.PluginContext.RoutedDocuments,
                context.BodyStore, context.BaseUrl, searchSnippets, logger,
                routePipelineResult.RouteResult.ListRouteGraph,
                DerivedDocuments: routePipelineResult.PluginContext.DerivedDocuments));
            PublicOutputLifecycle.Record(manifestSetup.Manifest, context.OutputDir, plannedOutputs);
            var result = await VariantReportStage.ExecuteAsync(
                context, routePipelineResult, seoStage.SeoResult, renderPipelineResult,
                variantStageMetrics, templateResolver, analyticsTransformPlan.AnalyticsBuildState, logger, projectionResults);
            return result with { PendingManifest = manifestSetup, PlannedOutputs = plannedOutputs };
        }
        finally
        {
            analyticsTransformPlan.PluginHtmlTransforms.RecordExecutions();
        }
    }

    internal static async Task<T> ExecuteRenderWithHtmlTransformRecordingAsync<T>(
        CollectedHtmlTransforms pluginHtmlTransforms,
        Func<Task<T>> render)
    {
        try
        {
            return await render();
        }
        finally
        {
            pluginHtmlTransforms.RecordExecutions();
        }
    }

    private static Task<ThemeBootstrapResult> BootstrapThemeAsync(
        AppConfig config,
        string rootDir,
        ILogger logger)
        => Task.FromResult(ThemeBootstrapper.BootstrapRequired(config, rootDir, logger));
}
