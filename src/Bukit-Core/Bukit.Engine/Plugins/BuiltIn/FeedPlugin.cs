using Bukit.Config;

namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;
public sealed class FeedPlugin : IBukitPlugin, IAfterBuildPlugin
{
    public string Name => "feed";
    public string Version => "3.0.0";

    public void AfterBuild(BuildContext context)
    {
        var siteUrl = context.Config.Site.Url;
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            return;
        }

        var feedConfig = context.Config.Site.Feed;
        var formats = ParseFormats(feedConfig.Formats);
        var limit = feedConfig.Limit > 0 ? feedConfig.Limit : 20;
        var collections = context.Config.Site.Collections;

        var isMergedMode = context.Config.Site.Languages is { Count: > 0 }
            && SiteModeResolver.ResolveFeedMode(context.Config.Site) == "merged";

        if (isMergedMode)
        {
            return;
        }

        var allPosts = RssGenerator.CollectAllPosts(
            collections, context.RoutedDocuments, context.BodyStore, context.ContentGraph, context.SeoIndex, siteUrl, context.BaseUrl);

        GenerateGlobalFeeds(context.OutputDir, siteUrl, context.BaseUrl, context.Config.Site.Title,
            allPosts, formats, limit, feedConfig.Path, context.Config.Site.Description);

        GeneratePerCollectionFeeds(context, collections, siteUrl, allPosts, formats, limit, feedConfig.Path);
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
        string fallbackPath)
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
            var title = cfg.Output.FeedTitle ?? context.Config.Site.Title;
            var description = cfg.Output.FeedDescription ?? context.Config.Site.Description;

            var collectionPosts = RssGenerator.CollectPosts(
                collections, context.RoutedDocuments, context.BodyStore, context.ContentGraph, context.SeoIndex, siteUrl, context.BaseUrl, key);

            foreach (var format in formats)
            {
                switch (format)
                {
                    case "rss":
                        RssGenerator.GenerateMerged(context.OutputDir, siteUrl, context.BaseUrl, title,
                            collectionPosts, limit, description);
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
