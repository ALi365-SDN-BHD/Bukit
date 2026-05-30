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

namespace Bukit.Engine;

internal sealed record DataModuleResult(
    IReadOnlyList<ContentItem> DataItems,
    IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? Modules,
    IReadOnlyDictionary<string, object>? SourceData);

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

internal sealed class VariantBuildPipeline
{
    internal DataModuleResult PrepareDataModules(
        IReadOnlyList<ContentItem> items, string language, IContentBodyStore bodyStore)
    {
        var dataItems = items.Where(MetaHelpers.IsDataItem).ToList();
        var modules = DataModuleBuilder.BuildModules(dataItems, language, bodyStore);
        var sourceData = DataModuleBuilder.BuildDataBySource(dataItems, bodyStore);
        return new DataModuleResult(dataItems, modules, sourceData);
    }

    internal RoutePipelineResult GenerateRoutes(AppConfig config, IReadOnlyList<ContentItem> items)
    {
        return new RoutePipeline().Execute(config, items);
    }

    internal ITemplateRenderer CreateRenderer(
        BuildVariantContext ctx, ThemeComponentRegistry? themeRegistry,
        SectionSchemaValidator? schemaValidator,
        IReadOnlyDictionary<string, ISectionPlugin>? resolvedSectionPlugins,
        IReadOnlyList<(ContentItem, RouteInfo?)>? allPagesForSections)
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
        IReadOnlyDictionary<string, object>? pluginData = null)
    {
        var data = MergeSiteData(sourceData, pluginData);
        return new SiteModel
        {
            Name = config.Site.Name,
            Title = config.Site.Title,
            Url = config.Site.Url,
            Description = config.Site.Description,
            BaseUrl = baseUrl,
            Language = config.Site.Language,
            Analytics = new AnalyticsModel
            {
                Enabled = config.Site.Analytics.Enabled,
                GoogleAnalyticsId = config.Site.Analytics.GoogleAnalyticsId
            },
            Params = config.Theme.Params,
            Modules = modules,
            Data = data
        };
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
        if (template is null)
        {
            if (hasStaticDir)
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
        var items = ctx.Items;
        var bodyStore = ctx.BodyStore;
        var outputDir = ctx.OutputDir;
        var baseUrl = ctx.BaseUrl;

        Directory.CreateDirectory(outputDir);

        var bootstrap = await BootstrapThemeAsync(config, rootDir, logger);
        var dataModules = await BuildDataModulesAsync(items, config.Site.Language, bodyStore, variantStageMetrics);
        var routePipelineResult = await BuildRoutePipelineAsync(
            config, items, dataModules.DataItems, bodyStore, ctx, logger, variantStageMetrics, cancellationToken);

        await RunPluginDeriveStageAsync(routePipelineResult.PluginContext, variantStageMetrics, cancellationToken);

        var allPagesForSections = bootstrap.Registry is not null
            ? routePipelineResult.RouteResult.Routed.Select(x => ((ContentItem)x.Item, (RouteInfo?)x.Route)).ToList()
            : (IReadOnlyList<(ContentItem, RouteInfo?)>?)null;

        ITemplateRenderer renderer = rendererFactory is not null
            ? rendererFactory(ctx.LayoutsDir)
            : CreateRenderer(ctx, bootstrap.Registry, bootstrap.SchemaValidator, bootstrap.SectionPlugins, allPagesForSections);

        var siteModel = BuildSiteModel(config, baseUrl, dataModules.Modules, dataModules.SourceData, routePipelineResult.PluginContext.Data);
        var manifestSetup = SetupManifest(ctx, overrides, templateHashCache);

        var renderQueue = routePipelineResult.RouteResult.Routed.Concat(routePipelineResult.PluginContext.DerivedRouted).ToList();
        var listRoutes = routePipelineResult.RouteResult.ListRoutes;

        var seoStage = await BuildSeoStageAsync(
            config, baseUrl, renderQueue, listRoutes, siteModel.Analytics, logger,
            ctx.SeoAlternates, ctx.RootBaseUrl, ctx.DefaultLanguage, overrides,
            routePipelineResult.PluginContext);

        var renderPipelineResult = await RenderPagesStageAsync(
            renderQueue, routePipelineResult.RouteResult.Routed, bodyStore, renderer, siteModel,
            config, ctx, outputDir, manifestSetup, seoStage, routePipelineResult.StaticEntries,
            variantStageMetrics, logger, cancellationToken);

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

        var searchSnippetsEnabled = TemplateCapabilitiesResolver.SupportsSearchSnippets(
            TemplateCapabilitiesResolver.SearchTemplatePath, ctx.LayoutsDir);

        return await GenerateReportStageAsync(
            config, baseUrl, outputDir, searchSnippetsEnabled, bodyStore,
            routePipelineResult.RouteResult.Routed, routePipelineResult.PluginContext,
            seoStage.SeoResult, renderPipelineResult, variantStageMetrics, logger, ctx.DefaultLanguage);
    }

    private static Task<ThemeBootstrapResult> BootstrapThemeAsync(AppConfig config, string rootDir, ILogger logger)
    {
        return Task.FromResult(ThemeBootstrapper.Bootstrap(config, rootDir, logger));
    }

    private Task<DataModuleResult> BuildDataModulesAsync(
        IReadOnlyList<ContentItem> items, string language, IContentBodyStore bodyStore,
        BuildStageMetricsCollector metrics)
    {
        var splitItemsStopwatch = Stopwatch.StartNew();
        var dataModules = PrepareDataModules(items, language, bodyStore);
        splitItemsStopwatch.Stop();
        metrics.AddDuration("prepareContent", splitItemsStopwatch.ElapsedMilliseconds);
        return Task.FromResult(dataModules);
    }

    private async Task<BuildRoutePipelineResult> BuildRoutePipelineAsync(
        AppConfig config,
        IReadOnlyList<ContentItem> items,
        IReadOnlyList<ContentItem> dataItems,
        IContentBodyStore bodyStore,
        BuildVariantContext ctx,
        ILogger logger,
        BuildStageMetricsCollector metrics,
        CancellationToken cancellationToken)
    {
        var routeGenerationStopwatch = Stopwatch.StartNew();
        var routeResult = GenerateRoutes(config, items);
        var routed = routeResult.Routed;
        routeGenerationStopwatch.Stop();
        metrics.AddDuration("routeGeneration", routeGenerationStopwatch.ElapsedMilliseconds);

        var pluginContext = new BuildContext
        {
            Config = config,
            RootDir = ctx.RootDir,
            OutputDir = ctx.OutputDir,
            BaseUrl = ctx.BaseUrl,
            LayoutsDir = ctx.LayoutsDir,
            Routed = routed,
            BodyStore = bodyStore,
            Logger = logger
        };

        var taxonomyStopwatch = Stopwatch.StartNew();
        TaxonomyTermsInjector.InjectFromDataItems(pluginContext, dataItems);
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
        else if (hasStaticDir)
        {
            logger.Warn("Static HTML files in static dir are skipped because no static template is configured (theme.staticTemplate).");
        }

        RouteInventoryValidator.ValidateFinalRoutes(routed, pluginContext.DerivedRouted, routeResult.ListRoutes, staticHtmlRoutes);

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
        foreach (var (item, route, lastModified) in derived)
        {
            pluginContext.DerivedRouted.Add((item, route));
            pluginContext.DerivedRoutes.Add((route, lastModified));
        }
    }

    private static Task<SeoStageResult> BuildSeoStageAsync(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> renderQueue,
        IReadOnlyList<RouteInfo> listRoutes,
        AnalyticsModel analytics,
        ILogger logger,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> seoAlternateInputs,
        string? rootBaseUrl,
        string? defaultLanguage,
        ConfigOverrides overrides,
        BuildContext pluginContext)
    {
        var seoAlternates = SeoAlternatesService.AddVariantRouteAlternates(
            config, seoAlternateInputs, listRoutes, rootBaseUrl, defaultLanguage);
        var maxDegreeOfParallelism = overrides.Jobs ?? Environment.ProcessorCount;

        var seoResult = new SeoPipeline().Execute(
            config, baseUrl, renderQueue, listRoutes, seoAlternates, analytics, logger);
        pluginContext.SeoIndex = seoResult.SeoIndex.Entries;

        return Task.FromResult(new SeoStageResult(seoResult, seoAlternates, maxDegreeOfParallelism));
    }

    private async Task<RenderPipelineResult> RenderPagesStageAsync(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> renderQueue,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        AppConfig config,
        BuildVariantContext ctx,
        string outputDir,
        ManifestSetupResult manifestSetup,
        SeoStageResult seoStage,
        IReadOnlyList<RenderEntry>? staticEntries,
        BuildStageMetricsCollector metrics,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var renderDependencyHashStopwatch = Stopwatch.StartNew();
        var renderDependencyHash = manifestSetup.IncrementalEnabled
            ? RenderDependencyHasher.Compute(config, siteModel)
            : string.Empty;
        renderDependencyHashStopwatch.Stop();
        metrics.AddDuration("renderDependencyHash", renderDependencyHashStopwatch.ElapsedMilliseconds);

        var renderPipelineResult = await new RenderPipeline().ExecuteAsync(new RenderPipelineContext(
            RenderQueue: renderQueue, Routed: routed, BodyStore: bodyStore,
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
            StaticEntries: staticEntries,
            SeoBuilder: seoStage.SeoResult.SeoBuilder,
            HtmlPostProcessor: seoStage.SeoResult.HtmlPostProcessor,
            ListItemSeoBuilder: seoStage.SeoResult.ListItemSeoBuilder,
            ListSeoBuilder: seoStage.SeoResult.ListSeoBuilder,
            ListHtmlPostProcessor: seoStage.SeoResult.ListHtmlPostProcessor),
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
            AssetHashMode: config.Build.AssetHashMode,
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
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        BuildContext pluginContext,
        SeoPipelineResult seoResult,
        RenderPipelineResult renderPipelineResult,
        BuildStageMetricsCollector variantStageMetrics,
        ILogger logger,
        string? defaultLanguage)
    {
        var renderedCount = renderPipelineResult.RenderedCount;
        var skippedCount = renderPipelineResult.SkippedCount;
        var renderReasons = new Dictionary<string, int>(
            renderPipelineResult.RenderReasons, StringComparer.OrdinalIgnoreCase);

        return Task.FromResult(new BuildReportPipeline().Execute(new BuildReportPipelineContext(
            Config: config, Language: config.Site.Language, OutputDir: outputDir,
            BaseUrl: baseUrl, SearchSnippetsEnabled: searchSnippetsEnabled,
            BodyStore: bodyStore, Routed: routed,
            DerivedRouted: pluginContext.DerivedRouted,
            DerivedRoutes: pluginContext.DerivedRoutes,
            SeoIndex: seoResult.SeoIndex.Entries,
            SeoModels: seoResult.SeoIndex.Models,
            PluginExecutions: pluginContext.PluginExecutions.ToList(),
            RenderedCount: renderedCount, SkippedCount: skippedCount,
            RenderReasons: renderReasons,
            StageMetrics: variantStageMetrics.Snapshot(),
            Logger: logger,
            DefaultLanguage: defaultLanguage)));
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
