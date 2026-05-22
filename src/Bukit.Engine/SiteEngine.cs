using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Engine.Plugins;
using Bukit.Engine.Incremental;
using Bukit.Config;
using Bukit.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

public sealed class SiteEngine
{
    private readonly ILogger _logger;
    private readonly IContentProviderFactory _contentProviderFactory;
    private readonly ISearchIndexBuilder _searchIndexBuilder;

    public SiteEngine(ILogger logger)
        : this(logger, new DefaultContentProviderFactory(), new DefaultSearchIndexBuilder())
    {
    }

    internal SiteEngine(ILogger logger, IContentProviderFactory contentProviderFactory, ISearchIndexBuilder searchIndexBuilder)
    {
        _logger = logger;
        _contentProviderFactory = contentProviderFactory;
        _searchIndexBuilder = searchIndexBuilder;
    }

    public async Task BuildAsync(AppConfig config, string rootDir, ConfigOverrides overrides, CancellationToken cancellationToken = default)
    {
        var effectiveConfig = ConfigApplier.Apply(config, overrides);
        ConfigValidator.Validate(effectiveConfig);

        var outputDir = BuildPathUtils.MakeAbsolute(rootDir, effectiveConfig.Build.Output);
        var (layoutsDir, assetsDir, staticDir, parentLayoutsDir, parentAssetsDir, parentStaticDir, userLayoutsDir) = BuildPathUtils.ResolveThemeDirectories(rootDir, effectiveConfig.Theme);

        if (effectiveConfig.Build.Clean && Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }

        Directory.CreateDirectory(outputDir);
        _logger.Info($"event=build.start rootDir={rootDir} outputDir={outputDir}");

        var mediaCacheDir = string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(rootDir, ".cache", "media")
            : Path.Combine(Path.GetFullPath(overrides.CacheDir!), "media");

        var provider = _contentProviderFactory.Create(effectiveConfig, rootDir, overrides.IsCI, _logger);
        var loadResult = await provider.LoadAsync(cancellationToken);
        loadResult = await _contentProviderFactory.LocalizeContentImagesAsync(loadResult, effectiveConfig.Content.Media, rootDir, mediaCacheDir, _logger, cancellationToken);
        var items = loadResult.Items;
        var bodyStore = loadResult.BodyStore;

        if (!effectiveConfig.Build.Draft)
        {
            var before = items.Count;
            items = items.Where(i =>
                !(i.Meta.TryGetValue("draft", out var d) && d is true or "true" or "True")).ToList();
            if (items.Count < before)
            {
                _logger.Info($"event=content.draft_filtered removed={before - items.Count}");
            }
        }

        _logger.Info($"event=content.loaded count={items.Count}");

        var schemaErrors = ValidateContentSchemas(effectiveConfig.Site.Collections, items, _logger);
        if (schemaErrors.Count > 0)
        {
            var schemaFailMode = (effectiveConfig.Build.SchemaFailMode ?? "warn").Trim().ToLowerInvariant();
            if (schemaFailMode == "strict")
            {
                throw new ConfigException($"Schema validation failed with {schemaErrors.Count} error(s).");
            }
        }

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
            return;
        }

        var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(effectiveConfig.Site, languages);
        var rootBaseUrl = BuildPathUtils.NormalizeBaseUrl(effectiveConfig.Site.BaseUrl);
        var seoAlternates = SeoAlternatesService.BuildSeoAlternates(effectiveConfig, items, languages, defaultLanguage, rootBaseUrl);
        var results = new BuildVariantResult[languages.Count];
        await Parallel.ForEachAsync(
            languages.Select((lang, i) => (lang, i)),
            new ParallelOptions { MaxDegreeOfParallelism = languages.Count, CancellationToken = cancellationToken },
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

        var hasStaticDir = Directory.Exists(ctx.StaticDir);
        var staticTemplate = config.Theme.StaticTemplate;

        var splitItemsStopwatch = Stopwatch.StartNew();
        var dataItems = items.Where(MetaHelpers.IsDataItem).ToList();
        var contentItems = items.Where(i => !MetaHelpers.IsDataItem(i)).ToList();
        var modules = DataModuleBuilder.BuildModules(dataItems, config.Site.Language, bodyStore);
        var sourceData = DataModuleBuilder.BuildDataBySource(dataItems, bodyStore);
        splitItemsStopwatch.Stop();
        variantStageMetrics.AddDuration("prepareContent", splitItemsStopwatch.ElapsedMilliseconds);

        ITemplateRenderer renderer = new ScribanTemplateRendererAdapter(ctx.LayoutsDir, ctx.ParentLayoutsDir, config.Theme.Shortcodes, config.Theme.Components, ctx.UserLayoutsDir);
        var collectionRules = BuildCollectionRules(config.Site);

        var routeGenerationStopwatch = Stopwatch.StartNew();
        var routed = contentItems
            .Select(i => (Item: i, Route: RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .ToList();
        RouteInventoryValidator.ValidateContentRoutes(routed);
        routeGenerationStopwatch.Stop();
        variantStageMetrics.AddDuration("routeGeneration", routeGenerationStopwatch.ElapsedMilliseconds);

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
        var templateHash = incrementalEnabled ? templateHashCache.GetOrAdd(ctx.LayoutsDir) : string.Empty;
        templateHashStopwatch.Stop();
        variantStageMetrics.AddDuration("templateHash", templateHashStopwatch.ElapsedMilliseconds);
        var manifest = incrementalEnabled ? BuildManifest.Load(manifestPath) : new BuildManifest();
        manifest.TemplateHash = templateHash;
        var manifestEntries = incrementalEnabled
            ? new ConcurrentDictionary<string, BuildManifestEntry>(manifest.Entries, StringComparer.Ordinal)
            : null;

        var renderQueue = routed.Concat(pluginContext.DerivedRouted).ToList();
        var listRoutes = SeoAlternatesService.BuildListRoutes(config.Site.Collections);
        RouteInventoryValidator.ValidateFinalRoutes(routed, pluginContext.DerivedRouted, listRoutes);
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

        if (hasStaticDir)
        {
            var staticStopwatch = Stopwatch.StartNew();
            if (!string.IsNullOrWhiteSpace(staticTemplate))
            {
                StaticFileService.RenderStaticFiles(ctx.StaticDir, outputDir, renderer, siteModel, staticTemplate, baseUrl, currentKeys, cancellationToken);
            }
            else
            {
                DirectoryCopy.Sync(ctx.StaticDir, outputDir);
            }
            if (ctx.ParentStaticDir is not null && Directory.Exists(ctx.ParentStaticDir))
            {
                DirectoryCopy.Sync(ctx.ParentStaticDir, outputDir);
            }
            staticStopwatch.Stop();
            variantStageMetrics.AddDuration("staticSync", staticStopwatch.ElapsedMilliseconds);
        }

        if (Directory.Exists(ctx.AssetsDir))
        {
            var assetsSyncStopwatch = Stopwatch.StartNew();
            ScssCompiler.CompileIfEnabled(ctx.AssetsDir, config.Theme.Scss, _logger);
            ImageOptimizer.OptimizeIfEnabled(ctx.AssetsDir, config.Theme.Images, _logger);
            DirectoryCopy.Sync(ctx.AssetsDir, Path.Combine(outputDir, "assets"));
            if (ctx.ParentAssetsDir is not null && Directory.Exists(ctx.ParentAssetsDir))
            {
                DirectoryCopy.Sync(ctx.ParentAssetsDir, Path.Combine(outputDir, "assets"));
            }
            assetsSyncStopwatch.Stop();
            variantStageMetrics.AddDuration("assetsSync", assetsSyncStopwatch.ElapsedMilliseconds);
        }

        if (Directory.Exists(ctx.MediaDownloadDir))
        {
            var mediaCopyStopwatch = Stopwatch.StartNew();
            var mediaOutputDir = Path.Combine(outputDir, "assets", "uploads");
            DirectoryCopy.SyncFiles(ctx.MediaDownloadDir, mediaOutputDir, ignoreDotPrefixedFiles: true);
            mediaCopyStopwatch.Stop();
            variantStageMetrics.AddDuration("mediaCopy", mediaCopyStopwatch.ElapsedMilliseconds);
        }

        if (incrementalEnabled)
        {
            var removed = manifest.Entries.Keys.Where(k => !currentKeys.ContainsKey(k)).ToList();
            foreach (var k in removed)
            {
                manifest.Entries.Remove(k);
            }
        }

        var afterBuildStopwatch = Stopwatch.StartNew();
        await PluginRunner.RunAfterBuildAsync(pluginContext, cancellationToken);
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

    private static List<ContentSchemaValidator.SchemaValidationError> ValidateContentSchemas(
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        IReadOnlyList<ContentItem> items,
        ILogger logger)
    {
        var allErrors = new List<ContentSchemaValidator.SchemaValidationError>();

        if (collections is null || collections.Count == 0)
        {
            return allErrors;
        }

        foreach (var item in items)
        {
            var collectionName = GetEffectiveCollection(item);
            if (string.IsNullOrWhiteSpace(collectionName) ||
                !collections.TryGetValue(collectionName, out var collection) ||
                collection.Schema is null || collection.Schema.Count == 0)
            {
                continue;
            }

            var errors = ContentSchemaValidator.Validate(item.Meta, collection.Schema, item.Id);
            if (errors.Count > 0)
            {
                allErrors.AddRange(errors);
                foreach (var error in errors)
                {
                    logger.Warn($"event=schema.validation code={error.Code} field={error.Field} source={error.SourcePath} message={error.Message}");
                }
            }
        }

        return allErrors;
    }

    private static string GetEffectiveCollection(ContentItem item)
    {
        if (item.Meta.TryGetValue("collection", out var c) && c is not null && !string.IsNullOrWhiteSpace(c.ToString()))
        {
            return c.ToString()!;
        }

        if (item.Meta.TryGetValue("type", out var t) && t is not null && !string.IsNullOrWhiteSpace(t.ToString()))
        {
            return t.ToString()!;
        }

        return "page";
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
}
