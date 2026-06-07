using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Incremental;
using Bukit.Engine.Output;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

public sealed class SiteEngine
{
    private readonly ILogger _logger;
    private readonly IContentProviderFactory _contentProviderFactory;
    private readonly ISearchIndexBuilder _searchIndexBuilder;
    private readonly Func<string, ITemplateRenderer>? _rendererFactory;
    private readonly VariantBuildPipeline _variantPipeline;

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
        _variantPipeline = new VariantBuildPipeline();
    }

    // -- public API --

    public Task<BuildResult> BuildAsync(AppConfig config, string rootDir, ConfigOverrides overrides, CancellationToken cancellationToken = default)
    {
        var pipeline = new BuildPipeline(BuildCoreAsync);
        return pipeline.ExecuteAsync(new BuildPipelineContext(config, rootDir, overrides), cancellationToken);
    }

    public static IReadOnlyList<RouteInfo> GetListRoutes(
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        ThemeTemplateResolver? templateResolver = null)
        => SeoAlternatesService.GetListRoutes(collections, templateResolver);

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
        var documents = contentResult.Documents;
        var contentGraph = contentResult.ContentGraph ?? CanonicalContentGraph.Empty;
        var bodyStore = contentResult.BodyStore;
        var bodyCacheMetrics = contentResult.BodyCacheMetrics;

        var templateHashCache = new DirectoryHashCache();

        var languages = I18nOutputMerger.GetLanguages(effectiveConfig.Site);
        if (languages.Count == 0)
        {
            var siteLanguage = effectiveConfig.Site.Language;
            var result = await BuildSingleLanguageVariantAsync(
                effectiveConfig, rootDir, overrides, documents, contentGraph, bodyStore, plan.OutputDir,
                plan.LayoutsDir, plan.AssetsDir, plan.StaticDir, plan.MediaCacheDir,
                plan.ParentLayoutsDir, plan.ParentAssetsDir, plan.ParentStaticDir, plan.UserLayoutsDir,
                templateHashCache, cancellationToken);

            _logger.Info($"event=build.variant.done language={effectiveConfig.Site.Language} baseUrl={BuildPathUtils.NormalizeBaseUrl(effectiveConfig.Site.BaseUrl)}");
            MetricsWriter.WriteIfRequested(rootDir, overrides.MetricsPath, effectiveConfig, plan.OutputDir, documents.Count, new[] { result }, contentResult.BodyCacheMetrics);
            plan.Stopwatch.Stop();
            var singleLanguageBuildResult = BuildResultFactory.Create(effectiveConfig, rootDir, plan.OutputDir, overrides, plan.StartedAt, DateTimeOffset.UtcNow, plan.Stopwatch.ElapsedMilliseconds, new[] { result }, contentResult.SchemaErrors);
            BuildReporter.WriteIfEnabled(effectiveConfig, rootDir, plan.OutputDir, singleLanguageBuildResult, new[] { result }, _logger);
            WriteOutputMarker(plan.OutputDir);
            BuildRecoveryTracker.MarkCompleted(plan.OutputDir);
            return singleLanguageBuildResult;
        }

        return await BuildMultiLanguageAsync(
            effectiveConfig, rootDir, overrides, documents, contentGraph, bodyStore, plan.OutputDir,
            plan.LayoutsDir, plan.AssetsDir, plan.StaticDir, plan.MediaCacheDir,
            plan.ParentLayoutsDir, plan.ParentAssetsDir, plan.ParentStaticDir, plan.UserLayoutsDir,
            templateHashCache, languages, plan.StartedAt, plan.Stopwatch,
            bodyCacheMetrics,
            contentResult.SchemaErrors, cancellationToken);
    }

    private async Task<BuildVariantResult> BuildSingleLanguageVariantAsync(
        AppConfig config, string rootDir, ConfigOverrides overrides,
        IReadOnlyList<ContentDocument> documents, CanonicalContentGraph contentGraph, IContentBodyStore bodyStore,
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
            config, rootDir, overrides, documents, contentGraph, bodyStore, outputDir, baseUrl,
            layoutsDir, assetsDir, staticDir, mediaCacheDir,
            SeoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal),
            RootBaseUrl: null, ManifestSuffix: null, DefaultLanguage: null,
            ParentLayoutsDir: parentLayoutsDir, ParentAssetsDir: parentAssetsDir, ParentStaticDir: parentStaticDir,
            UserLayoutsDir: userLayoutsDir);
        return await BuildVariantAsync(variantCtx, templateHashCache, cancellationToken);
    }

    private async Task<BuildResult> BuildMultiLanguageAsync(
        AppConfig config, string rootDir, ConfigOverrides overrides,
        IReadOnlyList<ContentDocument> documents, CanonicalContentGraph contentGraph, IContentBodyStore bodyStore,
        string outputDir, string layoutsDir, string assetsDir, string staticDir,
        string mediaCacheDir,
        string? parentLayoutsDir, string? parentAssetsDir, string? parentStaticDir,
        string? userLayoutsDir,
        DirectoryHashCache templateHashCache,
        IReadOnlyList<string> languages,
        DateTimeOffset buildStartedAt, Stopwatch buildStopwatch,
        BodyCacheMetrics? bodyCacheMetrics,
        IReadOnlyList<ContentValidationIssue> schemaErrors,
        CancellationToken cancellationToken)
    {
        var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(config.Site, languages);
        var rootBaseUrl = BuildPathUtils.NormalizeBaseUrl(config.Site.BaseUrl);
        var templateResolver = new ThemeTemplateResolver(ThemeBootstrapper.Bootstrap(config, rootDir, _logger).Manifest);
        var seoAlternates = SeoAlternatesService.BuildSeoAlternates(config, documents, languages, defaultLanguage, rootBaseUrl, templateResolver);
        var results = new BuildVariantResult[languages.Count];

        var languageJobs = Math.Max(1, config.Build.LanguageJobs);
        var processorCount = Environment.ProcessorCount;
        if (languageJobs > processorCount)
        {
            languageJobs = processorCount;
        }

        _logger.Info($"event=build.i18n.start languages={languages.Count} concurrent_jobs={languageJobs}");
        await Parallel.ForEachAsync(
            languages.Select((lang, i) => (lang, i)),
            new ParallelOptions { MaxDegreeOfParallelism = languageJobs, CancellationToken = cancellationToken },
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

                var variantDocuments = I18nOutputMerger.FilterDocumentsByLanguage(documents, lang, defaultLanguage);
                var variantOutputDir = Path.Combine(outputDir, lang);
                variantLogger.Info($"event=build.variant.start language={lang} baseUrl={baseUrl} outputDir={variantOutputDir}");
                var variantCtx = new BuildVariantContext(
                    variantConfig, rootDir, overrides, variantDocuments, contentGraph, bodyStore, variantOutputDir, baseUrl,
                    layoutsDir, assetsDir, staticDir, mediaCacheDir,
                    SeoAlternates: seoAlternates,
                    RootBaseUrl: rootBaseUrl, ManifestSuffix: lang, DefaultLanguage: defaultLanguage,
                    ParentLayoutsDir: parentLayoutsDir, ParentAssetsDir: parentAssetsDir, ParentStaticDir: parentStaticDir,
                    UserLayoutsDir: userLayoutsDir);
                results[i] = await BuildVariantAsync(variantCtx, templateHashCache, ct, variantLogger);
                variantLogger.Info($"event=build.variant.done language={lang} baseUrl={baseUrl} outputDir={variantOutputDir}");
            });

        var variantResults = results.Where(r => r is not null).ToList();

        var projectionResults = I18nOutputMerger.GenerateRootOutputs(config, outputDir, rootBaseUrl, variantResults, _logger, _searchIndexBuilder);
        SeoAuditReportWriter.WriteMerged(config, outputDir, variantResults, _logger, projectionResults);
        _logger.Info("event=build.done");
        MetricsWriter.WriteIfRequested(rootDir, overrides.MetricsPath, config, outputDir, documents.Count, variantResults, bodyCacheMetrics);
        buildStopwatch.Stop();
        var buildResult = BuildResultFactory.Create(config, rootDir, outputDir, overrides, buildStartedAt, DateTimeOffset.UtcNow, buildStopwatch.ElapsedMilliseconds, variantResults, schemaErrors);
        BuildReporter.WriteIfEnabled(config, rootDir, outputDir, buildResult, variantResults, _logger);
        WriteOutputMarker(outputDir);
        BuildRecoveryTracker.MarkCompleted(outputDir);
        return buildResult;
    }

    private static IReadOnlyList<ContentDocument> FilterDocumentsByLanguage(
        IReadOnlyList<ContentDocument> documents,
        string language,
        string defaultLanguage)
    {
        return documents
            .Where(document =>
            {
                var docLanguage = document.Record.Presentation.Language;
                if (string.IsNullOrWhiteSpace(docLanguage) ||
                    string.Equals(docLanguage, "und", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase);
                }

                return string.Equals(docLanguage, language, StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
    }

    private async Task<BuildVariantResult> BuildVariantAsync(
        BuildVariantContext ctx,
        DirectoryHashCache templateHashCache,
        CancellationToken cancellationToken,
        ILogger? variantLogger = null)
    {
        return await _variantPipeline.ExecuteAsync(
            ctx, templateHashCache, _rendererFactory, variantLogger ?? _logger, cancellationToken);
    }

    private const string OutputMarkerFileName = ".bukit-output-marker";

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
