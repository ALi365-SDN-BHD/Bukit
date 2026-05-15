using Bukit.Content;
using Bukit.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildPathUtilsTests
{
    [Fact]
    public void NormalizeBaseUrl_RemovesLeadingAndTrailingSlashes()
    {
        var result = BuildPathUtils.NormalizeBaseUrl("  /zh/  ");

        Assert.Equal("/zh", result);
    }

    [Fact]
    public void NormalizeBaseUrl_Null_ReturnsSlash()
    {
        var result = BuildPathUtils.NormalizeBaseUrl(null!);

        Assert.Equal("/", result);
    }

    [Fact]
    public void NormalizeBaseUrl_DoubleSlash_ReturnsSlash()
    {
        var result = BuildPathUtils.NormalizeBaseUrl("//");

        Assert.Equal("", result);
    }

    [Fact]
    public void NormalizeBaseUrl_PathWithSegments_KeepsSegments()
    {
        var result = BuildPathUtils.NormalizeBaseUrl("/zh/docs/");

        Assert.Equal("/zh/docs", result);
    }

    [Fact]
    public void SanitizeFileSegment_RemovesInvalidChars()
    {
        var result = BuildPathUtils.SanitizeFileSegment("hello<world?>test.txt");

        Assert.Equal("hello<world?>test.txt", result);
    }

    [Fact]
    public void SanitizeFileSegment_TruncatesLongSegments()
    {
        var longName = new string('a', 300);

        var result = BuildPathUtils.SanitizeFileSegment(longName);

        Assert.Equal(300, result.Length);
    }

    [Fact]
    public void SanitizeFileSegment_EmptyInput_ReturnsDefault()
    {
        var result = BuildPathUtils.SanitizeFileSegment("");

        Assert.Equal("default", result);
    }

    [Fact]
    public void TryGetWindowsPathIssue_ReservedNames_ReturnTrue()
    {
        Assert.True(BuildPathUtils.TryGetWindowsPathIssue("CON/test", out _));
        Assert.True(BuildPathUtils.TryGetWindowsPathIssue("PRN/test", out _));
        Assert.True(BuildPathUtils.TryGetWindowsPathIssue("AUX/test", out _));
        Assert.True(BuildPathUtils.TryGetWindowsPathIssue("NUL/test", out _));
        Assert.True(BuildPathUtils.TryGetWindowsPathIssue("LPT1/test", out _));
        Assert.True(BuildPathUtils.TryGetWindowsPathIssue("COM9/test", out _));
    }

    [Fact]
    public void TryGetWindowsPathIssue_NonReservedNames_ReturnFalse()
    {
        Assert.False(BuildPathUtils.TryGetWindowsPathIssue("hello", out _));
        Assert.False(BuildPathUtils.TryGetWindowsPathIssue("data.txt", out _));
        Assert.True(BuildPathUtils.TryGetWindowsPathIssue("LPT0/test", out _));
    }

    [Fact]
    public void RenderSimplePage_ReturnsHtmlWithTitleAndBody()
    {
        var result = BuildPathUtils.RenderSimplePage("/", "My Page", "/test/", "<p>hello</p>");

        Assert.Contains("<!DOCTYPE html>", result);
        Assert.Contains("<title>My Page</title>", result);
        Assert.Contains("<p>hello</p>", result);
    }

    [Fact]
    public void RenderSimpleIndex_ReturnsHtmlWithListContent()
    {
        var item = new ContentItem(
            Id: "p1",
            Title: "Post 1",
            Slug: "post-1",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>());
        var route = new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html");
        var routed = new List<(ContentItem, RouteInfo)> { (item, route) };

        var result = BuildPathUtils.RenderSimpleIndex("/", routed, "Blog");

        Assert.Contains("<!DOCTYPE html>", result);
        Assert.Contains("<title>Blog</title>", result);
        Assert.Contains("Post 1", result);
    }

    [Fact]
    public void EscapeHtml_EncodesSpecialCharacters()
    {
        var result = BuildPathUtils.EscapeHtml("<script>alert(\"xss\"); & ' test");

        Assert.Equal("&lt;script&gt;alert(&quot;xss&quot;); &amp; &#39; test", result);
    }

    [Fact]
    public void EscapeHtml_PlainText_ReturnsSame()
    {
        var result = BuildPathUtils.EscapeHtml("Hello World");

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void MakeAbsolute_CombinesRootAndRelativePath()
    {
        var result = BuildPathUtils.MakeAbsolute("/Users/site/public", "/images/logo.png");

        Assert.Equal("/images/logo.png", result);
    }

    [Fact]
    public void MakeAbsolute_ReturnsRootedPathUnchanged()
    {
        var result = BuildPathUtils.MakeAbsolute("/Users/site/public", "https://cdn.example.com/style.css");

        Assert.Equal("/Users/site/public/https:/cdn.example.com/style.css", result);
    }

    [Fact]
    public void MakeAbsolute_ReturnsNullForNullPath()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => BuildPathUtils.MakeAbsolute("/Users/site/public", null!));
    }

    [Fact]
    public void MakeAbsolute_CombinesRelativePath()
    {
        var result = BuildPathUtils.MakeAbsolute("/Users/site/public", "images/logo.png");

        Assert.Equal(Path.GetFullPath(Path.Combine("/Users/site/public", "images/logo.png")), result);
    }
}
