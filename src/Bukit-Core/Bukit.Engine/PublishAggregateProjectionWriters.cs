using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class RssProjectionWriter
{
    internal static void WriteRss(PublishProjectionContext context)
    {
        if (!ShouldWriteFeed(context, "rss"))
        {
            return;
        }

        var posts = CollectFeedPosts(context);
        var limit = ResolveFeedLimit(context);
        RssGenerator.GenerateMerged(context.OutputDir, context.Config.Site.Url!, context.BaseUrl, context.Config.Site.Title, posts, limit, context.Config.Site.Description);
        WriteCollectionRssFeeds(context, limit);
    }

    internal static void WriteAtom(PublishProjectionContext context)
    {
        if (!ShouldWriteFeed(context, "atom"))
        {
            return;
        }

        var posts = CollectFeedPosts(context);
        var limit = ResolveFeedLimit(context);
        AtomFeedGenerator.Generate(context.OutputDir, context.Config.Site.Url!, context.BaseUrl, context.Config.Site.Title, posts, $"{context.Config.Site.Feed.Path}/atom.xml", limit, context.Config.Site.Description);
        WriteCollectionAtomFeeds(context, limit);
    }

    internal static void WriteJsonFeed(PublishProjectionContext context)
    {
        if (!ShouldWriteFeed(context, "json"))
        {
            return;
        }

        var posts = CollectFeedPosts(context);
        var limit = ResolveFeedLimit(context);
        JsonFeedGenerator.Generate(context.OutputDir, context.Config.Site.Url!, context.BaseUrl, context.Config.Site.Title, posts, $"{context.Config.Site.Feed.Path}/feed.json", limit, context.Config.Site.Description);
        WriteCollectionJsonFeeds(context, limit);
    }

    internal static List<RssGenerator.Post> CollectFeedPosts(PublishProjectionContext context)
        => RssGenerator.CollectAllPosts(
            context.Config.Site.Collections,
            context.RoutedDocuments,
            context.BodyStore ?? NullContentBodyStore.Instance,
            context.ContentGraph,
            context.SeoIndex,
            context.Config.Site.Url ?? string.Empty,
            context.BaseUrl);

    private static bool ShouldWriteFeed(PublishProjectionContext context, string format)
        => !string.IsNullOrWhiteSpace(context.Config.Site.Url) &&
           context.Config.Site.Feed.Formats.Any(x => string.Equals(x, format, StringComparison.OrdinalIgnoreCase));

    private static int ResolveFeedLimit(PublishProjectionContext context)
        => context.Config.Site.Feed.Limit > 0 ? context.Config.Site.Feed.Limit : 20;

    private static void WriteCollectionRssFeeds(PublishProjectionContext context, int limit)
    {
        foreach (var (_, _, posts, feedBase, title, description) in ResolveCollectionFeeds(context))
        {
            RssGenerator.GenerateAtPath(context.OutputDir, context.Config.Site.Url!, context.BaseUrl, title, posts, feedBase, limit, description);
        }
    }

    private static void WriteCollectionAtomFeeds(PublishProjectionContext context, int limit)
    {
        foreach (var (_, _, posts, feedBase, title, description) in ResolveCollectionFeeds(context))
        {
            AtomFeedGenerator.Generate(context.OutputDir, context.Config.Site.Url!, context.BaseUrl, title, posts, $"{feedBase}/atom.xml", limit, description);
        }
    }

    private static void WriteCollectionJsonFeeds(PublishProjectionContext context, int limit)
    {
        foreach (var (_, _, posts, feedBase, title, description) in ResolveCollectionFeeds(context))
        {
            JsonFeedGenerator.Generate(context.OutputDir, context.Config.Site.Url!, context.BaseUrl, title, posts, $"{feedBase}/feed.json", limit, description);
        }
    }

    private static IEnumerable<(string Key, CollectionConfig Collection, List<RssGenerator.Post> Posts, string FeedBase, string Title, string? Description)> ResolveCollectionFeeds(PublishProjectionContext context)
    {
        if (context.Config.Site.Collections is not { Count: > 0 } collections)
        {
            yield break;
        }

        foreach (var (key, collection) in collections)
        {
            if (!collection.Output.Rss)
            {
                continue;
            }

            var posts = RssGenerator.CollectPosts(
                collections,
                context.RoutedDocuments,
                context.BodyStore ?? NullContentBodyStore.Instance,
                context.ContentGraph,
                context.SeoIndex,
                context.Config.Site.Url ?? string.Empty,
                context.BaseUrl,
                key);
            var feedBase = NormalizeCollectionFeedBase(collection.Output.FeedPath ?? $"{context.Config.Site.Feed.Path}/{key}");
            var title = collection.Output.FeedTitle ?? context.Config.Site.Title;
            var description = collection.Output.FeedDescription ?? context.Config.Site.Description;
            yield return (key, collection, posts, feedBase, title, description);
        }
    }

    private static string NormalizeCollectionFeedBase(string feedBase)
    {
        var normalized = (feedBase ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            throw new ConfigException("Collection feedPath must be a safe relative path.", DiagnosticCode.ConfigInvalidValue);
        }

        return normalized;
    }
}

internal static class SearchProjectionWriter
{
    internal static void WriteSearchIndex(PublishProjectionContext context)
    {
        SearchIndexBuilder.GenerateSingleSearchIndex(
            context.OutputDir,
            context.BaseUrl,
            context.Config.Site.SearchIncludeDerived,
            context.SearchSnippetsEnabled,
            context.Config.Site.Search.MaxContentLength,
            context.RoutedDocuments,
            context.DerivedDocuments,
            context.SeoIndex,
            context.BodyStore ?? NullContentBodyStore.Instance,
            context.ListRouteGraph,
            context.SeoModels);
        SearchIndexPlugin.WriteSearchUi(context.Config, context.OutputDir);
    }
}

internal static class LlmsAggregateProjectionWriter
{
    internal static void WriteLlmsTxt(PublishProjectionContext context)
    {
        if (!context.Config.Site.Seo.Geo.Enabled || !context.Config.Site.Seo.Geo.LlmsTxt)
        {
            return;
        }

        LlmsTxtPlugin.WriteLlmsTxt(
            context.Config,
            context.OutputDir,
            context.BaseUrl,
            context.RoutedDocuments,
            context.DerivedDocuments,
            context.SeoIndex,
            context.SeoModels,
            context.Config.Site.Seo.Geo);
    }

    internal static void WriteLlmsFullTxt(PublishProjectionContext context)
    {
        if (!context.Config.Site.Seo.Geo.Enabled || !context.Config.Site.Seo.Geo.LlmsFullTxt)
        {
            return;
        }

        LlmsTxtPlugin.WriteLlmsFullTxt(
            context.Config,
            context.OutputDir,
            context.BaseUrl,
            context.RoutedDocuments,
            context.DerivedDocuments,
            context.ContentGraph,
            context.SeoIndex,
            context.BodyStore ?? NullContentBodyStore.Instance);
    }
}

internal static class RobotsAggregateProjectionWriter
{
    internal static void WriteRobotsTxt(PublishProjectionContext context)
        => RobotsTxtWriter.WriteIfRequested(context.Config, context.OutputDir, context.BaseUrl, context.SeoIndex);
}
