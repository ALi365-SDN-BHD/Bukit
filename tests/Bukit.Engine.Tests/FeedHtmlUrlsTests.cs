using Xunit;

namespace Bukit.Engine.Tests;

public sealed class FeedHtmlUrlsTests
{
    [Fact]
    public void AbsolutizeRootRelative_PrefixesOnlyRootRelativeLinkAndMediaUrls()
    {
        const string html = "<p><a href=\"/post/\">x</a> <a href='//cdn.test/a'>c</a> <a href=\"https://other.test/b\">o</a> " +
            "<a href=\"rel/\">r</a> <img src=\"/img/a.png\" srcset=\"/img/a-1.png 1x,/img/a-2.png 2x\"> " +
            "<img src=\"b.png\" srcset=\"b-1.png 1x,b-2.png 2x\"><video poster=\"/v.jpg\"></video></p>";

        var result = FeedHtmlUrls.AbsolutizeRootRelative(html, "https://user@example.com:8443/site/");

        Assert.Equal(
            "<p><a href=\"https://example.com:8443/post/\">x</a> <a href='//cdn.test/a'>c</a> <a href=\"https://other.test/b\">o</a> " +
            "<a href=\"rel/\">r</a> <img src=\"https://example.com:8443/img/a.png\" " +
            "srcset=\"https://example.com:8443/img/a-1.png 1x,https://example.com:8443/img/a-2.png 2x\"> " +
            "<img src=\"b.png\" srcset=\"b-1.png 1x,b-2.png 2x\"><video poster=\"https://example.com:8443/v.jpg\"></video></p>",
            result);
    }

    [Fact]
    public void AbsolutizeRootRelative_LeavesTextAttributeTextAndDataUrisUnchanged()
    {
        const string html = "<pre><code>&lt;img src='/logo.png'&gt;</code></pre>" +
            "<img alt=\"x src='/y'\" src=\"/z.png\"> <img srcset=\"data:image/png;base64,/9j/AA 1x, /a.png 2x\">";

        var result = FeedHtmlUrls.AbsolutizeRootRelative(html, "https://example.com");

        Assert.Equal(
            "<pre><code>&lt;img src='/logo.png'&gt;</code></pre>" +
            "<img alt=\"x src='/y'\" src=\"https://example.com/z.png\"> <img srcset=\"data:image/png;base64,/9j/AA 1x, /a.png 2x\">",
            result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/relative")]
    [InlineData("ftp://example.com")]
    public void AbsolutizeRootRelative_WithoutHttpOrigin_ReturnsHtmlUnchanged(string siteUrl)
    {
        const string html = "<a href=\"/post/\">x</a>";

        Assert.Same(html, FeedHtmlUrls.AbsolutizeRootRelative(html, siteUrl));
    }

    [Fact]
    public void AbsolutizeContent_RewritesEachPostAndKeepsPostsWithoutHtml()
    {
        var publishedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        RssGenerator.Post[] posts =
        [
            new("A", "https://example.com/a/", publishedAt, null, null, "<a href=\"/b/\">b</a>"),
            new("B", "https://example.com/b/", publishedAt, null, null, null)
        ];

        var result = FeedHtmlUrls.AbsolutizeContent(posts, "https://example.com");

        Assert.Equal("<a href=\"https://example.com/b/\">b</a>", result[0].ContentHtml);
        Assert.Same(posts[1], result[1]);
    }
}
