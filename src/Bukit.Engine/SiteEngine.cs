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
