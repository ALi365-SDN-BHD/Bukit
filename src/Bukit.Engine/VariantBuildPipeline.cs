using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Incremental;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
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

    internal (IReadOnlyList<RouteInfo> StaticHtmlRoutes, string StaticRouteTemplate) BuildStaticHtmlData(
        string? staticDir, string? staticTemplate,
        Action<string> warn, bool publishDotFiles)
    {
        var template = !string.IsNullOrWhiteSpace(staticTemplate) ? staticTemplate : "__raw_static__";
        var hasStaticDir = staticDir is not null && Directory.Exists(staticDir);
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

        var bootstrap = ThemeBootstrapper.Bootstrap(config, rootDir, logger);
        var themeName = bootstrap.ThemeName;
        var themeRoot = bootstrap.ThemeRoot;
        var parentThemeRoot = bootstrap.ParentThemeRoot;
        var themeManifest = bootstrap.Manifest;
        var themeRegistry = bootstrap.Registry;
        var schemaValidator = bootstrap.SchemaValidator;
        var resolvedSectionPlugins = bootstrap.SectionPlugins;

        var splitItemsStopwatch = Stopwatch.StartNew();
        var dataModules = PrepareDataModules(items, config.Site.Language, bodyStore);
        splitItemsStopwatch.Stop();
        variantStageMetrics.AddDuration("prepareContent", splitItemsStopwatch.ElapsedMilliseconds);

        var routeGenerationStopwatch = Stopwatch.StartNew();
        var routeResult = GenerateRoutes(config, items);
        var routed = routeResult.Routed;
        routeGenerationStopwatch.Stop();
        variantStageMetrics.AddDuration("routeGeneration", routeGenerationStopwatch.ElapsedMilliseconds);

        IReadOnlyList<(ContentItem Item, RouteInfo? Route)>? allPagesForSections = themeRegistry is not null
            ? routed.Select(x => ((ContentItem)x.Item, (RouteInfo?)x.Route)).ToList()
            : null;

        ITemplateRenderer renderer = rendererFactory is not null
            ? rendererFactory(ctx.LayoutsDir)
            : CreateRenderer(ctx, themeRegistry, schemaValidator, resolvedSectionPlugins, allPagesForSections);

        var pluginContext = new BuildContext
        {
            Config = config,
            RootDir = rootDir,
            OutputDir = outputDir,
            BaseUrl = baseUrl,
            LayoutsDir = ctx.LayoutsDir,
            Routed = routed,
            BodyStore = bodyStore,
            Logger = logger
        };

        var taxonomyStopwatch = Stopwatch.StartNew();
        TaxonomyTermsInjector.InjectFromDataItems(pluginContext, dataModules.DataItems);
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

        var siteModel = BuildSiteModel(config, baseUrl, dataModules.Modules, dataModules.SourceData, pluginContext.Data);

        var manifestSetup = SetupManifest(ctx, overrides, templateHashCache);

        var renderQueue = routed.Concat(pluginContext.DerivedRouted).ToList();
        var listRoutes = routeResult.ListRoutes;

        var hasStaticDir = Directory.Exists(ctx.StaticDir);
        var (staticHtmlRoutes, staticRouteTemplate) = BuildStaticHtmlData(
            hasStaticDir ? ctx.StaticDir : null,
            config.Theme.StaticTemplate,
            msg => (logger).Warn(msg),
            config.Build.PublishDotFiles);

        RouteInventoryValidator.ValidateFinalRoutes(routed, pluginContext.DerivedRouted, listRoutes, staticHtmlRoutes);
        var seoAlternates = SeoAlternatesService.AddVariantRouteAlternates(
            config, ctx.SeoAlternates, listRoutes, ctx.RootBaseUrl, ctx.DefaultLanguage);
        var maxDegreeOfParallelism = overrides.Jobs ?? Environment.ProcessorCount;

        var seoResult = new SeoPipeline().Execute(
            config, baseUrl, renderQueue, listRoutes, seoAlternates, siteModel.Analytics, logger);
        pluginContext.SeoIndex = seoResult.SeoIndex.Entries;

        var renderDependencyHashStopwatch = Stopwatch.StartNew();
        var renderDependencyHash = manifestSetup.IncrementalEnabled
            ? RenderDependencyHasher.Compute(config, siteModel)
            : string.Empty;
        renderDependencyHashStopwatch.Stop();
        variantStageMetrics.AddDuration("renderDependencyHash", renderDependencyHashStopwatch.ElapsedMilliseconds);

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
            MaxDegreeOfParallelism: maxDegreeOfParallelism, Logger: logger,
            SeoBuilder: seoResult.SeoBuilder,
            HtmlPostProcessor: seoResult.HtmlPostProcessor,
            ListItemSeoBuilder: seoResult.ListItemSeoBuilder,
            ListSeoBuilder: seoResult.ListSeoBuilder,
            ListHtmlPostProcessor: seoResult.ListHtmlPostProcessor),
            cancellationToken);

        variantStageMetrics.Merge(renderPipelineResult.StageMetrics);

        var renderedCount = renderPipelineResult.RenderedCount;
        var skippedCount = renderPipelineResult.SkippedCount;
        var renderReasons = new ConcurrentDictionary<string, int>(
            renderPipelineResult.RenderReasons, StringComparer.OrdinalIgnoreCase);
        var currentKeys = renderPipelineResult.CurrentKeys;

        var (themeRootForTokens, parentThemeRootForTokens) = GetThemeRootForTokens(
            themeRoot, themeRegistry is not null, parentThemeRoot,
            !string.IsNullOrWhiteSpace(themeManifest?.Extends));

        var assetPipelineResult = await new AssetPipeline().ExecuteAsync(new AssetPipelineContext(
            StaticDir: hasStaticDir ? ctx.StaticDir : null,
            ParentStaticDir: ctx.ParentStaticDir, AssetsDir: ctx.AssetsDir,
            ParentAssetsDir: ctx.ParentAssetsDir, MediaDownloadDir: ctx.MediaDownloadDir,
            ThemeRoot: themeRootForTokens, ParentThemeRoot: parentThemeRootForTokens,
            OutputDir: outputDir, BaseUrl: baseUrl, Renderer: renderer,
            SiteModel: siteModel, StaticTemplate: staticRouteTemplate,
            Manifest: manifestSetup.Manifest, IncrementalEnabled: manifestSetup.IncrementalEnabled,
            AssetHashMode: config.Build.AssetHashMode,
            ScssConfig: config.Theme.Scss, ImageConfig: config.Theme.Images,
            Logger: logger, CurrentKeys: currentKeys,
            PublishDotFiles: config.Build.PublishDotFiles),
            cancellationToken);

        variantStageMetrics.Merge(assetPipelineResult.StageMetrics);

        var pluginPipelineResult = await new PluginPipeline().ExecuteAsync(new PluginPipelineContext(
            PluginContext: pluginContext, OutputDir: outputDir, BaseUrl: baseUrl,
            Manifest: manifestSetup.Manifest, ManifestPath: manifestSetup.ManifestPath,
            IncrementalEnabled: manifestSetup.IncrementalEnabled, CurrentKeys: currentKeys,
            RenderedCount: renderedCount, SkippedCount: skippedCount,
            Logger: logger, Config: config),
            cancellationToken);
        variantStageMetrics.Merge(pluginPipelineResult.StageMetrics);

        variantTotalStopwatch.Stop();
        variantStageMetrics.AddDuration("variantTotal", variantTotalStopwatch.ElapsedMilliseconds);

        var searchSnippetsEnabled = TemplateCapabilitiesResolver.SupportsSearchSnippets(
            TemplateCapabilitiesResolver.SearchTemplatePath, ctx.LayoutsDir);

        return new BuildReportPipeline().Execute(new BuildReportPipelineContext(
            Config: config, Language: config.Site.Language, OutputDir: outputDir,
            BaseUrl: baseUrl, SearchSnippetsEnabled: searchSnippetsEnabled,
            BodyStore: bodyStore, Routed: routed,
            DerivedRouted: pluginContext.DerivedRouted,
            DerivedRoutes: pluginContext.DerivedRoutes,
            SeoIndex: seoResult.SeoIndex.Entries,
            SeoModels: seoResult.SeoIndex.Models,
            PluginExecutions: pluginContext.PluginExecutions.ToList(),
            RenderedCount: renderedCount, SkippedCount: skippedCount,
            RenderReasons: new Dictionary<string, int>(renderReasons, StringComparer.OrdinalIgnoreCase),
            StageMetrics: variantStageMetrics.Snapshot(),
            Logger: logger,
            DefaultLanguage: ctx.DefaultLanguage));
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
