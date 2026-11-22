using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Audit;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PublishOutputInclusionTests
{
    private static readonly SeoIndexEntry Post = Entry("/blog/post/", "https://x.test/blog/post/");
    private static readonly SeoIndexEntry Other = Entry("/blog/other/", "https://x.test/blog/other/");

    [Fact]
    public void OutputContains_MatchesRouteNeedlesCaseInsensitively()
    {
        var inclusion = new PublishOutputInclusion([Post, Other], projectionResults: null);
        const string sitemap = "<urlset><LOC>HTTPS://X.TEST/BLOG/POST/</LOC></urlset>";

        Assert.True(inclusion.OutputContains(sitemap, PublishOutputInclusion.SitemapLocNeedle(Post.Canonical)));
        Assert.True(inclusion.OutputContains(sitemap, Post.Route.Url));
        Assert.False(inclusion.OutputContains(sitemap, PublishOutputInclusion.SitemapLocNeedle(Other.Canonical)));
    }

    [Fact]
    public void OutputContains_ReturnsFalseForNullTextOrNeedle()
    {
        var inclusion = new PublishOutputInclusion([Post], projectionResults: null);

        Assert.False(inclusion.OutputContains(null, Post.Route.Url));
        Assert.False(inclusion.OutputContains("/blog/post/", null));
    }

    [Fact]
    public void OutputContains_AnswersUnknownNeedlesDirectly()
    {
        var inclusion = new PublishOutputInclusion([Post], projectionResults: null);

        Assert.True(inclusion.OutputContains("see /ABOUT/", "/about/"));
        Assert.False(inclusion.OutputContains("see /blog/post/", "/about/"));
    }

    [Fact]
    public void Constructor_IgnoresEntriesWithNullUrls()
    {
        var broken = new SeoIndexEntry(null!, null!, null, false, DateTimeOffset.UnixEpoch, null, null);
        var inclusion = new PublishOutputInclusion([broken, Post], projectionResults: null);

        Assert.True(inclusion.OutputContains("/blog/post/", Post.Route.Url));
    }

    [Fact]
    public void TryGetProjected_PrefersProjectionAndKeepsLastOutputPerUrl()
    {
        var projections = new[]
        {
            new PublishProjectionResult(PublishRepresentationRegistry.Html,
            [
                new PublishRepresentationOutput("sitemap", "/blog/post/", "sitemap.xml", Exists: true, Indexable: true),
                new PublishRepresentationOutput("SITEMAP", "/BLOG/POST/", "sitemap.xml", Exists: false, Indexable: true),
                new PublishRepresentationOutput("search", " ", "search.json", Exists: true, Indexable: true),
            ]),
        };
        var inclusion = new PublishOutputInclusion([Post], projections);

        Assert.True(inclusion.TryGetProjected("sitemap", Post, out var sitemapIncluded));
        Assert.False(sitemapIncluded);
        Assert.False(inclusion.TryGetProjected("search", Post, out _));
        Assert.False(inclusion.TryGetProjected("feed", Post, out _));
    }

    [Fact]
    public void TryGetProjected_FallsBackToCanonical()
    {
        var projections = new[]
        {
            new PublishProjectionResult(PublishRepresentationRegistry.Html,
                [new PublishRepresentationOutput("feed", Post.Canonical, "rss.xml", Exists: true, Indexable: true)]),
        };
        var inclusion = new PublishOutputInclusion([Post], projections);

        Assert.True(inclusion.TryGetProjected("feed", Post, out var included));
        Assert.True(included);
    }

    private static SeoIndexEntry Entry(string url, string canonical)
        => new(new RouteInfo(url, url.Trim('/') + "/index.html", "post"), canonical, null, true, DateTimeOffset.UnixEpoch, null, null);
}
