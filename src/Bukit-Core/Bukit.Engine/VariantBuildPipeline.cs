using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Engine.Plugins;
using Bukit.Engine.RouteMetadata;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

internal sealed partial class VariantBuildPipeline
{
    internal Task<DataModuleResult> PrepareDataModulesAsync(
        IReadOnlyList<ContentDocument> documents,
        string language,
        IContentBodyStore bodyStore,
        IReadOnlyList<ContentSourceConfig>? sources = null,
        RouteMetadataConfig? routeMetadata = null,
        CancellationToken cancellationToken = default)
        => VariantDataSitePlanner.PrepareDataModulesAsync(
            documents,
            language,
            bodyStore,
            sources,
            routeMetadata,
            cancellationToken);

    internal RoutePipelineResult GenerateRoutes(
        AppConfig config,
        IReadOnlyList<ContentDocument> documents,
        ThemeTemplateResolver templateResolver)
        => new RoutePipeline().Execute(config, documents, templateResolver);

    internal ITemplateRenderer CreateRenderer(
        BuildVariantContext context,
        ThemeComponentRegistry? themeRegistry,
        SectionSchemaValidator? schemaValidator,
        IReadOnlyDictionary<string, ISectionPlugin>? resolvedSectionPlugins,
        IReadOnlyList<(ContentDocument, RouteInfo?)>? allPagesForSections)
        => VariantRendererThemePlanner.CreateRenderer(
            context,
            themeRegistry,
            schemaValidator,
            resolvedSectionPlugins,
            allPagesForSections);

    internal SiteModel BuildSiteModel(
        AppConfig config,
        string baseUrl,
        IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? modules,
        IReadOnlyDictionary<string, object>? sourceData,
        IReadOnlyDictionary<string, object>? pluginData = null,
        IReadOnlyDictionary<string, object>? dataIndex = null,
        DateTimeOffset? buildStartedAt = null)
        => VariantDataSitePlanner.BuildSiteModel(
            config,
            baseUrl,
            modules,
            sourceData,
            pluginData,
            dataIndex,
            buildStartedAt);

    internal ManifestSetupResult SetupManifest(
        BuildVariantContext context,
        ConfigOverrides overrides,
        DirectoryHashCache templateHashCache)
        => VariantManifestPlanner.Create(context, overrides, templateHashCache);

    internal (IReadOnlyList<RouteInfo> StaticHtmlRoutes, string? StaticRouteTemplate) BuildStaticHtmlData(
        string? staticDir,
        string? staticTemplate,
        Action<string> warn,
        bool publishDotFiles)
    {
        var template = !string.IsNullOrWhiteSpace(staticTemplate) ? staticTemplate : null;
        var hasStaticDir = staticDir is not null && Directory.Exists(staticDir);
        var hasStaticHtmlFiles = hasStaticDir &&
            SafeFileEnumerator.EnumerateFiles(staticDir!, "*.html").Any();
        if (template is null)
        {
            if (hasStaticHtmlFiles)
            {
                warn("Static HTML files in static dir are skipped because no static template is configured (theme.staticTemplate).");
            }

            return (Array.Empty<RouteInfo>(), null);
        }

        var routes = hasStaticDir
            ? StaticFileService.BuildStaticHtmlRoutes(
                staticDir!,
                template,
                warn,
                publishDotFiles)
            : Array.Empty<RouteInfo>();
        return (routes, template);
    }

    internal (string? ThemeRoot, string? ParentThemeRoot) GetThemeRootForTokens(
        string? themeRoot,
        bool hasRegistry,
        string? parentThemeRoot,
        bool hasExtends)
        => VariantRendererThemePlanner.GetThemeRootForTokens(
            themeRoot,
            hasRegistry,
            parentThemeRoot,
            hasExtends);

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
            return await VariantReportStage.ExecuteAsync(
                context,
                routePipelineResult,
                seoStage.SeoResult,
                renderPipelineResult,
                variantStageMetrics,
                templateResolver,
                analyticsTransformPlan.AnalyticsBuildState,
                logger);
        }
        finally
        {
            analyticsTransformPlan.PluginHtmlTransforms.RecordExecutions();
        }
    }

    internal static HtmlTransformPipeline CreateHtmlTransformPipeline(
        SeoPipelineResult seoResult,
        CollectedHtmlTransforms pluginHtmlTransforms,
        BuildExecutionMode executionMode)
        => VariantAnalyticsTransformStage.CreateHtmlTransformPipeline(
            seoResult,
            pluginHtmlTransforms,
            executionMode);

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
