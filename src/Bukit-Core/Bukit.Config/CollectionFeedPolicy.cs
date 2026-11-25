namespace Bukit.Config;

/// <summary>
/// Decides which collections get feed files of their own. <c>output.rss</c> puts a collection's posts in the site
/// feed; <c>output.feed</c> writes the collection's own feed. Left unset, a collection gets its own feed only when
/// two or more collections are in the site feed, so a single-collection site does not repeat its site feed.
/// </summary>
internal static class CollectionFeedPolicy
{
    internal static bool WritesOwnFeed(IReadOnlyDictionary<string, CollectionConfig> collections, CollectionConfig collection)
    {
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(collection);
        return collection.Output.Feed ?? (collection.Output.Rss && collections.Values.Count(x => x.Output.Rss) >= 2);
    }
}
