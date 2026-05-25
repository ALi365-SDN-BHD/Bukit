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
    private readonly Func<string, ITemplateRenderer>? _rendererFactory;
    private readonly SiteBuildOrchestrator _orchestrator;

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
        _orchestrator = new SiteBuildOrchestrator(logger, contentProviderFactory, searchIndexBuilder, rendererFactory);
    }

    public Task<BuildResult> BuildAsync(AppConfig config, string rootDir, ConfigOverrides overrides, CancellationToken cancellationToken = default)
    {
        var pipeline = new BuildPipeline(_orchestrator.BuildCoreAsync);
        return pipeline.ExecuteAsync(new BuildPipelineContext(config, rootDir, overrides), cancellationToken);
    }

    public static IReadOnlyList<RouteInfo> GetListRoutes(IReadOnlyDictionary<string, CollectionConfig>? collections)
        => SeoAlternatesService.GetListRoutes(collections);

    public async Task BuildAsync(IContentProvider provider, BuildOptions options, CancellationToken cancellationToken = default)
    {
        var fullOutputDir = Path.GetFullPath(options.OutputDir);
        var rootDir = Path.GetDirectoryName(fullOutputDir) ?? ".";
        var outputDirName = Path.GetFileName(fullOutputDir);

        var config = BuildOptionsToConfig(options, outputDirName);
        var overrides = new ConfigOverrides { IsCI = options.IsCI, Incremental = false };
        var factory = new FixedContentProviderFactory(provider, _contentProviderFactory);
        var orchestrator = new SiteBuildOrchestrator(_logger, factory, _searchIndexBuilder, _rendererFactory);
        var pipeline = new BuildPipeline(orchestrator.BuildCoreAsync);
        await pipeline.ExecuteAsync(new BuildPipelineContext(config, rootDir, overrides), cancellationToken);
    }

    private static AppConfig BuildOptionsToConfig(BuildOptions options, string outputDirName)
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = options.SiteTitle,
                Title = options.SiteTitle,
                Language = "en",
                BaseUrl = options.BaseUrl,
                Url = options.SiteUrl,
                OutputPathEncoding = options.OutputPathEncoding,
                Seo = new SeoConfig { Enabled = false }
            },
            Build = new BuildConfig
            {
                Output = outputDirName,
                Clean = options.Clean
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
    }

    private sealed class FixedContentProviderFactory : IContentProviderFactory
    {
        private readonly IContentProvider _provider;
        private readonly IContentProviderFactory _fallback;

        internal FixedContentProviderFactory(IContentProvider provider, IContentProviderFactory fallback)
        {
            _provider = provider;
            _fallback = fallback;
        }

        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
            => _provider;

        public Task<ContentLoadResult> LocalizeContentImagesAsync(
            ContentLoadResult result,
            MediaConfig media,
            string rootDir,
            string cacheDir,
            ILogger logger,
            CancellationToken cancellationToken)
            => _fallback.LocalizeContentImagesAsync(result, media, rootDir, cacheDir, logger, cancellationToken);
    }

    // Retained for test backward compatibility (reflection-based tests)
    // These delegates are intentionally kept on SiteEngine to avoid breaking
    // SiteEngineHelperTests which use BindingFlags to discover private methods.

    private static IReadOnlyDictionary<string, RouteGenerator.CollectionRouteRule>? BuildCollectionRules(SiteConfig site)
    {
        return SiteBuildOrchestrator.BuildCollectionRules(site);
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

    private static IReadOnlyList<SeoAlternateModel>? GetSeoAlternates(
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> alternates,
        string key)
    {
        return SeoPipeline.GetSeoAlternates(alternates, key);
    }
}
