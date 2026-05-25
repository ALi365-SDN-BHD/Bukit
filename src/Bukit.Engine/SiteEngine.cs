using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Engine.Plugins;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Incremental;
using Bukit.Engine.Output;
using Bukit.Config;
using Bukit.Content;
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

    public Task<BuildResult> BuildAsync(AppConfig config, string rootDir, ConfigOverrides overrides, CancellationToken cancellationToken = default)
    {
        var pipeline = new BuildPipeline(BuildCoreAsync);
        return pipeline.ExecuteAsync(new BuildPipelineContext(config, rootDir, overrides), cancellationToken);
    }

    private async Task<BuildResult> BuildCoreAsync(BuildPipelineContext context, CancellationToken cancellationToken)
    {
        var config = context.Config;
        var rootDir = context.RootDir;
        var overrides = context.Overrides;
        var buildStartedAt = DateTimeOffset.UtcNow;
        var buildStopwatch = Stopwatch.StartNew();
        var effectiveConfig = ConfigApplier.Apply(config, overrides);
        ConfigValidator.Validate(effectiveConfig);

        var outputDir = BuildPathUtils.MakeAbsolute(rootDir, effectiveConfig.Build.Output);
        var (layoutsDir, assetsDir, staticDir, parentLayoutsDir, parentAssetsDir, parentStaticDir, userLayoutsDir) = BuildPathUtils.ResolveThemeDirectories(rootDir, effectiveConfig.Theme);

        if (effectiveConfig.Build.Clean && Directory.Exists(outputDir))
        {
            EnsureOutputDirectoryCanBeCleaned(rootDir, outputDir);
            Directory.Delete(outputDir, recursive: true);
        }

        if (!effectiveConfig.Build.Clean && BuildRecoveryTracker.HasIncompleteBuild(outputDir))
        {
            _logger.Warn($"event=build.recovery previousIncomplete=true outputDir={outputDir} action=autoClean");
            Directory.Delete(outputDir, recursive: true);
        }

        Directory.CreateDirectory(outputDir);

        BuildRecoveryTracker.MarkStarted(outputDir);
        _logger.Info($"event=build.start rootDir={rootDir} outputDir={outputDir}");

        var mediaCacheDir = string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(rootDir, ".cache", "media")
            : Path.Combine(Path.GetFullPath(overrides.CacheDir!), "media");

        var contentPipeline = new ContentPipeline(_contentProviderFactory, _logger);
        var contentResult = await contentPipeline.ExecuteAsync(effectiveConfig, rootDir, overrides, mediaCacheDir, cancellationToken);
        var items = contentResult.Items;
        var bodyStore = contentResult.BodyStore;

        var templateHashCache = new DirectoryHashCache();

        var languages = I18nOutputMerger.GetLanguages(effectiveConfig.Site);
        if (languages.Count == 0)
        {
            var siteLanguage = effectiveConfig.Site.Language;
            var beforeLang = items.Count;
            items = I18nOutputMerger.FilterItemsByLanguage(items, siteLanguage, siteLanguage);
            if (items.Count < beforeLang)
            {
                _logger.Info($"event=content.language_filtered removed={beforeLang - items.Count} language={siteLanguage}");
            }

            var baseUrl = BuildPathUtils.NormalizeBaseUrl(effectiveConfig.Site.BaseUrl);
            _logger.Info($"event=build.variant.start language={effectiveConfig.Site.Language} baseUrl={baseUrl}");
            var variantCtx = new BuildVariantContext(
                effectiveConfig, rootDir, overrides, items, bodyStore, outputDir, baseUrl,
                layoutsDir, assetsDir, staticDir, mediaCacheDir,
                SeoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal),
                RootBaseUrl: null, ManifestSuffix: null, DefaultLanguage: null,
                ParentLayoutsDir: parentLayoutsDir, ParentAssetsDir: parentAssetsDir, ParentStaticDir: parentStaticDir,
                UserLayoutsDir: userLayoutsDir);
            var result = await BuildVariantAsync(variantCtx, templateHashCache, cancellationToken);

            _logger.Info($"event=build.variant.done language={effectiveConfig.Site.Language} baseUrl={baseUrl}");
            MetricsWriter.WriteIfRequested(rootDir, overrides.MetricsPath, effectiveConfig, outputDir, items.Count, new[] { result });
            buildStopwatch.Stop();
            var singleLanguageBuildResult = BuildResultFactory.Create(effectiveConfig, rootDir, outputDir, overrides, buildStartedAt, DateTimeOffset.UtcNow, buildStopwatch.ElapsedMilliseconds, new[] { result });
            BuildReporter.WriteIfEnabled(effectiveConfig, rootDir, outputDir, singleLanguageBuildResult, new[] { result }, _logger);
            WriteOutputMarker(outputDir);
            BuildRecoveryTracker.MarkCompleted(outputDir);
            return singleLanguageBuildResult;
        }

        var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(effectiveConfig.Site, languages);
        var rootBaseUrl = BuildPathUtils.NormalizeBaseUrl(effectiveConfig.Site.BaseUrl);
        var seoAlternates = SeoAlternatesService.BuildSeoAlternates(effectiveConfig, items, languages, defaultLanguage, rootBaseUrl);
        var results = new BuildVariantResult[languages.Count];
        await Parallel.ForEachAsync(
            languages.Select((lang, i) => (lang, i)),
            new ParallelOptions { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken },
            async (entry, ct) =>
            {
                var (lang, i) = entry;
                var variantLogger = new ConsoleLogger(ResolveVariantLogLevel(effectiveConfig, overrides.IsCI));
                var baseUrl = I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, lang);
                var variantConfig = effectiveConfig with
                {
                    Site = effectiveConfig.Site with
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

        I18nOutputMerger.GenerateRootOutputs(effectiveConfig, outputDir, rootBaseUrl, variantResults, _logger, _searchIndexBuilder);
        SeoAuditReportWriter.WriteMerged(effectiveConfig, outputDir, variantResults, _logger);
        _logger.Info("event=build.done");
        MetricsWriter.WriteIfRequested(rootDir, overrides.MetricsPath, effectiveConfig, outputDir, items.Count, variantResults);
        buildStopwatch.Stop();
        var buildResult = BuildResultFactory.Create(effectiveConfig, rootDir, outputDir, overrides, buildStartedAt, DateTimeOffset.UtcNow, buildStopwatch.ElapsedMilliseconds, variantResults);
        BuildReporter.WriteIfEnabled(effectiveConfig, rootDir, outputDir, buildResult, variantResults, _logger);
        WriteOutputMarker(outputDir);
        BuildRecoveryTracker.MarkCompleted(outputDir);
        return buildResult;
    }

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

    private static void EnsureOutputDirectoryCanBeCleaned(string rootDir, string outputDir)
    {
        var fullRoot = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullOutput = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullOutput, fullRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullOutput, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullOutput, Path.GetPathRoot(fullOutput)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(fullOutput), ".git", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigException($"Refusing to clean unsafe output directory: {outputDir}");
        }

        if (!Directory.EnumerateFileSystemEntries(fullOutput).Any())
        {
            return;
        }

        if (!File.Exists(Path.Combine(fullOutput, OutputMarkerFileName)))
        {
            throw new ConfigException($"Refusing to clean output directory without Bukit marker: {outputDir}");
        }
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

        var themeName = config.Theme.Name;
        ThemeManifestV2? themeManifest = null;
        ThemeComponentRegistry? themeRegistry = null;
        SectionSchemaValidator? schemaValidator = null;
        IReadOnlyDictionary<string, ISectionPlugin>? resolvedSectionPlugins = null;
        if (!string.IsNullOrWhiteSpace(themeName) || !string.IsNullOrWhiteSpace(config.Theme.Source))
        {
            var themeRoot = Path.Combine(rootDir, "themes", themeName ?? "remote");
            if (!string.IsNullOrWhiteSpace(config.Theme.Source))
            {
                var themesCacheDir = Path.Combine(rootDir, ".cache", "themes");
                Directory.CreateDirectory(themesCacheDir);
                var resolved = ThemeSourceManager.Resolve(config.Theme.Source, themesCacheDir,
                    msg => _logger.Warn(msg));
                if (resolved is not null)
                {
                    themeRoot = resolved.ThemeRoot;
                    if (!string.IsNullOrWhiteSpace(themeName))
                    {
                        themeRoot = Path.Combine(resolved.ThemeRoot, themeName);
                    }
                }
            }

            themeManifest = ThemeManifestLoader.Load(themeRoot);
            if (themeManifest is not null)
            {
                ThemeComponentRegistry? parentRegistry = null;
                if (!string.IsNullOrWhiteSpace(themeManifest.Extends))
                {
                    var parentThemeRoot = Path.Combine(rootDir, "themes", themeManifest.Extends);
                    var parentManifest = ThemeManifestLoader.Load(parentThemeRoot);
                    if (parentManifest is not null)
                    {
                        parentRegistry = new ThemeComponentRegistry(parentThemeRoot, parentManifest, null);
                    }
                }

                themeRegistry = new ThemeComponentRegistry(themeRoot, themeManifest, parentRegistry);

                var sectionPlugins = new Dictionary<string, ISectionPlugin>(StringComparer.OrdinalIgnoreCase);
                if (themeManifest.Sections is not null)
                {
                    foreach (var (sectionName, sDef) in themeManifest.Sections)
                    {
                        if (!string.IsNullOrWhiteSpace(sDef.Plugin) &&
                            SectionPluginRegistry.TryResolve(sDef.Plugin, out var plugin))
                        {
                            sectionPlugins[sDef.Plugin] = plugin!;
                            _logger.Info($"Section '{sectionName}' loaded plugin: {sDef.Plugin} ({plugin!.SupportedHook})");
                        }
                    }
                }
                resolvedSectionPlugins = sectionPlugins.Count > 0 ? sectionPlugins : null;

                var validationMode = config.Theme.ComponentValidation switch
                {
                    "strict" => ValidationMode.Strict,
                    "warn" => ValidationMode.Warn,
                    _ => ValidationMode.Off
                };
                schemaValidator = new SectionSchemaValidator(validationMode, themeRoot, log);
            }
        }

        var hasStaticDir = Directory.Exists(ctx.StaticDir);
        var staticTemplate = config.Theme.StaticTemplate;

        var splitItemsStopwatch = Stopwatch.StartNew();
        var dataItems = items.Where(MetaHelpers.IsDataItem).ToList();
        var contentItems = items.Where(i => !MetaHelpers.IsDataItem(i)).ToList();
        var modules = DataModuleBuilder.BuildModules(dataItems, config.Site.Language, bodyStore);
        var sourceData = DataModuleBuilder.BuildDataBySource(dataItems, bodyStore);
        splitItemsStopwatch.Stop();
        variantStageMetrics.AddDuration("prepareContent", splitItemsStopwatch.ElapsedMilliseconds);

        var collectionRules = BuildCollectionRules(config.Site);

        var routeGenerationStopwatch = Stopwatch.StartNew();
        var routed = contentItems
            .Select(i => (Item: i, Route: RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .ToList();
        RouteInventoryValidator.ValidateContentRoutes(routed);
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
        var listRoutes = SeoAlternatesService.BuildListRoutes(config.Site.Collections);
        var staticHtmlRoutes = hasStaticDir && !string.IsNullOrWhiteSpace(staticTemplate)
            ? StaticFileService.BuildStaticHtmlRoutes(ctx.StaticDir, staticTemplate, log.Warn)
            : Array.Empty<RouteInfo>();
        RouteInventoryValidator.ValidateFinalRoutes(routed, pluginContext.DerivedRouted, listRoutes, staticHtmlRoutes);
        var seoAlternates = SeoAlternatesService.AddVariantRouteAlternates(
            config,
            ctx.SeoAlternates,
            listRoutes,
            ctx.RootBaseUrl,
            ctx.DefaultLanguage);
        var seoIndex = SeoIndexBuilder.Build(config, baseUrl, renderQueue, listRoutes, seoAlternates);
        pluginContext.SeoIndex = seoIndex.Entries;
        SeoDiagnostics.AnalyzeIndex(config, seoIndex.Entries, seoIndex.Models, log);
        var currentKeys = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var maxDegreeOfParallelism = overrides.Jobs ?? Environment.ProcessorCount;
        var seoHtmlMode = (config.Site.Seo.RenderMode ?? "inject").Trim().ToLowerInvariant();
        var shouldProvideSeoModel = config.Site.Seo.Enabled && seoHtmlMode != "off";
        var shouldInjectSeo = shouldProvideSeoModel && seoHtmlMode == "inject";

        var renderPagesStopwatch = Stopwatch.StartNew();
        var renderResult = await PageRenderDispatcher.RenderPagesAsync(
            renderQueue, bodyStore, renderer, siteModel, outputDir, templateHash,
            incrementalEnabled, manifest, manifestEntries, currentKeys,
            maxDegreeOfParallelism, log, cancellationToken,
            shouldProvideSeoModel
                ? (_, route) => seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model) ? model : null!
                : null,
            shouldProvideSeoModel
                ? (item, route, page, html) =>
                {
                    var skipSeo = SeoInjectionPolicy.ShouldSkip(item.Meta);
                    if (shouldInjectSeo && !skipSeo)
                    {
                        html = SeoHtmlRenderer.InjectIntoHead(html, page.Seo, siteModel.Analytics);
                    }

                    return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, log);
                }
        : null);
        renderPagesStopwatch.Stop();
        variantStageMetrics.AddDuration("renderPages", renderPagesStopwatch.ElapsedMilliseconds);
        variantStageMetrics = MergeStageMetrics(variantStageMetrics, renderResult.StageMetrics);

        var renderedCount = renderResult.RenderedCount;
        var skippedCount = renderResult.SkippedCount;
        var renderReasons = new ConcurrentDictionary<string, int>(renderResult.RenderReasons, StringComparer.OrdinalIgnoreCase);

        var renderSpecialListsStopwatch = Stopwatch.StartNew();
        var specialListResult = await PageRenderDispatcher.RenderSpecialListsAsync(
            routed, bodyStore, renderer, siteModel, config.Site.Collections, ctx.LayoutsDir, config.Build.ListPageContentMode, config.Site.OutputPathEncoding, outputDir, templateHash,
            incrementalEnabled, manifest, currentKeys, renderReasons, cancellationToken,
            shouldProvideSeoModel
                ? (item, route) => SeoModelBuilder.BuildForContent(
                    config,
                    baseUrl,
                    item,
                    route,
                    GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildAlternateKey(item, route)))
                : null,
            shouldProvideSeoModel
                ? (route, page) => seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model)
                    ? model
                    : SeoModelBuilder.BuildForList(
                        config,
                        baseUrl,
                        page,
                        GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildListAlternateKey(route)))
                : null,
            shouldProvideSeoModel
                ? (route, page, html) =>
                {
                    if (shouldInjectSeo)
                    {
                        html = SeoHtmlRenderer.InjectIntoHead(html, page.Seo, siteModel.Analytics);
                    }

                    return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, log);
                }
        : null);
        renderSpecialListsStopwatch.Stop();
        variantStageMetrics.AddDuration("renderSpecialLists", renderSpecialListsStopwatch.ElapsedMilliseconds);
        variantStageMetrics = MergeStageMetrics(variantStageMetrics, specialListResult.StageMetrics);
        renderedCount += specialListResult.RenderedCount;
        skippedCount += specialListResult.SkippedCount;

        if (incrementalEnabled && manifestEntries is not null)
        {
            manifest.Entries = manifestEntries.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        }

        if (hasStaticDir || (ctx.ParentStaticDir is not null && Directory.Exists(ctx.ParentStaticDir)))
        {
            var staticStopwatch = Stopwatch.StartNew();
            if (ctx.ParentStaticDir is not null && Directory.Exists(ctx.ParentStaticDir))
            {
                DirectoryCopy.Sync(ctx.ParentStaticDir, outputDir, new DirectoryCopyOptions { HashMode = config.Build.AssetHashMode });
            }

            if (hasStaticDir)
            {
                if (!string.IsNullOrWhiteSpace(staticTemplate))
                {
                    StaticFileService.RenderStaticFiles(ctx.StaticDir, outputDir, renderer, siteModel, staticTemplate, baseUrl, currentKeys, cancellationToken, log.Warn);
                }
                else
                {
                    DirectoryCopy.Sync(ctx.StaticDir, outputDir);
                }
            }

            TrackStaticOutputs(ctx.ParentStaticDir, hasStaticDir ? ctx.StaticDir : null, outputDir, manifest, incrementalEnabled, log, !string.IsNullOrWhiteSpace(staticTemplate));
            staticStopwatch.Stop();
            variantStageMetrics.AddDuration("staticSync", staticStopwatch.ElapsedMilliseconds);
        }

        if (Directory.Exists(ctx.AssetsDir) || (ctx.ParentAssetsDir is not null && Directory.Exists(ctx.ParentAssetsDir)))
        {
            var assetsSyncStopwatch = Stopwatch.StartNew();
            if (ctx.ParentAssetsDir is not null && Directory.Exists(ctx.ParentAssetsDir))
            {
                DirectoryCopy.Sync(ctx.ParentAssetsDir, Path.Combine(outputDir, "assets"));
            }

            if (Directory.Exists(ctx.AssetsDir))
            {
                ScssCompiler.CompileIfEnabled(ctx.AssetsDir, config.Theme.Scss, _logger);
                ImageOptimizer.OptimizeIfEnabled(ctx.AssetsDir, config.Theme.Images, _logger);
                DirectoryCopy.Sync(ctx.AssetsDir, Path.Combine(outputDir, "assets"), new DirectoryCopyOptions { HashMode = config.Build.AssetHashMode });
            }

            TrackAssetOutputs(ctx.ParentAssetsDir, ctx.AssetsDir, outputDir, manifest, incrementalEnabled, log);
            assetsSyncStopwatch.Stop();
            variantStageMetrics.AddDuration("assetsSync", assetsSyncStopwatch.ElapsedMilliseconds);
        }

        if (themeRegistry is not null)
        {
            var tokensStopwatch = Stopwatch.StartNew();
            var themeRoot = Path.Combine(rootDir, "themes", themeName!);
            var parentThemeRoot = !string.IsNullOrWhiteSpace(themeManifest?.Extends)
                ? Path.Combine(rootDir, "themes", themeManifest!.Extends)
                : null;

            var tokensLoader = new ThemeTokensLoader();
            var tokens = tokensLoader.LoadWithInheritance(themeRoot, parentThemeRoot);
            if (tokens is not null)
            {
                var tokensOutputPath = Path.Combine(outputDir, "assets", "css", "theme-tokens.css");
                ThemeTokensProcessor.WriteToFile(tokens, tokensOutputPath);
                log.Info($"event=tokens.generated output={tokensOutputPath}");
            }
            tokensStopwatch.Stop();
            variantStageMetrics.AddDuration("tokensGen", tokensStopwatch.ElapsedMilliseconds);
        }

        if (Directory.Exists(ctx.MediaDownloadDir))
        {
            var mediaCopyStopwatch = Stopwatch.StartNew();
            SyncMediaOutputs(ctx.MediaDownloadDir, outputDir, manifest, incrementalEnabled, log);
            mediaCopyStopwatch.Stop();
            variantStageMetrics.AddDuration("mediaCopy", mediaCopyStopwatch.ElapsedMilliseconds);
        }

        if (incrementalEnabled)
        {
            DeleteStaleManifestOutputs(outputDir, manifest, currentKeys, log);
        }

        var afterBuildStopwatch = Stopwatch.StartNew();
        await PluginRunner.RunAfterBuildAsync(pluginContext, cancellationToken);
        TrackPluginOutputs(pluginContext, outputDir, manifest, incrementalEnabled, log);
        RobotsTxtWriter.WriteIfRequested(config, outputDir, baseUrl, seoIndex.Entries);
        afterBuildStopwatch.Stop();
        variantStageMetrics.AddDuration("afterBuildPlugins", afterBuildStopwatch.ElapsedMilliseconds);

        if (incrementalEnabled)
        {
            manifest.Save(manifestPath);
            log.Info($"Incremental build: rendered={renderedCount}, skipped={skippedCount}, cache={cacheDir}");
        }

        if (ctx.DefaultLanguage is null)
        {
            log.Info($"Build completed: {Path.GetFullPath(outputDir)}");
        }
        else
        {
            log.Info($"Build completed: {Path.GetFullPath(outputDir)} (lang={config.Site.Language})");
        }

        variantTotalStopwatch.Stop();
        variantStageMetrics.AddDuration("variantTotal", variantTotalStopwatch.ElapsedMilliseconds);

        var result = new BuildVariantResult(
            config.Site.Language,
            outputDir,
            baseUrl,
            TemplateCapabilitiesResolver.SupportsSearchSnippets(TemplateCapabilitiesResolver.SearchTemplatePath, ctx.LayoutsDir),
            bodyStore,
            routed,
            pluginContext.DerivedRouted,
            pluginContext.DerivedRoutes,
            seoIndex.Entries,
            seoIndex.Models,
            pluginContext.PluginExecutions.ToList(),
            renderedCount,
            skippedCount,
            new Dictionary<string, int>(renderReasons, StringComparer.OrdinalIgnoreCase),
            variantStageMetrics.Snapshot());
        SeoAuditReportWriter.Write(config, outputDir, seoIndex.Entries, seoIndex.Models, log);
        return result;
    }

    private static BuildStageMetricsCollector MergeStageMetrics(BuildStageMetricsCollector collector, BuildStageMetrics metrics)
    {
        foreach (var kv in metrics.DurationsMs)
        {
            collector.AddDuration(kv.Key, kv.Value);
        }

        foreach (var kv in metrics.Counts)
        {
            collector.Increment(kv.Key, kv.Value);
        }

        return collector;
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
        if (site.Collections is null || site.Collections.Count == 0)
        {
            return null;
        }

        var rules = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, collection) in site.Collections)
        {
            rules[key] = new RouteGenerator.CollectionRouteRule(collection.Permalink, collection.Template);
        }

        return rules;
    }

    private static IReadOnlyList<SeoAlternateModel>? GetSeoAlternates(
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> alternates,
        string key)
    {
        return alternates.TryGetValue(key, out var list) && list.Count > 0 ? list : null;
    }

    public static IReadOnlyList<RouteInfo> GetListRoutes(IReadOnlyDictionary<string, CollectionConfig>? collections)
        => SeoAlternatesService.GetListRoutes(collections);

    public async Task BuildAsync(IContentProvider provider, BuildOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Clean && Directory.Exists(options.OutputDir))
        {
            Directory.Delete(options.OutputDir, recursive: true);
        }

        Directory.CreateDirectory(options.OutputDir);

        var baseUrl = BuildPathUtils.NormalizeBaseUrl(options.BaseUrl);
        var loadResult = await provider.LoadAsync(cancellationToken);
        var items = loadResult.Items;
        var bodyStore = loadResult.BodyStore;

        _logger.Info($"Loaded content: {items.Count}");

        var routed = items
            .Select(i => (Item: i, Route: RouteGenerator.Generate(i, options.OutputPathEncoding)))
            .ToList();

        var warnedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (item, route) in routed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BuildPathUtils.WarnIfWindowsIncompatible(route.OutputPath, warnedOutputPaths, _logger);
            var html = BuildPathUtils.RenderSimplePage(baseUrl, item.Title, route.Url, await ContentBodyResolver.GetHtmlAsync(item, bodyStore, cancellationToken));
            FileWriter.WriteUtf8(options.OutputDir, route.OutputPath, html);
        }

        FileWriter.WriteUtf8(options.OutputDir, "index.html", BuildPathUtils.RenderSimpleIndex(baseUrl, routed));
        FileWriter.WriteUtf8(options.OutputDir, Path.Combine("blog", "index.html"), BuildPathUtils.RenderSimpleIndex(baseUrl, routed.Where(x => x.Route.Url.StartsWith("/blog/", StringComparison.OrdinalIgnoreCase)).ToList(), "Blog"));
        FileWriter.WriteUtf8(options.OutputDir, Path.Combine("pages", "index.html"), BuildPathUtils.RenderSimpleIndex(baseUrl, routed.Where(x => x.Route.Url.StartsWith("/pages/", StringComparison.OrdinalIgnoreCase)).ToList(), "Pages"));

        if (!string.IsNullOrWhiteSpace(options.AssetsDir))
        {
            DirectoryCopy.Sync(options.AssetsDir, Path.Combine(options.OutputDir, "assets"));
        }

        if (!string.IsNullOrWhiteSpace(options.SiteUrl) && options.GenerateSitemap)
        {
            var metaRoutes = new List<(RouteInfo Route, DateTimeOffset LastModified)>(capacity: routed.Count + 3)
            {
                (new RouteInfo("/", "index.html", "pages/index.html"), DateTimeOffset.UtcNow),
                (new RouteInfo("/blog/", Path.Combine("blog", "index.html"), "pages/index.html"), DateTimeOffset.UtcNow),
                (new RouteInfo("/pages/", Path.Combine("pages", "index.html"), "pages/index.html"), DateTimeOffset.UtcNow)
            };

            metaRoutes.AddRange(routed.Select(x => (x.Route, x.Item.PublishAt)));
            SitemapGenerator.Generate(options.OutputDir, options.SiteUrl, baseUrl, metaRoutes);
        }

        if (!string.IsNullOrWhiteSpace(options.SiteUrl) && options.GenerateRss)
        {
            RssGenerator.Generate(options.OutputDir, options.SiteUrl, baseUrl, options.SiteTitle, null, routed, bodyStore);
        }

        _logger.Info($"Build completed: {Path.GetFullPath(options.OutputDir)}");
    }

    internal static void SyncMediaOutputs(string mediaDownloadDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger)
    {
        var mediaOutputDir = Path.Combine(outputDir, "assets", "uploads");
        DirectoryCopy.SyncFilesRecursive(mediaDownloadDir, mediaOutputDir, ignoreDotPrefixedFiles: true);

        var currentMedia = Directory.EnumerateFiles(mediaDownloadDir, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).StartsWith('.'))
            .Select(file =>
            {
                var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(mediaDownloadDir, file));
                var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine("assets", "uploads", relativePath));
                return new KeyValuePair<string, string>(outputPath, ComputeFileFingerprint(file));
            })
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        DeleteStaleTrackedFiles(outputDir, manifest.Media, currentMedia, incrementalEnabled, logger, "media");
        manifest.Media = currentMedia;
    }

    internal static void TrackPluginOutputs(BuildContext pluginContext, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger)
    {
        var currentOutputs = new Dictionary<string, PluginOutputManifestEntry>(StringComparer.Ordinal);
        if (pluginContext.Data.TryGetValue("__plugin_outputs", out var outputsObj) && outputsObj is HashSet<PluginOutputTrackingInfo> outputs)
        {
            foreach (var output in outputs)
            {
                var fullPath = FileWriter.GetSafeFullPath(outputDir, output.Path);
                if (File.Exists(fullPath))
                {
                    currentOutputs[BuildPathUtils.NormalizeRelPath(output.Path)] = new PluginOutputManifestEntry
                    {
                        Plugin = output.Plugin,
                        Hook = output.Hook,
                        Path = BuildPathUtils.NormalizeRelPath(output.Path),
                        Hash = ComputeFileFingerprint(fullPath)
                    };
                }
            }
        }

        DeleteStaleTrackedFiles(outputDir, manifest.PluginOutputs.ToDictionary(x => x.Key, x => x.Value.Hash, StringComparer.Ordinal), currentOutputs.ToDictionary(x => x.Key, x => x.Value.Hash, StringComparer.Ordinal), incrementalEnabled, logger, "plugin");
        manifest.PluginOutputs = currentOutputs;
    }

    internal static void TrackStaticOutputs(string? parentStaticDir, string? staticDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger, bool renderHtmlStaticFiles)
    {
        var currentStatic = new Dictionary<string, string>(StringComparer.Ordinal);
        AddStaticSourceOutputs(parentStaticDir, currentStatic, renderHtmlStaticFiles: false);
        AddStaticSourceOutputs(staticDir, currentStatic, renderHtmlStaticFiles);

        DeleteStaleTrackedFiles(outputDir, manifest.Static, currentStatic, incrementalEnabled, logger, "static");
        manifest.Static = currentStatic;
    }

    private static void AddStaticSourceOutputs(string? sourceDir, Dictionary<string, string> outputs, bool renderHtmlStaticFiles)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            if (renderHtmlStaticFiles && string.Equals(Path.GetExtension(file), ".html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(sourceDir, file));
            outputs[relativePath] = ComputeFileFingerprint(file);
        }
    }

    internal static void TrackAssetOutputs(string? parentAssetsDir, string assetsDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger)
    {
        var currentAssets = new Dictionary<string, string>(StringComparer.Ordinal);
        AddAssetSourceOutputs(parentAssetsDir, currentAssets);
        AddAssetSourceOutputs(assetsDir, currentAssets);

        DeleteStaleTrackedFiles(outputDir, manifest.Assets, currentAssets, incrementalEnabled, logger, "asset");
        manifest.Assets = currentAssets;
    }

    private static void AddAssetSourceOutputs(string? sourceDir, Dictionary<string, string> outputs)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(sourceDir, file));
            var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine("assets", relativePath));
            outputs[outputPath] = ComputeFileFingerprint(file);
        }
    }

    private static void DeleteStaleTrackedFiles(
        string outputDir,
        IReadOnlyDictionary<string, string> previous,
        IReadOnlyDictionary<string, string> current,
        bool incrementalEnabled,
        ILogger logger,
        string kind)
    {
        if (!incrementalEnabled)
        {
            return;
        }

        var outputFileSystem = new SafeOutputFileSystem(outputDir);
        foreach (var stale in previous.Keys.Where(key => !current.ContainsKey(key)).ToList())
        {
            try
            {
                var fullPath = outputFileSystem.GetSafeFullPath(stale);
                if (File.Exists(fullPath))
                {
                    outputFileSystem.DeleteFileAsync(stale, CancellationToken.None).GetAwaiter().GetResult();
                    DeleteEmptyDirectoriesUpToRoot(Path.GetDirectoryName(fullPath), outputDir);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.Warn($"Failed to delete stale {kind} output '{stale}': {ex.Message}");
            }
        }
    }

    private static string ComputeFileFingerprint(string file)
    {
        var info = new FileInfo(file);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    internal static void DeleteStaleManifestOutputs(string outputDir, BuildManifest manifest, ConcurrentDictionary<string, byte> currentKeys, ILogger logger)
    {
        var removed = manifest.Entries
            .Where(kv => !currentKeys.ContainsKey(kv.Key))
            .ToList();

        foreach (var kv in removed)
        {
            var relativePath = string.IsNullOrWhiteSpace(kv.Value.OutputPath) ? kv.Key : kv.Value.OutputPath;
            try
            {
                var fullPath = FileWriter.GetSafeFullPath(outputDir, relativePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    DeleteEmptyDirectoriesUpToRoot(Path.GetDirectoryName(fullPath), outputDir);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.Warn($"Failed to delete stale output '{relativePath}': {ex.Message}");
            }

            manifest.Entries.Remove(kv.Key);
        }
    }

    private static void DeleteEmptyDirectoriesUpToRoot(string? directory, string outputDir)
    {
        var root = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullDirectory, root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!Directory.Exists(fullDirectory) || Directory.EnumerateFileSystemEntries(fullDirectory).Any())
            {
                break;
            }

            Directory.Delete(fullDirectory);
            directory = Path.GetDirectoryName(fullDirectory);
        }
    }
}
