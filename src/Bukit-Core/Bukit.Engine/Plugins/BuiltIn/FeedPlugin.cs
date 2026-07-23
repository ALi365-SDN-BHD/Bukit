using Bukit.Config;

namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;
internal sealed class FeedPlugin : IBukitPlugin, IAfterBuildPlugin
{
    private readonly AppConfig _config;

    internal FeedPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "feed";
    public string Version => "3.0.0";

    public void AfterBuild(BuildContext context)
    {
        var siteUrl = _config.Site.Url;
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            return;
        }

        var feedConfig = _config.Site.Feed;
        var formats = ParseFormats(feedConfig.Formats);
        var limit = feedConfig.Limit > 0 ? feedConfig.Limit : 20;
        var collections = _config.Site.Collections;

        var isMergedMode = _config.Site.Languages is { Count: > 0 }
            && SiteModeResolver.ResolveFeedMode(_config.Site) == "merged";

        if (isMergedMode)
        {
            return;
        }

        var allPosts = RssGenerator.CollectAllPosts(
            collections, context.RoutedDocuments, context.BodyStore, context.ContentGraph, context.SeoIndex, siteUrl, context.BaseUrl);

        GenerateGlobalFeeds(context.OutputDir, siteUrl, context.BaseUrl, _config.Site.Title,
            allPosts, formats, limit, feedConfig.Path, _config.Site.Description);

        GeneratePerCollectionFeeds(
            context,
            collections,
            siteUrl,
            allPosts,
            formats,
            limit,
            feedConfig.Path,
            _config.Site.Title,
            _config.Site.Description);
    }

    private static void GenerateGlobalFeeds(
        string outputDir,
        string siteUrl,
        string baseUrl,
        string siteTitle,
        List<RssGenerator.Post> allPosts,
        IReadOnlySet<string> formats,
        int limit,
        string feedPath,
        string? siteDescription)
    {
        foreach (var format in formats)
        {
            switch (format)
            {
                case "rss":
                    RssGenerator.GenerateMerged(outputDir, siteUrl, baseUrl, siteTitle, allPosts, limit, siteDescription);
                    break;
                case "atom":
                    AtomFeedGenerator.Generate(outputDir, siteUrl, baseUrl, siteTitle, allPosts,
                        $"{feedPath}/atom.xml", limit, siteDescription);
                    break;
                case "json":
                    JsonFeedGenerator.Generate(outputDir, siteUrl, baseUrl, siteTitle, allPosts,
                        $"{feedPath}/feed.json", limit, siteDescription);
                    break;
            }
        }
    }

    private static void GeneratePerCollectionFeeds(
        BuildContext context,
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        string siteUrl,
        List<RssGenerator.Post> allPosts,
        IReadOnlySet<string> formats,
        int limit,
        string fallbackPath,
        string siteTitle,
        string? siteDescription)
    {
        if (collections is null || collections.Count == 0)
        {
            return;
        }

        foreach (var (key, cfg) in collections)
        {
            if (!cfg.Output.Rss)
            {
                continue;
            }

            var feedBase = cfg.Output.FeedPath ?? $"{fallbackPath}/{key}";
            var title = cfg.Output.FeedTitle ?? siteTitle;
            var description = cfg.Output.FeedDescription ?? siteDescription;

            var collectionPosts = RssGenerator.CollectPosts(
                collections, context.RoutedDocuments, context.BodyStore, context.ContentGraph, context.SeoIndex, siteUrl, context.BaseUrl, key);

            foreach (var format in formats)
            {
                switch (format)
                {
                    case "rss":
                        RssGenerator.GenerateAtPath(context.OutputDir, siteUrl, context.BaseUrl, title,
                            collectionPosts, feedBase, limit, description);
                        break;
                    case "atom":
                        AtomFeedGenerator.Generate(context.OutputDir, siteUrl, context.BaseUrl, title,
                            collectionPosts, $"{feedBase}/atom.xml", limit, description);
                        break;
                    case "json":
                        JsonFeedGenerator.Generate(context.OutputDir, siteUrl, context.BaseUrl, title,
                            collectionPosts, $"{feedBase}/feed.json", limit, description);
                        break;
                }
            }
        }
    }

    private static HashSet<string> ParseFormats(IReadOnlyList<string>? formats)
    {
        if (formats is null || formats.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "rss" };
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in formats)
        {
            var normalized = f.Trim().ToLowerInvariant();
            if (normalized is "rss" or "atom" or "json")
            {
                set.Add(normalized);
            }
        }

        return set.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "rss" }
            : set;
    }
}
