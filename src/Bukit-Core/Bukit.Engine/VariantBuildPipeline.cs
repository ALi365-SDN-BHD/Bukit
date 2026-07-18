using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Theme;
using Bukit.Engine.RouteMetadata;
using Bukit.Engine.Analytics;

namespace Bukit.Engine;

internal sealed record DataModuleResult(
    IReadOnlyList<ContentDocument> DataDocuments,
    IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? Modules,
    IReadOnlyDictionary<string, object>? SourceData,
    IReadOnlyDictionary<string, object>? DataIndex,
    IReadOnlyDictionary<string, RouteMetadataEntry>? RouteMetadata);

internal sealed record ManifestSetupResult(
    BuildManifest Manifest,
    string TemplateHash,
    string ManifestPath,
    ConcurrentDictionary<string, BuildManifestEntry>? ManifestEntries,
    bool IncrementalEnabled);

internal sealed record BuildRoutePipelineResult(
    RoutePipelineResult RouteResult,
    IReadOnlyList<RouteInfo> StaticHtmlRoutes,
    IReadOnlyList<RenderEntry>? StaticEntries,
    BuildContext PluginContext);

internal sealed record SeoStageResult(
    SeoPipelineResult SeoResult,
    IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> SeoAlternates,
    int MaxDegreeOfParallelism);

internal sealed partial class VariantBuildPipeline
{
    internal DataModuleResult PrepareDataModules(
        IReadOnlyList<ContentDocument> documents, string language, IContentBodyStore bodyStore,
        IReadOnlyList<ContentSourceConfig>? sources = null,
        RouteMetadataConfig? routeMetadata = null)
    {
        var dataDocuments = documents.Where(ContentFieldReader.IsDataItem).ToList();
        var templateDataDocuments = ExcludeRouteMetadataDocuments(dataDocuments, routeMetadata?.Source);
        var modules = DataModuleBuilder.BuildModules(templateDataDocuments, language, bodyStore);
        var sourceData = DataModuleBuilder.BuildDataBySource(dataDocuments, bodyStore);
        var dataIndex = DataModuleBuilder.BuildDataIndex(templateDataDocuments, sources);
        var routeMetadataIndex = routeMetadata is null
            ? null
            : RouteMetadataIndexBuilder.Build(routeMetadata, sourceData);
        return new DataModuleResult(dataDocuments, modules, sourceData, dataIndex, routeMetadataIndex);
    }

    private static IReadOnlyList<ContentDocument> ExcludeRouteMetadataDocuments(
        IReadOnlyList<ContentDocument> dataDocuments,
        string? reservedSource)
    {
        if (string.IsNullOrWhiteSpace(reservedSource))
        {
            return dataDocuments;
        }

        return dataDocuments
            .Where(document => !string.Equals(
                ContentFieldReader.GetText(document.CustomFields, "sourceKey")?.Trim(),
                reservedSource,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    internal RoutePipelineResult GenerateRoutes(AppConfig config, IReadOnlyList<ContentDocument> documents, ThemeTemplateResolver templateResolver)
    {
        return new RoutePipeline().Execute(config, documents, templateResolver);
    }

    internal ITemplateRenderer CreateRenderer(
        BuildVariantContext ctx, ThemeComponentRegistry? themeRegistry,
        SectionSchemaValidator? schemaValidator,
        IReadOnlyDictionary<string, ISectionPlugin>? resolvedSectionPlugins,
        IReadOnlyList<(ContentDocument, RouteInfo?)>? allPagesForSections)
    {
        var config = ctx.Config;
        return themeRegistry is not null
            ? new ScribanTemplateRendererAdapter(
                ctx.LayoutsDir, ctx.ParentLayoutsDir,
                config.Theme.Shortcodes, config.Theme.Components,
                ctx.UserLayoutsDir, themeRegistry, schemaValidator, null,
                config.Theme.ComponentValidation, allPagesForSections,
                resolvedSectionPlugins)
            : new ScribanTemplateRendererAdapter(
                ctx.LayoutsDir, ctx.ParentLayoutsDir,
                config.Theme.Shortcodes, config.Theme.Components,
                ctx.UserLayoutsDir);
    }

    internal SiteModel BuildSiteModel(
        AppConfig config, string baseUrl,
        IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? modules,
        IReadOnlyDictionary<string, object>? sourceData,
        IReadOnlyDictionary<string, object>? pluginData = null,
        IReadOnlyDictionary<string, object>? dataIndex = null,
        DateTimeOffset? buildStartedAt = null)
    {
        var reservedSource = config.Content.RouteMetadata?.Source;
        var data = ExcludeReservedSource(MergeSiteData(sourceData, pluginData), reservedSource);
        var buildInstant = buildStartedAt ?? DateTimeOffset.UtcNow;
        var buildTimezone = TimeZoneResolver.ResolveOrUtc(config.Site.Timezone);
        return new SiteModel
        {
            Name = config.Site.Name,
            Title = config.Site.Title,
            Url = config.Site.Url,
            Description = config.Site.Description,
            BaseUrl = baseUrl,
            Language = config.Site.Language,
            BuildYear = TimeZoneInfo.ConvertTime(buildInstant, buildTimezone).Year,
            Params = config.Theme.Params,
            Modules = ExcludeReservedSource(modules, reservedSource),
            Data = data,
            DataIndex = ExcludeReservedSource(dataIndex, reservedSource)
        };
    }

    private static IReadOnlyDictionary<string, TValue>? ExcludeReservedSource<TValue>(
        IReadOnlyDictionary<string, TValue>? values,
        string? reservedSource)
    {
        if (values is null || string.IsNullOrWhiteSpace(reservedSource) ||
            !values.Keys.Any(key => string.Equals(key, reservedSource, StringComparison.OrdinalIgnoreCase)))
        {
            return values;
        }

        var filtered = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            if (!string.Equals(key, reservedSource, StringComparison.OrdinalIgnoreCase))
            {
                filtered[key] = value;
            }
        }

        return filtered.Count == 0 ? null : filtered;
    }

    internal ManifestSetupResult SetupManifest(
        BuildVariantContext ctx, ConfigOverrides overrides,
        DirectoryHashCache templateHashCache)
    {
        var rootDir = ctx.RootDir;
        var incrementalEnabled = overrides.Incremental ?? true;
        var cacheDir = string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(rootDir, ".cache")
            : Path.GetFullPath(overrides.CacheDir!);

        var suffix = string.IsNullOrWhiteSpace(ctx.ManifestSuffix)
            ? null
            : BuildPathUtils.SanitizeFileSegment(ctx.ManifestSuffix);
        var manifestPath = suffix is null
            ? Path.Combine(cacheDir, "build-manifest.json")
            : Path.Combine(cacheDir, $"build-manifest.{suffix}.json");

        var templateHash = incrementalEnabled
            ? ComputeCompositeTemplateHash(ctx, templateHashCache)
            : string.Empty;

        var manifest = incrementalEnabled
            ? BuildManifest.Load(manifestPath)
            : new BuildManifest();
        manifest.TemplateHash = templateHash;

        var manifestEntries = incrementalEnabled
            ? new ConcurrentDictionary<string, BuildManifestEntry>(manifest.Entries, StringComparer.Ordinal)
            : null;

        return new ManifestSetupResult(manifest, templateHash, manifestPath, manifestEntries, incrementalEnabled);
    }

    internal (IReadOnlyList<RouteInfo> StaticHtmlRoutes, string? StaticRouteTemplate) BuildStaticHtmlData(
        string? staticDir, string? staticTemplate,
        Action<string> warn, bool publishDotFiles)
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
            ? StaticFileService.BuildStaticHtmlRoutes(staticDir!, template, warn, publishDotFiles)
            : Array.Empty<RouteInfo>();
        return (routes, template);
    }

    internal (string? ThemeRoot, string? ParentThemeRoot) GetThemeRootForTokens(
        string? themeRoot, bool hasRegistry, string? parentThemeRoot, bool hasExtends)
    {
        if (!hasRegistry)
        {
            return (null, null);
        }

        var parent = hasExtends ? parentThemeRoot : null;
        return (themeRoot, parent);
    }

    internal async Task<BuildVariantResult> ExecuteAsync(
        BuildVariantContext ctx,
        DirectoryHashCache templateHashCache,
        Func<string, ITemplateRenderer>? rendererFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var variantTotalStopwatch = Stopwatch.StartNew();
        var variantStageMetrics = new BuildStageMetricsCollector();
        var config = ctx.Config;
        var rootDir = ctx.RootDir;
        var overrides = ctx.Overrides;
        var documents = ctx.Documents;
        var bodyStore = ctx.BodyStore;
        var outputDir = ctx.OutputDir;
        var baseUrl = ctx.BaseUrl;

        Directory.CreateDirectory(outputDir);

        var bootstrap = await BootstrapThemeAsync(config, rootDir, logger);
        var templateResolver = new ThemeTemplateResolver(bootstrap.Manifest);
        templateResolver.ValidateRequiredTemplates();
        var dataModules = await BuildDataModulesAsync(
            documents, config.Site.Language, bodyStore, config.Content.Sources,
            config.Content.RouteMetadata, variantStageMetrics);
        var routePipelineResult = await BuildRoutePipelineAsync(
            config, documents, dataModules.DataDocuments, bodyStore, ctx, logger, variantStageMetrics, templateResolver, cancellationToken);

        await RunPluginDeriveStageAsync(routePipelineResult.PluginContext, variantStageMetrics, cancellationToken);
        routePipelineResult = routePipelineResult with
        {
            RouteResult = AddDerivedListRoutesToGraph(
                routePipelineResult.RouteResult,
                routePipelineResult.PluginContext,
                dataModules.RouteMetadata)
        };
        ValidatePostDeriveRoutes(routePipelineResult);

        var allPagesForSections = bootstrap.Registry is not null
            ? routePipelineResult.RouteResult.RoutedDocuments.Select(x => (x.Document, (RouteInfo?)x.Route)).ToList()
            : (IReadOnlyList<(ContentDocument, RouteInfo?)>?)null;

        ITemplateRenderer renderer = rendererFactory is not null
            ? rendererFactory(ctx.LayoutsDir)
            : CreateRenderer(ctx, bootstrap.Registry, bootstrap.SchemaValidator, bootstrap.SectionPlugins, allPagesForSections);

        var siteModel = BuildSiteModel(
            config, baseUrl, dataModules.Modules, dataModules.SourceData,
            routePipelineResult.PluginContext.Data, dataModules.DataIndex, ctx.BuildStartedAt);
        var manifestSetup = SetupManifest(ctx, overrides, templateHashCache);

        var renderDocuments = routePipelineResult.RouteResult.RoutedDocuments
            .Concat(routePipelineResult.PluginContext.DerivedDocuments)
            .ToList();
        var listRoutes = routePipelineResult.RouteResult.ListRoutes;

        var seoStage = await BuildSeoStageAsync(
            config, baseUrl, renderDocuments, listRoutes, routePipelineResult.RouteResult.ListRouteGraph, logger,
            ctx.SeoAlternates, ctx.RootBaseUrl, ctx.DefaultLanguage, overrides,
            routePipelineResult.PluginContext, routePipelineResult.StaticEntries, dataModules.RouteMetadata);
        var analyticsBuildState = AnalyticsBuildState.Create(config, overrides.ExecutionMode);
        AnalyticsBuildState.Attach(routePipelineResult.PluginContext, analyticsBuildState);
        var pluginHtmlTransforms = PluginRunner.CollectHtmlTransforms(
            routePipelineResult.PluginContext,
            overrides.ExecutionMode);
        var htmlTransformPipeline = CreateHtmlTransformPipeline(
            seoStage.SeoResult,
            pluginHtmlTransforms,
            overrides.ExecutionMode);

        try
        {
            var renderPipelineResult = await ExecuteRenderWithHtmlTransformRecordingAsync(
                pluginHtmlTransforms,
                () => RenderPagesStageAsync(
                    renderDocuments, routePipelineResult.RouteResult.RoutedDocuments, routePipelineResult.RouteResult.ListRouteGraph, bodyStore, renderer, siteModel,
                    config, ctx, outputDir, manifestSetup, seoStage, routePipelineResult.StaticEntries,
                    htmlTransformPipeline, variantStageMetrics, logger, templateResolver, dataModules.RouteMetadata, cancellationToken));

            var hasStaticDir = Directory.Exists(ctx.StaticDir);
            var (themeRootForTokens, parentThemeRootForTokens) = GetThemeRootForTokens(
                bootstrap.ThemeRoot, bootstrap.Registry is not null, bootstrap.ParentThemeRoot,
                !string.IsNullOrWhiteSpace(bootstrap.Manifest?.Extends));

            var assetPipelineResult = await SyncAssetsStageAsync(
                ctx, hasStaticDir, themeRootForTokens, parentThemeRootForTokens,
                outputDir, manifestSetup, config, variantStageMetrics, logger, cancellationToken);

            await RunPluginAfterBuildStageAsync(
                routePipelineResult.PluginContext, outputDir, baseUrl, manifestSetup,
                renderPipelineResult, config, variantStageMetrics, logger, cancellationToken);

            variantTotalStopwatch.Stop();
            variantStageMetrics.AddDuration("variantTotal", variantTotalStopwatch.ElapsedMilliseconds);

            var searchSnippetsEnabled = templateResolver.TryResolveKindTemplate("search", out var searchTemplate) &&
                TemplateCapabilitiesResolver.SupportsSearchSnippets(searchTemplate, ctx.LayoutsDir);

            pluginHtmlTransforms.RecordExecutions();
            return await GenerateReportStageAsync(
                config, baseUrl, outputDir, searchSnippetsEnabled, bodyStore,
                ctx.ContentGraph,
                routePipelineResult.PluginContext,
                routePipelineResult.RouteResult.ListRouteGraph,
                seoStage.SeoResult, renderPipelineResult, variantStageMetrics, logger, ctx.DefaultLanguage,
                analyticsBuildState);
        }
        finally
        {
            pluginHtmlTransforms.RecordExecutions();
        }
    }

    internal static HtmlTransformPipeline CreateHtmlTransformPipeline(
        SeoPipelineResult seoResult,
        CollectedHtmlTransforms pluginHtmlTransforms,
        BuildExecutionMode executionMode)
    {
        var transforms = new List<IHtmlTransform>();
        if (seoResult.HtmlTransform is not null)
        {
            transforms.Add(seoResult.HtmlTransform);
        }
        transforms.AddRange(pluginHtmlTransforms);
        return new HtmlTransformPipeline(transforms, executionMode);
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

    private static Task<ThemeBootstrapResult> BootstrapThemeAsync(AppConfig config, string rootDir, ILogger logger)
    {
        return Task.FromResult(ThemeBootstrapper.BootstrapRequired(config, rootDir, logger));
    }

    private Task<DataModuleResult> BuildDataModulesAsync(
        IReadOnlyList<ContentDocument> documents, string language, IContentBodyStore bodyStore,
        IReadOnlyList<ContentSourceConfig>? sources,
        RouteMetadataConfig? routeMetadata,
        BuildStageMetricsCollector metrics)
    {
        var splitItemsStopwatch = Stopwatch.StartNew();
        var dataModules = PrepareDataModules(documents, language, bodyStore, sources, routeMetadata);
        splitItemsStopwatch.Stop();
        metrics.AddDuration("prepareContent", splitItemsStopwatch.ElapsedMilliseconds);
        return Task.FromResult(dataModules);
    }

    private async Task<BuildRoutePipelineResult> BuildRoutePipelineAsync(
        AppConfig config,
        IReadOnlyList<ContentDocument> documents,
        IReadOnlyList<ContentDocument> dataDocuments,
        IContentBodyStore bodyStore,
        BuildVariantContext ctx,
        ILogger logger,
        BuildStageMetricsCollector metrics,
        ThemeTemplateResolver templateResolver,
        CancellationToken cancellationToken)
    {
        var routeGenerationStopwatch = Stopwatch.StartNew();
        var routeResult = GenerateRoutes(config, documents, templateResolver);
        routeGenerationStopwatch.Stop();
        metrics.AddDuration("routeGeneration", routeGenerationStopwatch.ElapsedMilliseconds);

        var pluginContext = new BuildContext
        {
            Config = config,
            RootDir = ctx.RootDir,
            OutputDir = ctx.OutputDir,
            BaseUrl = ctx.BaseUrl,
            LayoutsDir = ctx.LayoutsDir,
            RoutedDocuments = routeResult.RoutedDocuments,
            StaticHtmlRoutes = Array.Empty<RouteInfo>(),
            ContentGraph = ctx.ContentGraph,
            BodyStore = bodyStore,
            TemplateResolver = templateResolver.ResolveKindTemplate,
            Logger = logger
        };
        pluginContext.Data[ListRouteGraphBuilder.BuildContextDataKey] = routeResult.ListRouteGraph;

        var taxonomyStopwatch = Stopwatch.StartNew();
        TaxonomyTermsInjector.InjectFromDataDocuments(pluginContext, dataDocuments);
        await TaxonomyTermsInjector.InjectFromNotionDatabaseOptionsAsync(pluginContext, cancellationToken);
        taxonomyStopwatch.Stop();
        metrics.AddDuration("taxonomySetup", taxonomyStopwatch.ElapsedMilliseconds);

        var hasStaticDir = Directory.Exists(ctx.StaticDir);
        var staticRouteTemplate = !string.IsNullOrWhiteSpace(config.Theme.StaticTemplate) ? config.Theme.StaticTemplate : null;
        IReadOnlyList<RenderEntry>? staticEntries = null;
        IReadOnlyList<RouteInfo> staticHtmlRoutes = Array.Empty<RouteInfo>();

        if (hasStaticDir && staticRouteTemplate is not null)
        {
            staticEntries = RenderEntry.ForStaticDir(ctx.StaticDir!, staticRouteTemplate, msg => logger.Warn(msg), config.Build.PublishDotFiles);
            staticHtmlRoutes = staticEntries.Select(e => e.Route).ToList();
        }
        else if (hasStaticDir &&
                 SafeFileEnumerator.EnumerateFiles(ctx.StaticDir!, "*.html").Any())
        {
            logger.Warn("Static HTML files in static dir are skipped because no static template is configured (theme.staticTemplate).");
        }

        pluginContext.StaticHtmlRoutes = staticHtmlRoutes;

        RouteInventoryValidator.ValidateFinalRoutes(routeResult.RoutedDocuments, pluginContext.DerivedDocuments, routeResult.ListRoutes, staticHtmlRoutes);

        return new BuildRoutePipelineResult(routeResult, staticHtmlRoutes, staticEntries, pluginContext);
    }

    private async Task RunPluginDeriveStageAsync(
        BuildContext pluginContext,
        BuildStageMetricsCollector metrics,
        CancellationToken cancellationToken)
    {
        var derivePagesStopwatch = Stopwatch.StartNew();
        var derived = await PluginRunner.RunDerivePagesAsync(pluginContext, cancellationToken);
        derivePagesStopwatch.Stop();
        metrics.AddDuration("derivePages", derivePagesStopwatch.ElapsedMilliseconds);
        foreach (var page in derived)
        {
            pluginContext.DerivedDocuments.Add(page);
            pluginContext.DerivedRoutes.Add((page.Route, page.LastModified ?? page.Document.PublishAt));
        }
    }

    private static Task<SeoStageResult> BuildSeoStageAsync(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<RoutedContentDocument> renderQueue,
        IReadOnlyList<RouteInfo> listRoutes,
        ListRouteGraph listRouteGraph,
        ILogger logger,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> seoAlternateInputs,
        string? rootBaseUrl,
        string? defaultLanguage,
        ConfigOverrides overrides,
        BuildContext pluginContext,
        IReadOnlyList<RenderEntry>? staticEntries,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
    {
        var seoAlternates = SeoAlternatesService.AddVariantRouteAlternates(
            config, seoAlternateInputs, listRouteGraph, rootBaseUrl, defaultLanguage);
        var maxDegreeOfParallelism = Math.Clamp(
            overrides.Jobs ?? Environment.ProcessorCount,
            1,
            Math.Max(1, Environment.ProcessorCount * 2));

        var searchAction = SearchActionDescriptorResolver.Resolve(
            config,
            baseUrl,
            renderQueue.Select(document => document.Route)
                .Concat(listRoutes)
                .Concat(pluginContext.StaticHtmlRoutes));

        var breadcrumbs = BreadcrumbDescriptorResolver.Resolve(
            config,
            baseUrl,
            renderQueue,
            listRouteGraph,
            staticEntries,
            routeMetadata);

        var seoResult = new SeoPipeline().Execute(
            config, baseUrl, renderQueue, listRoutes, seoAlternates, logger, listRouteGraph, routeMetadata, searchAction, breadcrumbs);
        pluginContext.SeoIndex = seoResult.SeoIndex.Entries;
        pluginContext.Data[BuildContextDataKeys.SeoModels] = seoResult.SeoIndex.Models;

        return Task.FromResult(new SeoStageResult(seoResult, seoAlternates, maxDegreeOfParallelism));
    }

    private async Task<RenderPipelineResult> RenderPagesStageAsync(
        IReadOnlyList<RoutedContentDocument> renderDocuments,
        IReadOnlyList<RoutedContentDocument> routedDocuments,
        ListRouteGraph listRouteGraph,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        AppConfig config,
        BuildVariantContext ctx,
        string outputDir,
        ManifestSetupResult manifestSetup,
        SeoStageResult seoStage,
        IReadOnlyList<RenderEntry>? staticEntries,
        HtmlTransformPipeline htmlTransformPipeline,
        BuildStageMetricsCollector metrics,
        ILogger logger,
        ThemeTemplateResolver templateResolver,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata,
        CancellationToken cancellationToken)
    {
        var renderDependencyHashStopwatch = Stopwatch.StartNew();
        var renderDependencyHash = manifestSetup.IncrementalEnabled
            ? RenderDependencyHasher.Compute(config, siteModel, ctx.Overrides.ExecutionMode)
            : string.Empty;
        renderDependencyHashStopwatch.Stop();
        metrics.AddDuration("renderDependencyHash", renderDependencyHashStopwatch.ElapsedMilliseconds);
        var contentByOutputPath = renderDocuments.ToDictionary(
            document => BuildPathUtils.NormalizeRelPath(document.Route.OutputPath),
            document => document.Document,
            StringComparer.OrdinalIgnoreCase);
        Func<RouteInfo, string>? renderDependencyHashResolver = routeMetadata is null
            ? null
            : route =>
            {
                var graphRoute = listRouteGraph.FindByOutputPath(route.OutputPath);
                string? metadataRouteUrl = graphRoute?.MetadataRouteUrl;
                if (metadataRouteUrl is null)
                {
                    contentByOutputPath.TryGetValue(
                        BuildPathUtils.NormalizeRelPath(route.OutputPath), out var document);
                    metadataRouteUrl = RouteMetadataApplicator.ResolveDependencyRouteUrl(document, route.Url);
                }

                if (metadataRouteUrl is null)
                {
                    return renderDependencyHash;
                }

                return RenderDependencyHasher.ComputeForRoute(renderDependencyHash, metadataRouteUrl, routeMetadata);
            };

        var renderPipelineResult = await new RenderPipeline().ExecuteAsync(new RenderPipelineContext(
            BodyStore: bodyStore,
            Renderer: renderer, SiteModel: siteModel,
            Collections: config.Site.Collections, LayoutsDir: ctx.LayoutsDir,
            ListPageContentMode: config.Build.ListPageContentMode,
            OutputPathEncoding: config.Site.OutputPathEncoding, OutputDir: outputDir,
            TemplateHash: manifestSetup.TemplateHash,
            RenderDependencyHash: renderDependencyHash,
            IncrementalEnabled: manifestSetup.IncrementalEnabled,
            Manifest: manifestSetup.Manifest,
            ManifestEntries: manifestSetup.ManifestEntries,
            MaxDegreeOfParallelism: seoStage.MaxDegreeOfParallelism, Logger: logger,
            ListRouteGraph: listRouteGraph,
            StaticEntries: staticEntries,
            SeoBuilder: seoStage.SeoResult.SeoBuilder,
            ListItemSeoBuilder: seoStage.SeoResult.ListItemSeoBuilder,
            ListSeoBuilder: seoStage.SeoResult.ListSeoBuilder,
            HtmlTransformPipeline: htmlTransformPipeline,
            TemplateResolver: templateResolver,
            RenderDocuments: renderDocuments,
            RoutedDocuments: routedDocuments,
            RenderDependencyHashResolver: renderDependencyHashResolver,
            RouteMetadata: routeMetadata),
            cancellationToken);

        metrics.Merge(renderPipelineResult.StageMetrics);
        return renderPipelineResult;
    }

    private static async Task<AssetPipelineResult> SyncAssetsStageAsync(
        BuildVariantContext ctx,
        bool hasStaticDir,
        string? themeRootForTokens,
        string? parentThemeRootForTokens,
        string outputDir,
        ManifestSetupResult manifestSetup,
        AppConfig config,
        BuildStageMetricsCollector metrics,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var assetPipelineResult = await new AssetPipeline().ExecuteAsync(new AssetPipelineContext(
            StaticDir: hasStaticDir ? ctx.StaticDir : null,
            ParentStaticDir: ctx.ParentStaticDir, AssetsDir: ctx.AssetsDir,
            ParentAssetsDir: ctx.ParentAssetsDir, MediaDownloadDir: ctx.MediaDownloadDir,
            ThemeRoot: themeRootForTokens, ParentThemeRoot: parentThemeRootForTokens,
            OutputDir: outputDir,
            Manifest: manifestSetup.Manifest, IncrementalEnabled: manifestSetup.IncrementalEnabled,
            FingerprintMode: config.Build.FingerprintMode,
            ScssConfig: config.Theme.Scss, ImageConfig: config.Theme.Images,
            Logger: logger,
            PublishDotFiles: config.Build.PublishDotFiles,
            FollowSymlinks: config.Build.FollowSymlinks),
            cancellationToken);

        metrics.Merge(assetPipelineResult.StageMetrics);
        return assetPipelineResult;
    }

    private async Task RunPluginAfterBuildStageAsync(
        BuildContext pluginContext,
        string outputDir,
        string baseUrl,
        ManifestSetupResult manifestSetup,
        RenderPipelineResult renderPipelineResult,
        AppConfig config,
        BuildStageMetricsCollector metrics,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var renderedCount = renderPipelineResult.RenderedCount;
        var skippedCount = renderPipelineResult.SkippedCount;
        var renderReasons = new ConcurrentDictionary<string, int>(
            renderPipelineResult.RenderReasons, StringComparer.OrdinalIgnoreCase);
        var currentKeys = renderPipelineResult.CurrentKeys;

        var pluginPipelineResult = await new PluginPipeline().ExecuteAsync(new PluginPipelineContext(
            PluginContext: pluginContext, OutputDir: outputDir, BaseUrl: baseUrl,
            Manifest: manifestSetup.Manifest, ManifestPath: manifestSetup.ManifestPath,
            IncrementalEnabled: manifestSetup.IncrementalEnabled, CurrentKeys: currentKeys,
            RenderedCount: renderedCount, SkippedCount: skippedCount,
            Logger: logger, Config: config),
            cancellationToken);
        metrics.Merge(pluginPipelineResult.StageMetrics);
    }

    private static Task<BuildVariantResult> GenerateReportStageAsync(
        AppConfig config,
        string baseUrl,
        string outputDir,
        bool searchSnippetsEnabled,
        IContentBodyStore bodyStore,
        CanonicalContentGraph contentGraph,
        BuildContext pluginContext,
        ListRouteGraph listRouteGraph,
        SeoPipelineResult seoResult,
        RenderPipelineResult renderPipelineResult,
        BuildStageMetricsCollector variantStageMetrics,
        ILogger logger,
        string? defaultLanguage,
        AnalyticsBuildState analyticsBuildState)
    {
        var renderedCount = renderPipelineResult.RenderedCount;
        var skippedCount = renderPipelineResult.SkippedCount;
        var renderReasons = new Dictionary<string, int>(
            renderPipelineResult.RenderReasons, StringComparer.OrdinalIgnoreCase);

        var result = new BuildReportPipeline().Execute(new BuildReportPipelineContext(
            Config: config, Language: config.Site.Language, OutputDir: outputDir,
            BaseUrl: baseUrl, SearchSnippetsEnabled: searchSnippetsEnabled,
            BodyStore: bodyStore,
            RoutedDocuments: pluginContext.RoutedDocuments,
            ListRouteGraph: listRouteGraph,
            DerivedDocuments: pluginContext.DerivedDocuments,
            DerivedRoutes: pluginContext.DerivedRoutes,
            SeoIndex: seoResult.SeoIndex.Entries,
            SeoModels: seoResult.SeoIndex.Models,
            PluginExecutions: pluginContext.PluginExecutions.ToList(),
            StaticRoutes: pluginContext.StaticHtmlRoutes,
            PluginOutputs: GetPluginOutputs(pluginContext),
            RenderedCount: renderedCount, SkippedCount: skippedCount,
            RenderReasons: renderReasons,
            StageMetrics: variantStageMetrics.Snapshot(),
            Logger: logger,
            DefaultLanguage: defaultLanguage,
            ContentGraph: contentGraph));
        analyticsBuildState.RecordRenderOutcome(renderedCount, skippedCount);
        AnalyticsReportWriter.WriteIfEnabled(config, outputDir, analyticsBuildState.Snapshot());
        return Task.FromResult(result);
    }

    private static IReadOnlyList<PluginOutputTrackingInfo> GetPluginOutputs(BuildContext pluginContext)
    {
        if (!pluginContext.Data.TryGetValue("__plugin_outputs", out var outputsObj) ||
            outputsObj is not HashSet<PluginOutputTrackingInfo> outputs)
        {
            return Array.Empty<PluginOutputTrackingInfo>();
        }

        return outputs.ToList();
    }

    private static IReadOnlyDictionary<string, object>? MergeSiteData(
        IReadOnlyDictionary<string, object>? sourceData,
        IReadOnlyDictionary<string, object>? pluginData)
    {
        if ((sourceData is null || sourceData.Count == 0) && (pluginData is null || pluginData.Count == 0))
        {
            return null;
        }

        var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (sourceData is not null)
        {
            foreach (var kv in sourceData) merged[kv.Key] = kv.Value;
        }
        if (pluginData is not null)
        {
            foreach (var kv in pluginData) merged[kv.Key] = kv.Value;
        }
        return merged;
    }

    private static string ComputeCompositeTemplateHash(BuildVariantContext ctx, DirectoryHashCache templateHashCache)
    {
        var parts = new List<string>
        {
            "scriban-renderer-v1",
            ComputeTemplateDirectoryPart("child", ctx.LayoutsDir, templateHashCache),
            ComputeTemplateDirectoryPart("parent", ctx.ParentLayoutsDir, templateHashCache),
            ComputeTemplateDirectoryPart("user", ctx.UserLayoutsDir, templateHashCache),
            ComputeThemeYamlPart(ctx.LayoutsDir),
            ComputeThemeYamlPart(ctx.ParentLayoutsDir),
            ComputeThemeYamlPart(ctx.UserLayoutsDir)
        };
        return HashUtil.Sha256Hex(string.Join('\n', parts));
    }

    private static string ComputeTemplateDirectoryPart(string label, string? directory, DirectoryHashCache cache)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return $"{label}:missing";
        }
        return $"{label}:{Path.GetFullPath(directory)}:{cache.GetOrAdd(directory)}";
    }

    private static string ComputeThemeYamlPart(string? layoutsDirectory)
    {
        if (string.IsNullOrWhiteSpace(layoutsDirectory))
        {
            return "theme-yaml:missing";
        }
        var parent = Directory.GetParent(layoutsDirectory)?.FullName ?? string.Empty;
        var themeYamlPath = Path.Combine(parent, "theme.yaml");
        if (!File.Exists(themeYamlPath))
        {
            return $"theme-yaml:{themeYamlPath}:missing";
        }
        return $"theme-yaml:{themeYamlPath}:{HashUtil.Sha256Hex(File.ReadAllBytes(themeYamlPath))}";
    }
}
