using Bukit.Theme;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
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
        var stageStarted = Stopwatch.GetTimestamp();

        var media = context.Config.Content.Media.DownloadToLocal ? new ContentMediaOutput(context) : null;
        if (media is not null)
            context = context with { BodyStore = media, Documents = [.. context.Documents.Select(document => media.NormalizeDocument(document))] };
        Directory.CreateDirectory(context.OutputDir);

        var bootstrap = await BootstrapThemeAsync(
            context.Config,
            context.RootDir,
            logger);
        var templateResolver = new ThemeTemplateResolver(bootstrap.Manifest);
        templateResolver.ValidateRequiredTemplates();
        RecordStage("variantSetup");

        var dataModules = await VariantDataSitePlanner.PrepareDataModulesStageAsync(
            context,
            variantStageMetrics,
            cancellationToken);
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
        I18nLegacyRedirectDeriver.Derive(context, routePipelineResult.PluginContext);
        routePipelineResult = routePipelineResult with
        {
            RouteResult = AddDerivedListRoutesToGraph(
                routePipelineResult.RouteResult,
                routePipelineResult.PluginContext,
                dataModules.RouteMetadata)
        };
        ValidatePostDeriveRoutes(routePipelineResult);
        I18nReservedPrefixGuard.ValidateRoutes(
            context.Config,
            routePipelineResult.RouteResult.RoutedDocuments,
            routePipelineResult.PluginContext.DerivedDocuments,
            routePipelineResult.RouteResult.ListRoutes);
        I18nReservedPrefixGuard.ValidateStaticRoutes(context.Config, routePipelineResult.StaticHtmlRoutes);
        stageStarted = Stopwatch.GetTimestamp();
        if (media is not null)
        {
            if (routePipelineResult.PluginContext.TryGet(BuildDataKeys.PagesById, out var indexedPages))
                media.NormalizeIndexedPages(indexedPages);
            for (var i = 0; i < routePipelineResult.PluginContext.DerivedDocuments.Count; i++)
            {
                var derived = routePipelineResult.PluginContext.DerivedDocuments[i];
                routePipelineResult.PluginContext.DerivedDocuments[i] = derived with { Document = media.NormalizeDocument(derived.Document) };
            }
            foreach (var document in dataModules.DataDocuments) media.NormalizeDocument(document, collect: true);
            await media.ResolveAsync(routePipelineResult.RouteResult.RoutedDocuments
                .Concat(routePipelineResult.PluginContext.DerivedDocuments).Select(document => document.Document), cancellationToken);
            RecordStage("mediaResolve");
        }

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
            routePipelineResult.PluginContext.SnapshotData(),
            dataModules.DataIndex,
            context.BuildStartedAt);
        var manifestSetup = VariantManifestPlanner.Create(
            context,
            context.Overrides,
            templateHashCache,
            logger);
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

        renderAssetPlan = renderAssetPlan with
        {
            AssetPipelineContext = renderAssetPlan.AssetPipelineContext with { MediaOutputs = media?.Plan(logger, cancellationToken) }
        };
        RecordStage("renderPlan");
        var assetPipelinePreparation = await AssetPipeline.PrepareAsync(
            renderAssetPlan.AssetPipelineContext,
            cancellationToken);
        I18nReservedPrefixGuard.ValidateAssetPlan(context.Config, assetPipelinePreparation.OutputPlan.Items);

        // Remove only prior owned destinations before aggregate writers inspect existing files.
        PublicOutputLifecycle.PrepareProjectionWrites(context.OutputDir, manifestSetup.Manifest, assetPipelinePreparation.OutputPlan.Items);
        RecordStage("assetPrepare");

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
        RecordStage("seoPrepare");

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
            I18nReservedPrefixGuard.ValidatePluginOutputs(
                context.Config,
                VariantReportStage.GetPluginOutputs(routePipelineResult.PluginContext));
            var plannedOutputs = assetPipelinePreparation.OutputPlan.Items.Concat(
                VariantReportStage.GetPluginOutputs(routePipelineResult.PluginContext).Select(output =>
                    new AssetOutputItem(output.Plugin, output.Path, AssetOutputCategory.Plugin, Operation: AssetOutputOperation.Generate))).ToArray();
            AssetOutputPlan.Validate(plannedOutputs, assetPipelinePreparation.OutputPlan.DestinationComparer);
            ValidateGeneratedOutputsDoNotReplacePages(
                routePipelineResult.RouteResult.RoutedDocuments.Select(document => document.Route)
                    .Concat(routePipelineResult.PluginContext.DerivedDocuments.Select(document => document.Route))
                    .Concat(routePipelineResult.RouteResult.ListRoutes)
                    .Concat(routePipelineResult.StaticHtmlRoutes),
                plannedOutputs,
                assetPipelinePreparation.OutputPlan.DestinationComparer);
            var result = await VariantReportRunner.RunAsync(
                context, routePipelineResult, seoStage, renderPipelineResult, variantStageMetrics, templateResolver,
                analyticsTransformPlan.AnalyticsBuildState, manifestSetup, media, plannedOutputs, logger, cancellationToken);
            return result with { PendingManifest = manifestSetup, PlannedOutputs = plannedOutputs };
        }
        finally
        {
            analyticsTransformPlan.PluginHtmlTransforms.RecordExecutions();
        }

        // Records the wall-clock time since the previous mark and starts the next one.
        void RecordStage(string stage)
        {
            var now = Stopwatch.GetTimestamp();
            variantStageMetrics.AddElapsedTicks(stage, now - stageStarted);
            stageStarted = now;
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

    /// <summary>
    /// Plugin and projection writers (feeds, search, llms, sitemap, tracked plugin files) run after
    /// pages are rendered; one that lands on a page would silently replace it and own it twice.
    /// Defensive only: every page route here is also a render entry, so <see cref="AssetOutputPlan.Validate"/>
    /// reports such collisions first; this guards writers that might bypass the asset plan.
    /// </summary>
    internal static void ValidateGeneratedOutputsDoNotReplacePages(
        IEnumerable<RouteInfo> pageRoutes,
        IEnumerable<AssetOutputItem> plannedOutputs,
        StringComparer comparer)
    {
        var generated = plannedOutputs
            .Where(item => item.Category is AssetOutputCategory.Plugin or AssetOutputCategory.Projection)
            .ToList();
        if (generated.Count == 0)
        {
            return;
        }

        var pages = pageRoutes
            .GroupBy(route => BuildPathUtils.NormalizeRelPath(route.OutputPath), comparer)
            .ToDictionary(group => group.Key, group => group.First().Url, comparer);
        foreach (var item in generated)
        {
            var path = BuildPathUtils.NormalizeRelPath(item.Destination);
            if (pages.TryGetValue(path, out var url))
            {
                var owner = item.Category == AssetOutputCategory.Plugin ? "Plugin" : "Projection";
                throw new InvalidOperationException(
                    $"Bukit defect: {owner} '{item.Source}' output '{path}' replaces the page for route '{url}' after the asset plan.");
            }
        }
    }
}
