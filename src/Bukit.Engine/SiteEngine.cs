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
                SeoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal),
                RootBaseUrl: null, ManifestSuffix: null, DefaultLanguage: null);
            var result = await BuildVariantAsync(variantCtx, templateHashCache, cancellationToken);

            _logger.Info($"event=build.variant.done language={effectiveConfig.Site.Language} baseUrl={baseUrl}");
            MetricsWriter.WriteIfRequested(rootDir, overrides.MetricsPath, effectiveConfig, outputDir, items.Count, new[] { result });
            return;
        }

        var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(effectiveConfig.Site, languages);
        var rootBaseUrl = BuildPathUtils.NormalizeBaseUrl(effectiveConfig.Site.BaseUrl);
        var seoAlternates = BuildSeoAlternates(effectiveConfig, items, languages, defaultLanguage, rootBaseUrl);
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
                SeoAlternates: seoAlternates,
                RootBaseUrl: rootBaseUrl, ManifestSuffix: lang, DefaultLanguage: defaultLanguage);
            var result = await BuildVariantAsync(variantCtx, templateHashCache, cancellationToken);
            results.Add(result);
            _logger.Info($"event=build.variant.done language={lang} baseUrl={baseUrl} outputDir={variantOutputDir}");
        }

        I18nOutputMerger.GenerateRootOutputs(effectiveConfig, outputDir, rootBaseUrl, results, _logger, _searchIndexBuilder);
        SeoAuditReportWriter.WriteMerged(effectiveConfig, outputDir, results, _logger);
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
            Analytics = new AnalyticsModel
            {
                Enabled = config.Site.Analytics.Enabled,
                GoogleAnalyticsId = config.Site.Analytics.GoogleAnalyticsId
            },
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
        var listRoutes = BuildListRoutes(config.Site.Collections);
        var seoAlternates = AddVariantRouteAlternates(
            config,
            ctx.SeoAlternates,
            listRoutes,
            ctx.RootBaseUrl,
            ctx.DefaultLanguage);
        var seoIndex = SeoIndexBuilder.Build(config, baseUrl, renderQueue, listRoutes, seoAlternates);
        pluginContext.SeoIndex = seoIndex.Entries;
        SeoDiagnostics.AnalyzeIndex(config, seoIndex.Entries, seoIndex.Models, _logger);
        var currentKeys = new HashSet<string>(StringComparer.Ordinal);
        var maxDegreeOfParallelism = overrides.Jobs ?? Environment.ProcessorCount;
        var seoHtmlMode = (config.Site.Seo.RenderMode ?? "inject").Trim().ToLowerInvariant();
        var shouldProvideSeoModel = config.Site.Seo.Enabled && seoHtmlMode != "off";
        var shouldInjectSeo = shouldProvideSeoModel && seoHtmlMode == "inject";

        var renderPagesStopwatch = Stopwatch.StartNew();
        var renderResult = await PageRenderDispatcher.RenderPagesAsync(
            renderQueue, bodyStore, renderer, siteModel, outputDir, templateHash,
            incrementalEnabled, manifest, manifestEntries, currentKeys,
            maxDegreeOfParallelism, _logger, cancellationToken,
            shouldProvideSeoModel
                ? (_, route) => seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model) ? model : null!
                : null,
            shouldProvideSeoModel
                ? (_, route, page, html) =>
                {
                    if (shouldInjectSeo)
                    {
                        html = SeoHtmlRenderer.InjectIntoHead(html, page.Seo, siteModel.Analytics);
                    }

                    return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, _logger);
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
            routed, bodyStore, renderer, siteModel, config.Site.Collections, ctx.LayoutsDir, config.Build.ListPageContentMode, outputDir, templateHash,
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

                    return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, _logger);
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
            DirectoryCopy.SyncFiles(ctx.MediaDownloadDir, mediaOutputDir, ignoreDotPrefixedFiles: true);
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
        WriteRobotsTxtIfRequested(config, outputDir, baseUrl, seoIndex.Entries);
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
        SeoAuditReportWriter.Write(config, outputDir, seoIndex.Entries, seoIndex.Models, _logger);
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

    private static IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> BuildSeoAlternates(
        AppConfig config,
        IReadOnlyList<ContentItem> items,
        IReadOnlyList<string> languages,
        string defaultLanguage,
        string rootBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(config.Site.Url) || languages.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal);
        }

        var grouped = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var collectionRules = BuildCollectionRules(config.Site);
        foreach (var language in languages)
        {
            var baseUrl = I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, language);
            var variantItems = I18nOutputMerger
                .FilterItemsByLanguage(items, language, defaultLanguage)
                .Where(i => !MetaHelpers.IsDataItem(i))
                .ToList();
            var variantRouted = variantItems
                .Select(i => (Item: i, Route: RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
                .ToList();

            foreach (var (item, route) in variantRouted)
            {
                AddAlternate(SeoModelBuilder.BuildAlternateKey(item, route), language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, route.Url));
            }

            foreach (var route in BuildListRoutes(config.Site.Collections))
            {
                AddAlternate(SeoModelBuilder.BuildListAlternateKey(route), language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, route.Url));
            }

            foreach (var url in BuildTaxonomyRouteUrls(config, variantRouted))
            {
                AddAlternate($"route:{url}", language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, url));
            }

            foreach (var url in BuildPaginationRouteUrls(config, variantRouted))
            {
                AddAlternate($"route:{url}", language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, url));
            }
        }

        var result = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal);
        foreach (var (key, byLanguage) in grouped)
        {
            if (byLanguage.Count < 2)
            {
                continue;
            }

            var list = new List<SeoAlternateModel>(byLanguage.Count + 1);
            if (byLanguage.TryGetValue(defaultLanguage, out var defaultHref))
            {
                list.Add(new SeoAlternateModel("x-default", defaultHref));
            }

            foreach (var language in languages)
            {
                if (byLanguage.TryGetValue(language, out var href))
                {
                    list.Add(new SeoAlternateModel(language, href));
                }
            }

            result[key] = list;
        }

        return result;

        void AddAlternate(string key, string language, string href)
        {
            if (!grouped.TryGetValue(key, out var byLanguage))
            {
                byLanguage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                grouped[key] = byLanguage;
            }

            byLanguage[language] = href;
        }
    }

    private static IReadOnlyList<string> BuildTaxonomyRouteUrls(
        AppConfig config,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
    {
        if (string.Equals((config.Taxonomy.OutputMode ?? string.Empty).Trim(), "data", StringComparison.OrdinalIgnoreCase) ||
            routed.Count == 0)
        {
            return Array.Empty<string>();
        }

        var pageSize = NormalizeSeoPageSize(config.Taxonomy.PageSize);
        var result = new List<string>();
        if (config.Taxonomy.Kinds is { Count: > 0 } kinds)
        {
            foreach (var kindConfig in kinds)
            {
                var key = (kindConfig.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var kind = string.IsNullOrWhiteSpace(kindConfig.Kind) ? key : kindConfig.Kind.Trim();
                AddTaxonomyKindRoutes(result, kind, BuildTaxonomyTermCounts(routed, key), pageSize, kindConfig.IndexEnabled ?? config.Taxonomy.IndexEnabled);
            }

            return result;
        }

        AddTaxonomyKindRoutes(result, "tags", BuildTaxonomyTermCounts(routed, "tags"), pageSize, config.Taxonomy.IndexEnabled);
        AddTaxonomyKindRoutes(result, "categories", BuildTaxonomyTermCounts(routed, "categories"), pageSize, config.Taxonomy.IndexEnabled);
        return result;
    }

    private static IReadOnlyList<string> BuildPaginationRouteUrls(
        AppConfig config,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
    {
        if (routed.Count == 0)
        {
            return Array.Empty<string>();
        }

        var collectionKey = "post";
        var listRoute = "/blog/";
        var pageSize = 10;
        if (config.Site.Collections is { Count: > 0 })
        {
            var paginationCollection = config.Site.Collections.FirstOrDefault(x => x.Value.Pagination.Enabled);
            if (paginationCollection.Value is null)
            {
                return Array.Empty<string>();
            }

            collectionKey = paginationCollection.Key;
            listRoute = paginationCollection.Value.ListRoute ?? listRoute;
            pageSize = paginationCollection.Value.Pagination.PageSize;
        }

        pageSize = NormalizeSeoPageSize(pageSize);
        var count = routed.Count(x => string.Equals(GetCollection(x.Item), collectionKey, StringComparison.OrdinalIgnoreCase));
        if (count <= pageSize)
        {
            return Array.Empty<string>();
        }

        var totalPages = (int)Math.Ceiling(count / (double)pageSize);
        var normalizedListRoute = NormalizeListRoute(listRoute);
        var result = new List<string>(totalPages - 1);
        for (var page = 2; page <= totalPages; page++)
        {
            result.Add($"{normalizedListRoute}page/{page}/");
        }

        return result;
    }

    private static void AddTaxonomyKindRoutes(
        List<string> result,
        string kind,
        IReadOnlyDictionary<string, int> termCounts,
        int pageSize,
        bool indexEnabled)
    {
        if (string.IsNullOrWhiteSpace(kind) || termCounts.Count == 0)
        {
            return;
        }

        var normalizedKind = kind.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedKind))
        {
            return;
        }

        if (indexEnabled)
        {
            result.Add($"/{normalizedKind}/");
        }

        foreach (var (slug, count) in termCounts)
        {
            result.Add($"/{normalizedKind}/{slug}/");
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);
            for (var page = 2; page <= totalPages; page++)
            {
                result.Add($"/{normalizedKind}/{slug}/page/{page}/");
            }
        }
    }

    private static IReadOnlyDictionary<string, int> BuildTaxonomyTermCounts(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        string key)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (item, _) in routed)
        {
            var values = GetSeoStringList(item.Meta, key);
            if (values is null)
            {
                continue;
            }

            foreach (var value in values)
            {
                var slug = SlugifySeoSegment(value);
                if (string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }

                result[slug] = result.TryGetValue(slug, out var count) ? count + 1 : 1;
            }
        }

        return result;
    }

    private static IReadOnlyList<string>? GetSeoStringList(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is string text)
        {
            var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (value is IEnumerable<object> values)
        {
            var list = values
                .Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
            return list.Count == 0 ? null : list;
        }

        return null;
    }

    private static string GetCollection(ContentItem item)
    {
        if (item.Meta.TryGetValue("collection", out var collection) && collection is not null && !string.IsNullOrWhiteSpace(collection.ToString()))
        {
            return collection.ToString()!;
        }

        if (item.Meta.TryGetValue("type", out var type) && type is not null && !string.IsNullOrWhiteSpace(type.ToString()))
        {
            return type.ToString()!;
        }

        return "page";
    }

    private static int NormalizeSeoPageSize(int pageSize) => pageSize <= 0 ? 10 : pageSize;

    private static string SlugifySeoSegment(string text)
    {
        var trimmed = text.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(trimmed.Length);
        var dash = false;
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                dash = false;
                continue;
            }

            if (ch is ' ' or '-' or '_' or '.')
            {
                if (!dash && sb.Length > 0)
                {
                    sb.Append('-');
                    dash = true;
                }
            }
        }

        return sb.ToString().Trim('-');
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> AddVariantRouteAlternates(
        AppConfig config,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> existing,
        IEnumerable<RouteInfo> routes,
        string? rootBaseUrl,
        string? defaultLanguage)
    {
        var languages = I18nOutputMerger.GetLanguages(config.Site);
        if (string.IsNullOrWhiteSpace(config.Site.Url) ||
            string.IsNullOrWhiteSpace(rootBaseUrl) ||
            string.IsNullOrWhiteSpace(defaultLanguage) ||
            languages.Count < 2)
        {
            return existing;
        }

        Dictionary<string, IReadOnlyList<SeoAlternateModel>>? result = null;
        foreach (var route in routes)
        {
            var key = SeoModelBuilder.BuildListAlternateKey(route);
            if (existing.ContainsKey(key))
            {
                continue;
            }

            result ??= new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(existing, StringComparer.Ordinal);
            var alternates = new List<SeoAlternateModel>(languages.Count + 1);
            alternates.Add(new SeoAlternateModel(
                "x-default",
                SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, defaultLanguage), route.Url)));

            foreach (var language in languages)
            {
                alternates.Add(new SeoAlternateModel(
                    language,
                    SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, language), route.Url)));
            }

            result[key] = alternates;
        }

        return result ?? existing;
    }

    private static IReadOnlyList<RouteInfo> BuildListRoutes(IReadOnlyDictionary<string, CollectionConfig>? collections)
    {
        var routes = new List<RouteInfo>
        {
            new("/", "index.html", "pages/index.html")
        };

        if (collections is null || collections.Count == 0)
        {
            routes.Add(new RouteInfo("/blog/", Path.Combine("blog", "index.html"), "pages/list.html"));
            routes.Add(new RouteInfo("/pages/", Path.Combine("pages", "index.html"), "pages/list.html"));
            return routes;
        }

        foreach (var (_, collection) in collections)
        {
            if (string.IsNullOrWhiteSpace(collection.ListRoute))
            {
                continue;
            }

            var url = NormalizeListRoute(collection.ListRoute);
            routes.Add(new RouteInfo(url, BuildListOutputPath(url), "pages/list.html"));
        }

        return routes;
    }

    private static string NormalizeListRoute(string route)
    {
        var value = (route ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/";
        }

        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        if (!value.EndsWith('/'))
        {
            value += "/";
        }

        return value;
    }

    private static string BuildListOutputPath(string route)
    {
        var normalized = NormalizeListRoute(route).Trim('/');
        return string.IsNullOrWhiteSpace(normalized)
            ? "index.html"
            : Path.Combine(normalized.Replace('/', Path.DirectorySeparatorChar), "index.html");
    }

    private static void WriteRobotsTxtIfRequested(
        AppConfig config,
        string outputDir,
        string baseUrl,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex)
    {
        if (!config.Site.Seo.RobotsTxt.Enabled || string.IsNullOrWhiteSpace(config.Site.Url))
        {
            return;
        }

        var robotsPath = Path.Combine(outputDir, "robots.txt");
        if (File.Exists(robotsPath))
        {
            return;
        }

        var lines = new List<string>
        {
            "User-agent: *",
            "Allow: /"
        };
        if (seoIndex.Values.Any(x => x.Indexable))
        {
            lines.Add($"Sitemap: {SitemapGenerator.BuildAbsoluteUrl(config.Site.Url, baseUrl, "/sitemap.xml")}");
        }

        FileWriter.WriteUtf8(outputDir, "robots.txt", string.Join(Environment.NewLine, lines) + Environment.NewLine);
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
