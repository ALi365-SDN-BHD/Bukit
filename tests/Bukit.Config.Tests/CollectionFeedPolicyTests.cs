using Xunit;

namespace Bukit.Config.Tests;

public sealed class CollectionFeedPolicyTests
{
    private static CollectionConfig Collection(bool rss = true, bool? feed = null)
        => new() { Permalink = "/{slug}/", Template = "pages/post.html", Output = new() { Rss = rss, Feed = feed } };

    [Fact]
    public void SingleSiteFeedCollection_GetsNoOwnFeed_SoTheSiteFeedIsNotRepeated()
    {
        var posts = Collection();
        var collections = new Dictionary<string, CollectionConfig> { ["posts"] = posts, ["pages"] = Collection(rss: false) };

        Assert.False(CollectionFeedPolicy.WritesOwnFeed(collections, posts));
        Assert.False(CollectionFeedPolicy.WritesOwnFeed(collections, collections["pages"]));
    }

    [Fact]
    public void TwoOrMoreSiteFeedCollections_EachGetTheirOwnFeed()
    {
        var collections = new Dictionary<string, CollectionConfig> { ["posts"] = Collection(), ["notes"] = Collection(), ["pages"] = Collection(rss: false) };

        Assert.True(CollectionFeedPolicy.WritesOwnFeed(collections, collections["posts"]));
        Assert.True(CollectionFeedPolicy.WritesOwnFeed(collections, collections["notes"]));
        Assert.False(CollectionFeedPolicy.WritesOwnFeed(collections, collections["pages"]));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    public void ExplicitFeed_OverridesTheDefault(bool rss, bool feed, bool expected)
    {
        var collection = Collection(rss, feed);
        var collections = new Dictionary<string, CollectionConfig> { ["posts"] = collection, ["notes"] = Collection() };

        Assert.Equal(expected, CollectionFeedPolicy.WritesOwnFeed(collections, collection));
    }
}
