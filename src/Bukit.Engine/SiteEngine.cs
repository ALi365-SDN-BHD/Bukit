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
        var (layoutsDir, assetsDir, staticDir) = BuildPathUtils.ResolveThemeDirectories(rootDir, effectiveConfig.Theme);

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
        var templateHashCache = new DirectoryHashCache();

        var languages = I18nOutputMerger.GetLanguages(effectiveConfig.Site);
        if (languages.Count == 0)
        {
            var baseUrl = BuildPathUtils.NormalizeBaseUrl(effectiveConfig.Site.BaseUrl);
            _logger.Info($"event=build.variant.start language={effectiveConfig.Site.Language} baseUrl={baseUrl}");
            var variantCtx = new BuildVariantContext(
                effectiveConfig, rootDir, overrides, items, bodyStore, outputDir, baseUrl,
                layoutsDir, assetsDir, staticDir, mediaCacheDir,
                ManifestSuffix: null, DefaultLanguage: null);
            var result = await BuildVariantAsync(variantCtx, templateHashCache, cancellationToken);

            _logger.Info($"event=build.variant.done language={effectiveConfig.Site.Language} baseUrl={baseUrl}");
            MetricsWriter.WriteIfRequested(rootDir, overrides.MetricsPath, effectiveConfig, outputDir, items.Count, new[] { result });
            return;
        }

        var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(effectiveConfig.Site, languages);
        var rootBaseUrl = BuildPathUtils.NormalizeBaseUrl(effectiveConfig.Site.BaseUrl);
        var results = new List<BuildVariantResult>(capacity: languages.Count);
        for (var i = 0; i < languages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lang = languages[i];
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
            _logger.Info($"event=build.variant.start language={lang} baseUrl={baseUrl} outputDir={variantOutputDir}");
            var variantCtx = new BuildVariantContext(
                variantConfig, rootDir, overrides, variantItems, bodyStore, variantOutputDir, baseUrl,
                layoutsDir, assetsDir, staticDir, mediaCacheDir,
                ManifestSuffix: lang, DefaultLanguage: defaultLanguage);
            var result = await BuildVariantAsync(variantCtx, templateHashCache, cancellationToken);
            results.Add(result);
            _logger.Info($"event=build.variant.done language={lang} baseUrl={baseUrl} outputDir={variantOutputDir}");
        }

        I18nOutputMerger.GenerateRootOutputs(effectiveConfig, outputDir, rootBaseUrl, results, _logger, _searchIndexBuilder);
        _logger.Info("event=build.done");
        MetricsWriter.WriteIfRequested(rootDir, overrides.MetricsPath, effectiveConfig, outputDir, items.Count, results);
    }

    private async Task<BuildVariantResult> BuildVariantAsync(
        BuildVariantContext ctx,
        DirectoryHashCache templateHashCache,
        CancellationToken cancellationToken)
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

        if (Directory.Exists(ctx.StaticDir))
        {
            DirectoryCopy.Sync(ctx.StaticDir, outputDir);
        }

        var splitItemsStopwatch = Stopwatch.StartNew();
        var dataItems = items.Where(MetaHelpers.IsDataItem).ToList();
        var contentItems = items.Where(i => !MetaHelpers.IsDataItem(i)).ToList();
        var modules = DataModuleBuilder.BuildModules(dataItems, config.Site.Language, bodyStore);
        splitItemsStopwatch.Stop();
        variantStageMetrics.AddDuration("prepareContent", splitItemsStopwatch.ElapsedMilliseconds);

        ITemplateRenderer renderer = new ScribanTemplateRendererAdapter(ctx.LayoutsDir);
        var collectionRules = BuildCollectionRules(config.Site);

        var routeGenerationStopwatch = Stopwatch.StartNew();
        var routed = contentItems
            .Select(i => (Item: i, Route: RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .ToList();
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
            Logger = _logger
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

        var siteModel = new SiteModel
        {
            Name = config.Site.Name,
            Title = config.Site.Title,
            Url = config.Site.Url,
            Description = config.Site.Description,
            BaseUrl = baseUrl,
            Language = config.Site.Language,
            Params = config.Theme.Params,
            Modules = modules,
            Data = pluginContext.Data.Count == 0 ? null : pluginContext.Data
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
        var currentKeys = new HashSet<string>(StringComparer.Ordinal);
        var maxDegreeOfParallelism = overrides.Jobs ?? Environment.ProcessorCount;

        var renderPagesStopwatch = Stopwatch.StartNew();
        var renderResult = await PageRenderDispatcher.RenderPagesAsync(
            renderQueue, bodyStore, renderer, siteModel, outputDir, templateHash,
            incrementalEnabled, manifest, manifestEntries, currentKeys,
            maxDegreeOfParallelism, _logger, cancellationToken);
        renderPagesStopwatch.Stop();
        variantStageMetrics.AddDuration("renderPages", renderPagesStopwatch.ElapsedMilliseconds);
        variantStageMetrics = MergeStageMetrics(variantStageMetrics, renderResult.StageMetrics);

        var renderedCount = renderResult.RenderedCount;
        var skippedCount = renderResult.SkippedCount;
        var renderReasons = new ConcurrentDictionary<string, int>(renderResult.RenderReasons, StringComparer.OrdinalIgnoreCase);

        var renderSpecialListsStopwatch = Stopwatch.StartNew();
        var specialListResult = await PageRenderDispatcher.RenderSpecialListsAsync(
            routed, bodyStore, renderer, siteModel, config.Site.Collections, ctx.LayoutsDir, config.Build.ListPageContentMode, outputDir, templateHash,
            incrementalEnabled, manifest, currentKeys, renderReasons);
        renderSpecialListsStopwatch.Stop();
        variantStageMetrics.AddDuration("renderSpecialLists", renderSpecialListsStopwatch.ElapsedMilliseconds);
        variantStageMetrics = MergeStageMetrics(variantStageMetrics, specialListResult.StageMetrics);
        renderedCount += specialListResult.RenderedCount;
        skippedCount += specialListResult.SkippedCount;

        if (incrementalEnabled && manifestEntries is not null)
        {
            manifest.Entries = manifestEntries.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        }

        if (Directory.Exists(ctx.AssetsDir))
        {
            var assetsSyncStopwatch = Stopwatch.StartNew();
            DirectoryCopy.Sync(ctx.AssetsDir, Path.Combine(outputDir, "assets"));
            assetsSyncStopwatch.Stop();
            variantStageMetrics.AddDuration("assetsSync", assetsSyncStopwatch.ElapsedMilliseconds);
        }

        if (Directory.Exists(ctx.MediaDownloadDir))
        {
            var mediaCopyStopwatch = Stopwatch.StartNew();
            var mediaOutputDir = Path.Combine(outputDir, "assets", "uploads");
            Directory.CreateDirectory(mediaOutputDir);
            foreach (var file in Directory.EnumerateFiles(ctx.MediaDownloadDir))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith('.'))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(mediaOutputDir, name), overwrite: true);
            }
            mediaCopyStopwatch.Stop();
            variantStageMetrics.AddDuration("mediaCopy", mediaCopyStopwatch.ElapsedMilliseconds);
        }

        if (incrementalEnabled)
        {
            var removed = manifest.Entries.Keys.Where(k => !currentKeys.Contains(k)).ToList();
            foreach (var k in removed)
            {
                manifest.Entries.Remove(k);
            }
        }

        var afterBuildStopwatch = Stopwatch.StartNew();
        await PluginRunner.RunAfterBuildAsync(pluginContext, cancellationToken);
        afterBuildStopwatch.Stop();
        variantStageMetrics.AddDuration("afterBuildPlugins", afterBuildStopwatch.ElapsedMilliseconds);

        if (incrementalEnabled)
        {
            manifest.Save(manifestPath);
            _logger.Info($"Incremental build: rendered={renderedCount}, skipped={skippedCount}, cache={cacheDir}");
        }

        if (ctx.DefaultLanguage is null)
        {
            _logger.Info($"Build completed: {Path.GetFullPath(outputDir)}");
        }
        else
        {
            _logger.Info($"Build completed: {Path.GetFullPath(outputDir)} (lang={config.Site.Language})");
        }

        variantTotalStopwatch.Stop();
        variantStageMetrics.AddDuration("variantTotal", variantTotalStopwatch.ElapsedMilliseconds);

        return new BuildVariantResult(
            config.Site.Language,
            outputDir,
            baseUrl,
            TemplateCapabilitiesResolver.SupportsSearchSnippets(TemplateCapabilitiesResolver.SearchTemplatePath, ctx.LayoutsDir),
            bodyStore,
            routed,
            pluginContext.DerivedRouted,
            pluginContext.DerivedRoutes,
            pluginContext.PluginExecutions.ToList(),
            renderedCount,
            skippedCount,
            new Dictionary<string, int>(renderReasons, StringComparer.OrdinalIgnoreCase),
            variantStageMetrics.Snapshot());
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
