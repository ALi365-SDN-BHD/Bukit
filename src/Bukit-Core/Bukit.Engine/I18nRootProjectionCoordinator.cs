using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record I18nRootProjectionWriterContext(
    AppConfig Config,
    string OutputDir,
    string RootBaseUrl,
    IReadOnlyList<BuildVariantResult> Results,
    ILogger Logger,
    ISearchIndexBuilder? SearchIndexBuilder = null)
{
    internal Dictionary<string, IReadOnlySet<string>> PublishedUrls { get; } = new(StringComparer.Ordinal);
    internal I18nMergedVariantState? MergedState { get; set; }
}

internal static class I18nRootProjectionCoordinator
{
    private static readonly I18nRootProjectionWriterRegistry WriterRegistry =
        I18nRootProjectionWriterRegistry.CreateDefault();

    internal static IReadOnlyList<PublishProjectionResult> GenerateRootOutputs(
        AppConfig config,
        string outputDir,
        string rootBaseUrl,
        IReadOnlyList<BuildVariantResult> results,
        ILogger logger,
        ISearchIndexBuilder? searchIndexBuilder = null,
        bool skipRootRobots = false)
    {
        var projectionContext = new PublishProjectionContext(
            Config: config,
            OutputDir: outputDir,
            ContentGraph: CanonicalContentGraph.Empty,
            SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
            SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
            RoutedDocuments: Array.Empty<RoutedContentDocument>(),
            BaseUrl: rootBaseUrl,
            Logger: logger,
            VariantResults: results,
            DerivedDocuments: Array.Empty<RoutedContentDocument>());
        var writerContext = ToWriterContext(projectionContext, searchIndexBuilder);
        var representations = PublishRepresentationRegistry.AggregateRepresentations();
        if (skipRootRobots)
        {
            representations = representations
                .Where(representation => representation.Kind != "robots")
                .ToArray();
        }

        var projections = WriterRegistry.BuildPlan(representations)
            .Select(entry => Project(writerContext, entry.Representation, entry.Writer))
            .ToArray();
        I18nRootRedirectWriter.WriteIfRequested(config, outputDir);
        return projections;
    }

    internal static PublishProjectionResult Project(
        PublishProjectionContext context,
        PublishRepresentation representation)
    {
        var writerContext = ToWriterContext(context);
        var writer = WriterRegistry.Resolve(representation);
        if (writer is not null)
        {
            writer.Write(writerContext, representation);
        }

        return new PublishProjectionResult(
            representation,
            I18nRootProjectionInventory.BuildOutputs(
                writerContext.OutputDir,
                InventoryRepresentation(writerContext.Config, representation),
                writerContext.Results,
                writerContext.PublishedUrls.GetValueOrDefault(representation.Kind),
                LanguagePath(writerContext.Config, representation),
                IsIndexMode(writerContext.Config, representation)));
    }

    private static PublishProjectionResult Project(
        I18nRootProjectionWriterContext context,
        PublishRepresentation representation,
        II18nRootProjectionWriter writer)
    {
        writer.Write(context, representation);
        return new PublishProjectionResult(
            representation,
            I18nRootProjectionInventory.BuildOutputs(context.OutputDir, InventoryRepresentation(context.Config, representation),
                context.Results, context.PublishedUrls.GetValueOrDefault(representation.Kind),
                LanguagePath(context.Config, representation), IsIndexMode(context.Config, representation)));
    }

    private static PublishRepresentation InventoryRepresentation(AppConfig config, PublishRepresentation representation)
        => representation with
        {
            Path = representation.Kind switch
            {
                "atom" => config.Site.Feed.Path + "/atom.xml",
                "jsonfeed" => config.Site.Feed.Path + "/feed.json",
                "search" when SiteModeResolver.ResolveSearchMode(config.Site) == "index" => "search.index.json",
                _ => representation.Path
            }
        };

    // Unless a root writer merges them, each language writes its own search index, sitemap and feeds
    // under its output prefix (the root files are then that language's copy, a search index index or a
    // sitemap index), so each language's routes are checked against its own file.
    private static Func<string, string>? LanguagePath(AppConfig config, PublishRepresentation representation)
    {
        var site = config.Site;
        var layout = I18nOutputLayout.For(site);
        string Prefixed(string language, string path)
            => I18nOutputLayout.CombineRelative(layout.RelativeOutputPrefix(language), path.TrimStart('/', '\\'));

        return representation.Kind switch
        {
            "search" when SiteModeResolver.ResolveSearchMode(site) is "split" or "index"
                => language => Prefixed(language, layout.SearchRelativePath(language)),
            "sitemap" when SitemapMode(site) is "split" or "index"
                => language => Prefixed(language, layout.SitemapRelativePath(language)),
            "feed" or "atom" or "jsonfeed" when SiteModeResolver.ResolveFeedMode(site) == "split"
                => language => Prefixed(language, InventoryRepresentation(config, representation).Path),
            _ => null
        };
    }

    private static bool IsIndexMode(AppConfig config, PublishRepresentation representation)
        => representation.Kind switch
        {
            "search" => SiteModeResolver.ResolveSearchMode(config.Site) == "index",
            "sitemap" => SitemapMode(config.Site) == "index",
            _ => false
        };

    private static string SitemapMode(SiteConfig site) => (site.SitemapMode ?? "split").Trim().ToLowerInvariant();

    private static I18nRootProjectionWriterContext ToWriterContext(
        PublishProjectionContext context,
        ISearchIndexBuilder? searchIndexBuilder = null)
        => new(
            context.Config,
            context.OutputDir,
            context.BaseUrl,
            context.VariantResults ?? Array.Empty<BuildVariantResult>(),
            context.Logger ?? new ConsoleLogger(LogLevel.Error),
            searchIndexBuilder);
}
