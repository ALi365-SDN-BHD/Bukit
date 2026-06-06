using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SitemapPolicyTests
{
    [Fact]
    public void ResolveLastModified_PrefersUpdateTime_DateTimeOffset()
    {
        var publishAt = new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero);
        var update = new DateTimeOffset(2024, 02, 03, 4, 5, 6, TimeSpan.Zero);

        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "s",
            publishAt: publishAt,
            contentHtml: "",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["update_time"] = new("date", update)
            });

        var dt = SitemapPolicy.ResolveLastModified(item);
        Assert.Equal(update, dt);
    }

    [Fact]
    public void ResolveLastModified_PrefersUpdateTime_ParsesText()
    {
        var publishAt = new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero);
        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "s",
            publishAt: publishAt,
            contentHtml: "",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["update_time"] = new("text", "2024-05-06T07:08:09Z")
            });

        var dt = SitemapPolicy.ResolveLastModified(item);
        Assert.Equal(new DateTimeOffset(2024, 05, 06, 7, 8, 9, TimeSpan.Zero), dt);
    }

    [Fact]
    public void ShouldExcludeFromSitemap_RobotsNoindex()
    {
        var html = """
                   <html>
                   <head>
                     <meta name="robots" content="noindex,nofollow">
                   </head>
                   </html>
                   """;
        Assert.True(SitemapPolicy.ShouldExcludeFromSitemap(html));
    }

    [Fact]
    public void ShouldExcludeFromSitemap_RobotsIndex()
    {
        var html = """
                   <html>
                   <head>
                     <meta name="robots" content="index,follow">
                   </head>
                   </html>
                   """;
        Assert.False(SitemapPolicy.ShouldExcludeFromSitemap(html));
    }

    [Fact]
    public void ShouldExcludeFromSitemap_SitemapExclude()
    {
        var html = """
                   <html>
                   <head>
                     <meta name="sitemap" content="exclude">
                   </head>
                   </html>
                   """;
        Assert.True(SitemapPolicy.ShouldExcludeFromSitemap(html));
    }
}

