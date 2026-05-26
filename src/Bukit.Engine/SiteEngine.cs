using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Incremental;
using Bukit.Engine.Output;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

public sealed class SiteEngine
{
    private readonly ILogger _logger;
    private readonly IContentProviderFactory _contentProviderFactory;
    private readonly ISearchIndexBuilder _searchIndexBuilder;
    private readonly Func<string, ITemplateRenderer>? _rendererFactory;

    public SiteEngine(ILogger logger)
        : this(logger, new DefaultContentProviderFactory(), new DefaultSearchIndexBuilder(), null)
    {
    }

    internal SiteEngine(ILogger logger, IContentProviderFactory contentProviderFactory, ISearchIndexBuilder searchIndexBuilder)
        : this(logger, contentProviderFactory, searchIndexBuilder, null)
    {
    }

    internal SiteEngine(ILogger logger, IContentProviderFactory contentProviderFactory, ISearchIndexBuilder searchIndexBuilder, Func<string, ITemplateRenderer>? rendererFactory)
    {
        _logger = logger;
        _contentProviderFactory = contentProviderFactory;
        _searchIndexBuilder = searchIndexBuilder;
        _rendererFactory = rendererFactory;
    }

    // -- public API --

    public Task<BuildResult> BuildAsync(AppConfig config, string rootDir, ConfigOverrides overrides, CancellationToken cancellationToken = default)
    {
        var pipeline = new BuildPipeline(BuildCoreAsync);
        return pipeline.ExecuteAsync(new BuildPipelineContext(config, rootDir, overrides), cancellationToken);
    }

    public static IReadOnlyList<RouteInfo> GetListRoutes(IReadOnlyDictionary<string, CollectionConfig>? collections)
        => SeoAlternatesService.GetListRoutes(collections);

    public async Task BuildAsync(IContentProvider provider, BuildOptions options, CancellationToken cancellationToken = default)
    {
        var fullOutputDir = Path.GetFullPath(options.OutputDir);
        var rootDir = Path.GetDirectoryName(fullOutputDir) ?? ".";
        var outputDirName = Path.GetFileName(fullOutputDir);

        var config = BuildOptionsMapper.ToAppConfig(options, outputDirName);
        var overrides = new ConfigOverrides { IsCI = options.IsCI, Incremental = false };
        var factory = new FixedContentProviderFactory(provider, _contentProviderFactory);
        var engine = new SiteEngine(_logger, factory, _searchIndexBuilder, _rendererFactory);
        await engine.BuildAsync(config, rootDir, overrides, cancellationToken);
    }

    // -- core build orchestrator --

    private async Task<BuildResult> BuildCoreAsync(BuildPipelineContext context, CancellationToken cancellationToken)
    {
        var plan = BuildPlanner.Plan(context.Config, context.RootDir, context.Overrides, _logger);
        var effectiveConfig = plan.EffectiveConfig;
        var rootDir = context.RootDir;
        var overrides = context.Overrides;

        var contentPipeline = new ContentPipeline(_contentProviderFactory, _logger);
        var contentResult = await contentPipeline.ExecuteAsync(effectiveConfig, rootDir, overrides, plan.MediaCacheDir, cancellationToken);
        var items = contentResult.Items;
        var bodyStore = contentResult.BodyStore;

        var templateHashCache = new DirectoryHashCache();

        var languages = I18nOutputMerger.GetLanguages(effectiveConfig.Site);
        if (languages.Count == 0)
        {
            var siteLanguage = effectiveConfig.Site.Language;
            var result = await BuildSingleLanguageVariantAsync(
                effectiveConfig, rootDir, overrides, items, bodyStore, plan.OutputDir,
                plan.LayoutsDir, plan.AssetsDir, plan.StaticDir, plan.MediaCacheDir,
                plan.ParentLayoutsDir, plan.ParentAssetsDir, plan.ParentStaticDir, plan.UserLayoutsDir,
                templateHashCache, cancellationToken);

            _logger.Info($"event=build.variant.done language={effectiveConfig.Site.Language} baseUrl={BuildPathUtils.NormalizeBaseUrl(effectiveConfig.Site.BaseUrl)}");
            MetricsWriter.WriteIfRequested(rootDir, overrides.MetricsPath, effectiveConfig, plan.OutputDir, items.Count, new[] { result });
            plan.Stopwatch.Stop();
            var singleLanguageBuildResult = BuildResultFactory.Create(effectiveConfig, rootDir, plan.OutputDir, overrides, plan.StartedAt, DateTimeOffset.UtcNow, plan.Stopwatch.ElapsedMilliseconds, new[] { result }, contentResult.SchemaErrors);
            BuildReporter.WriteIfEnabled(effectiveConfig, rootDir, plan.OutputDir, singleLanguageBuildResult, new[] { result }, _logger);
            WriteOutputMarker(plan.OutputDir);
            BuildRecoveryTracker.MarkCompleted(plan.OutputDir);
            return singleLanguageBuildResult;
        }

        return await BuildMultiLanguageAsync(
            effectiveConfig, rootDir, overrides, items, bodyStore, plan.OutputDir,
            plan.LayoutsDir, plan.AssetsDir, plan.StaticDir, plan.MediaCacheDir,
            plan.ParentLayoutsDir, plan.ParentAssetsDir, plan.ParentStaticDir, plan.UserLayoutsDir,
            templateHashCache, languages, plan.StartedAt, plan.Stopwatch,
            contentResult.SchemaErrors, cancellationToken);
    }

    private async Task<BuildVariantResult> BuildSingleLanguageVariantAsync(
        AppConfig config, string rootDir, ConfigOverrides overrides,
        IReadOnlyList<ContentItem> items, IContentBodyStore bodyStore,
        string outputDir, string layoutsDir, string assetsDir, string staticDir,
        string mediaCacheDir,
        string? parentLayoutsDir, string? parentAssetsDir, string? parentStaticDir,
        string? userLayoutsDir,
        DirectoryHashCache templateHashCache,
        CancellationToken cancellationToken)
    {
        var baseUrl = BuildPathUtils.NormalizeBaseUrl(config.Site.BaseUrl);
        _logger.Info($"event=build.variant.start language={config.Site.Language} baseUrl={baseUrl}");
        var variantCtx = new BuildVariantContext(
            config, rootDir, overrides, items, bodyStore, outputDir, baseUrl,
            layoutsDir, assetsDir, staticDir, mediaCacheDir,
            SeoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal),
            RootBaseUrl: null, ManifestSuffix: null, DefaultLanguage: null,
            ParentLayoutsDir: parentLayoutsDir, ParentAssetsDir: parentAssetsDir, ParentStaticDir: parentStaticDir,
            UserLayoutsDir: userLayoutsDir);
        return await BuildVariantAsync(variantCtx, templateHashCache, cancellationToken);
    }

    private async Task<BuildResult> BuildMultiLanguageAsync(
        AppConfig config, string rootDir, ConfigOverrides overrides,
        IReadOnlyList<ContentItem> items, IContentBodyStore bodyStore,
        string outputDir, string layoutsDir, string assetsDir, string staticDir,
        string mediaCacheDir,
        string? parentLayoutsDir, string? parentAssetsDir, string? parentStaticDir,
        string? userLayoutsDir,
        DirectoryHashCache templateHashCache,
        IReadOnlyList<string> languages,
        DateTimeOffset buildStartedAt, Stopwatch buildStopwatch,
        IReadOnlyList<ContentSchemaValidator.SchemaValidationError> schemaErrors,
        CancellationToken cancellationToken)
    {
        var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(config.Site, languages);
        var rootBaseUrl = BuildPathUtils.NormalizeBaseUrl(config.Site.BaseUrl);
        var seoAlternates = SeoAlternatesService.BuildSeoAlternates(config, items, languages, defaultLanguage, rootBaseUrl);
        var results = new BuildVariantResult[languages.Count];
        await Parallel.ForEachAsync(
            languages.Select((lang, i) => (lang, i)),
            new ParallelOptions { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken },
            async (entry, ct) =>
            {
                var (lang, i) = entry;
                var variantLogger = new ConsoleLogger(ResolveVariantLogLevel(config, overrides.IsCI));
                var baseUrl = I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, lang);
                var variantConfig = config with
                {
                    Site = config.Site with
                    {
                        Language = lang,
                        BaseUrl = baseUrl
                    }
                };

                var variantItems = I18nOutputMerger.FilterItemsByLanguage(items, lang, defaultLanguage);
                var variantOutputDir = Path.Combine(outputDir, lang);
                variantLogger.Info($"event=build.variant.start language={lang} baseUrl={baseUrl} outputDir={variantOutputDir}");
                var variantCtx = new BuildVariantContext(
                    variantConfig, rootDir, overrides, variantItems, bodyStore, variantOutputDir, baseUrl,
                    layoutsDir, assetsDir, staticDir, mediaCacheDir,
                    SeoAlternates: seoAlternates,
                    RootBaseUrl: rootBaseUrl, ManifestSuffix: lang, DefaultLanguage: defaultLanguage,
                    ParentLayoutsDir: parentLayoutsDir, ParentAssetsDir: parentAssetsDir, ParentStaticDir: parentStaticDir,
                    UserLayoutsDir: userLayoutsDir);
                results[i] = await BuildVariantAsync(variantCtx, templateHashCache, ct, variantLogger);
                variantLogger.Info($"event=build.variant.done language={lang} baseUrl={baseUrl} outputDir={variantOutputDir}");
            });

        var variantResults = results.Where(r => r is not null).ToList();

        I18nOutputMerger.GenerateRootOutputs(config, outputDir, rootBaseUrl, variantResults, _logger, _searchIndexBuilder);
        SeoAuditReportWriter.WriteMerged(config, outputDir, variantResults, _logger);
        _logger.Info("event=build.done");
        MetricsWriter.WriteIfRequested(rootDir, overrides.MetricsPath, config, outputDir, items.Count, variantResults);
        buildStopwatch.Stop();
        var buildResult = BuildResultFactory.Create(config, rootDir, outputDir, overrides, buildStartedAt, DateTimeOffset.UtcNow, buildStopwatch.ElapsedMilliseconds, variantResults, schemaErrors);
        BuildReporter.WriteIfEnabled(config, rootDir, outputDir, buildResult, variantResults, _logger);
        WriteOutputMarker(outputDir);
        BuildRecoveryTracker.MarkCompleted(outputDir);
        return buildResult;
    }

    private async Task<BuildVariantResult> BuildVariantAsync(
        BuildVariantContext ctx,
        DirectoryHashCache templateHashCache,
        CancellationToken cancellationToken,
        ILogger? variantLogger = null)
    {
        var log = variantLogger ?? _logger;
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

        var bootstrap = ThemeBootstrapper.Bootstrap(config, rootDir, log);
        var themeName = bootstrap.ThemeName;
        var themeRoot = bootstrap.ThemeRoot;
        var parentThemeRoot = bootstrap.ParentThemeRoot;
        var themeManifest = bootstrap.Manifest;
        var themeRegistry = bootstrap.Registry;
        var schemaValidator = bootstrap.SchemaValidator;
        var resolvedSectionPlugins = bootstrap.SectionPlugins;

        var hasStaticDir = Directory.Exists(ctx.StaticDir);
        var staticTemplate = config.Theme.StaticTemplate;

        var splitItemsStopwatch = Stopwatch.StartNew();
        var dataItems = items.Where(MetaHelpers.IsDataItem).ToList();
        var modules = DataModuleBuilder.BuildModules(dataItems, config.Site.Language, bodyStore);
        var sourceData = DataModuleBuilder.BuildDataBySource(dataItems, bodyStore);
        splitItemsStopwatch.Stop();
        variantStageMetrics.AddDuration("prepareContent", splitItemsStopwatch.ElapsedMilliseconds);

        var routeGenerationStopwatch = Stopwatch.StartNew();
        var routeResult = new RoutePipeline().Execute(config, items);
        var routed = routeResult.Routed;
        routeGenerationStopwatch.Stop();
        variantStageMetrics.AddDuration("routeGeneration", routeGenerationStopwatch.ElapsedMilliseconds);

        IReadOnlyList<(ContentItem Item, RouteInfo? Route)>? allPagesForSections = themeRegistry is not null
            ? routed.Select(x => ((ContentItem)x.Item, (RouteInfo?)x.Route)).ToList()
            : null;

        ITemplateRenderer renderer = _rendererFactory is not null
            ? _rendererFactory(ctx.LayoutsDir)
            : themeRegistry is not null
                ? new ScribanTemplateRendererAdapter(ctx.LayoutsDir, ctx.ParentLayoutsDir, config.Theme.Shortcodes, config.Theme.Components, ctx.UserLayoutsDir, themeRegistry, schemaValidator, null, config.Theme.ComponentValidation, allPagesForSections, resolvedSectionPlugins)
                : new ScribanTemplateRendererAdapter(ctx.LayoutsDir, ctx.ParentLayoutsDir, config.Theme.Shortcodes, config.Theme.Components, ctx.UserLayoutsDir);

        var pluginContext = new BuildContext
        {
            Config = config,
            RootDir = rootDir,
            OutputDir = outputDir,
            BaseUrl = baseUrl,
            LayoutsDir = ctx.LayoutsDir,
            Routed = routed,
            BodyStore = bodyStore,
            Logger = log
        };

        var taxonomyStopwatch = Stopwatch.StartNew();
        TaxonomyTermsInjector.InjectFromDataItems(pluginContext, dataItems);
        await TaxonomyTermsInjector.InjectFromNotionDatabaseOptionsAsync(pluginContext, cancellationToken);
        taxonomyStopwatch.Stop();
        variantStageMetrics.AddDuration("taxonomySetup", taxonomyStopwatch.ElapsedMilliseconds);

        var derivePagesStopwatch = Stopwatch.StartNew();
        var derived = await PluginRunner.RunDerivePagesAsync(pluginContext, cancellationToken);
        derivePagesStopwatch.Stop();
        variantStageMetrics.AddDuration("derivePages", derivePagesStopwatch.ElapsedMilliseconds);
        foreach (var (item, route, lastModified) in derived)
        {
            pluginContext.DerivedRouted.Add((item, route));
            pluginContext.DerivedRoutes.Add((route, lastModified));
        }

        var data = MergeSiteData(sourceData, pluginContext.Data);
        var siteModel = new SiteModel
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

        var incrementalEnabled = overrides.Incremental ?? true;
        var cacheDir = string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(rootDir, ".cache")
            : Path.GetFullPath(overrides.CacheDir!);

        var suffix = string.IsNullOrWhiteSpace(ctx.ManifestSuffix) ? null : BuildPathUtils.SanitizeFileSegment(ctx.ManifestSuffix);
        var manifestPath = suffix is null
            ? Path.Combine(cacheDir, "build-manifest.json")
            : Path.Combine(cacheDir, $"build-manifest.{suffix}.json");

        var templateHashStopwatch = Stopwatch.StartNew();
        var templateHash = incrementalEnabled ? ComputeCompositeTemplateHash(ctx, templateHashCache) : string.Empty;
        templateHashStopwatch.Stop();
        variantStageMetrics.AddDuration("templateHash", templateHashStopwatch.ElapsedMilliseconds);
        var manifest = incrementalEnabled ? BuildManifest.Load(manifestPath) : new BuildManifest();
        manifest.TemplateHash = templateHash;
        var manifestEntries = incrementalEnabled
            ? new ConcurrentDictionary<string, BuildManifestEntry>(manifest.Entries, StringComparer.Ordinal)
            : null;

        var renderQueue = routed.Concat(pluginContext.DerivedRouted).ToList();
        var listRoutes = routeResult.ListRoutes;
        var staticRouteTemplate = !string.IsNullOrWhiteSpace(staticTemplate) ? staticTemplate : "__raw_static__";
        var staticHtmlRoutes = hasStaticDir
            ? StaticFileService.BuildStaticHtmlRoutes(ctx.StaticDir, staticRouteTemplate, log.Warn, config.Build.PublishDotFiles)
            : Array.Empty<RouteInfo>();
        RouteInventoryValidator.ValidateFinalRoutes(routed, pluginContext.DerivedRouted, listRoutes, staticHtmlRoutes);
        var seoAlternates = SeoAlternatesService.AddVariantRouteAlternates(
            config,
            ctx.SeoAlternates,
            listRoutes,
            ctx.RootBaseUrl,
            ctx.DefaultLanguage);
        var maxDegreeOfParallelism = overrides.Jobs ?? Environment.ProcessorCount;

        var seoResult = new SeoPipeline().Execute(
            config,
            baseUrl,
            renderQueue,
            listRoutes,
            seoAlternates,
            siteModel.Analytics,
            log);
        pluginContext.SeoIndex = seoResult.SeoIndex.Entries;

        var renderDependencyHashStopwatch = Stopwatch.StartNew();
        var renderDependencyHash = incrementalEnabled ? RenderDependencyHasher.Compute(config, siteModel) : string.Empty;
        renderDependencyHashStopwatch.Stop();
        variantStageMetrics.AddDuration("renderDependencyHash", renderDependencyHashStopwatch.ElapsedMilliseconds);

        var renderPipelineResult = await new RenderPipeline().ExecuteAsync(new RenderPipelineContext(
            RenderQueue: renderQueue,
            Routed: routed,
            BodyStore: bodyStore,
            Renderer: renderer,
            SiteModel: siteModel,
            Collections: config.Site.Collections,
            LayoutsDir: ctx.LayoutsDir,
            ListPageContentMode: config.Build.ListPageContentMode,
            OutputPathEncoding: config.Site.OutputPathEncoding,
            OutputDir: outputDir,
            TemplateHash: templateHash,
            RenderDependencyHash: renderDependencyHash,
            IncrementalEnabled: incrementalEnabled,
            Manifest: manifest,
            ManifestEntries: manifestEntries,
            MaxDegreeOfParallelism: maxDegreeOfParallelism,
            Logger: log,
            SeoBuilder: seoResult.SeoBuilder,
            HtmlPostProcessor: seoResult.HtmlPostProcessor,
            ListItemSeoBuilder: seoResult.ListItemSeoBuilder,
            ListSeoBuilder: seoResult.ListSeoBuilder,
            ListHtmlPostProcessor: seoResult.ListHtmlPostProcessor),
            cancellationToken);

        variantStageMetrics.Merge(renderPipelineResult.StageMetrics);

        var renderedCount = renderPipelineResult.RenderedCount;
        var skippedCount = renderPipelineResult.SkippedCount;
        var renderReasons = new ConcurrentDictionary<string, int>(renderPipelineResult.RenderReasons, StringComparer.OrdinalIgnoreCase);
        var currentKeys = renderPipelineResult.CurrentKeys;

        var themeRootForTokens = themeRegistry is not null ? themeRoot : null;
        var parentThemeRootForTokens = themeRootForTokens is not null && !string.IsNullOrWhiteSpace(themeManifest?.Extends)
            ? parentThemeRoot
            : null;

        var assetPipelineResult = await new AssetPipeline().ExecuteAsync(new AssetPipelineContext(
            StaticDir: hasStaticDir ? ctx.StaticDir : null,
            ParentStaticDir: ctx.ParentStaticDir,
            AssetsDir: ctx.AssetsDir,
            ParentAssetsDir: ctx.ParentAssetsDir,
            MediaDownloadDir: ctx.MediaDownloadDir,
            ThemeRoot: themeRootForTokens,
            ParentThemeRoot: parentThemeRootForTokens,
            OutputDir: outputDir,
            BaseUrl: baseUrl,
            Renderer: renderer,
            SiteModel: siteModel,
            StaticTemplate: staticTemplate,
            Manifest: manifest,
            IncrementalEnabled: incrementalEnabled,
            AssetHashMode: config.Build.AssetHashMode,
            ScssConfig: config.Theme.Scss,
            ImageConfig: config.Theme.Images,
            Logger: log,
            CurrentKeys: currentKeys,
            PublishDotFiles: config.Build.PublishDotFiles),
            cancellationToken);

        variantStageMetrics.Merge(assetPipelineResult.StageMetrics);

        var pluginPipelineResult = await new PluginPipeline().ExecuteAsync(new PluginPipelineContext(
            PluginContext: pluginContext,
            OutputDir: outputDir,
            BaseUrl: baseUrl,
            Manifest: manifest,
            ManifestPath: manifestPath,
            IncrementalEnabled: incrementalEnabled,
            CurrentKeys: currentKeys,
            RenderedCount: renderedCount,
            SkippedCount: skippedCount,
            Logger: log,
            Config: config),
            cancellationToken);
        variantStageMetrics.Merge(pluginPipelineResult.StageMetrics);

        variantTotalStopwatch.Stop();
        variantStageMetrics.AddDuration("variantTotal", variantTotalStopwatch.ElapsedMilliseconds);

        var searchSnippetsEnabled = TemplateCapabilitiesResolver.SupportsSearchSnippets(TemplateCapabilitiesResolver.SearchTemplatePath, ctx.LayoutsDir);
        return new BuildReportPipeline().Execute(new BuildReportPipelineContext(
            Config: config,
            Language: config.Site.Language,
            OutputDir: outputDir,
            BaseUrl: baseUrl,
            SearchSnippetsEnabled: searchSnippetsEnabled,
            BodyStore: bodyStore,
            Routed: routed,
            DerivedRouted: pluginContext.DerivedRouted,
            DerivedRoutes: pluginContext.DerivedRoutes,
            SeoIndex: seoResult.SeoIndex.Entries,
            SeoModels: seoResult.SeoIndex.Models,
            PluginExecutions: pluginContext.PluginExecutions.ToList(),
            RenderedCount: renderedCount,
            SkippedCount: skippedCount,
            RenderReasons: new Dictionary<string, int>(renderReasons, StringComparer.OrdinalIgnoreCase),
            StageMetrics: variantStageMetrics.Snapshot(),
            Logger: log,
            DefaultLanguage: ctx.DefaultLanguage));
    }

    private static IReadOnlyDictionary<string, object>? MergeSiteData(
        IReadOnlyDictionary<string, object>? sourceData,
        IReadOnlyDictionary<string, object> pluginData)
    {
        if ((sourceData is null || sourceData.Count == 0) && pluginData.Count == 0)
        {
            return null;
        }

        var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (sourceData is not null)
        {
            foreach (var kv in sourceData)
            {
                merged[kv.Key] = kv.Value;
            }
        }

        foreach (var kv in pluginData)
        {
            merged[kv.Key] = kv.Value;
        }

        return merged;
    }

    private static IReadOnlyDictionary<string, RouteGenerator.CollectionRouteRule>? BuildCollectionRules(SiteConfig site)
    {
        return RouteInventoryValidator.BuildCollectionRules(site);
    }

    // -- private helpers --

    private const string OutputMarkerFileName = ".bukit-output-marker";
    private const string TemplateRendererFingerprintVersion = "scriban-renderer-v1";

    private static string ComputeCompositeTemplateHash(BuildVariantContext ctx, DirectoryHashCache templateHashCache)
    {
        var parts = new List<string>
        {
            TemplateRendererFingerprintVersion,
            ComputeTemplateDirectoryPart("child", ctx.LayoutsDir, templateHashCache),
            ComputeTemplateDirectoryPart("parent", ctx.ParentLayoutsDir, templateHashCache),
            ComputeTemplateDirectoryPart("user", ctx.UserLayoutsDir, templateHashCache),
            ComputeThemeYamlPart(ctx.LayoutsDir),
            ComputeThemeYamlPart(ctx.ParentLayoutsDir),
            ComputeThemeYamlPart(ctx.UserLayoutsDir)
        };

        return HashUtil.Sha256Hex(string.Join('\n', parts));
    }

    private static string ComputeTemplateDirectoryPart(string label, string? directory, DirectoryHashCache templateHashCache)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return $"{label}:missing";
        }

        return $"{label}:{Path.GetFullPath(directory)}:{templateHashCache.GetOrAdd(directory)}";
    }

    private static string ComputeThemeYamlPart(string? layoutsDirectory)
    {
        if (string.IsNullOrWhiteSpace(layoutsDirectory))
        {
            return "theme-yaml:missing";
        }

        var themeYamlPath = Path.Combine(Directory.GetParent(layoutsDirectory)?.FullName ?? string.Empty, "theme.yaml");
        if (!File.Exists(themeYamlPath))
        {
            return $"theme-yaml:{themeYamlPath}:missing";
        }

        return $"theme-yaml:{themeYamlPath}:{HashUtil.Sha256Hex(File.ReadAllBytes(themeYamlPath))}";
    }

    private static void WriteOutputMarker(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, OutputMarkerFileName), "bukit-output\n");
    }

    private static LogLevel ResolveVariantLogLevel(AppConfig config, bool isCi)
    {
        if (isCi)
        {
            return LogLevel.Warn;
        }

        return (config.Logging.Level ?? "info").Trim().ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "info" => LogLevel.Info,
            "warn" => LogLevel.Warn,
            "error" => LogLevel.Error,
            _ => LogLevel.Info
        };
    }
}
