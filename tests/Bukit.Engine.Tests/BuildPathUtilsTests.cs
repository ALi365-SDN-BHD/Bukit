using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
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

        Assert.Equal("hello_world__test.txt", result);
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
        var document = ContentDocument.Create(
            id: "p1",
            title: "Post 1",
            slug: "post-1",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));
        var route = new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html");
        var routed = new[] { new RoutedContentDocument(document, route) };

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
    public void MakeAbsolute_TreatsUrlLikeRelativePath_ForLegacyBehavior()
    {
        var root = Path.GetFullPath("/Users/site/public");
        var result = BuildPathUtils.MakeAbsolute(root, "https://cdn.example.com/style.css");

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "https:/cdn.example.com/style.css")), result);
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

    [Fact]
    public void MakeAbsolute_Should_ThrowConfigException_When_AbsolutePathOutsideRoot_AndEnforceWithinRoot()
    {
        var ex = Assert.Throws<ConfigException>(() =>
            BuildPathUtils.MakeAbsolute("/Users/site/public", "/etc/passwd", enforceWithinRoot: true));
        Assert.Contains("path outside root boundary", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MakeAbsolute_Should_ThrowConfigException_When_RelativeEscapesRoot()
    {
        var ex = Assert.Throws<ConfigException>(() =>
            BuildPathUtils.MakeAbsolute("/Users/site/public", "../../../etc/passwd", enforceWithinRoot: true));
        Assert.Contains("path outside root boundary", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MakeAbsolute_Should_AcceptPath_When_WithinRoot()
    {
        var result = BuildPathUtils.MakeAbsolute("/Users/site/public", "themes/foo/layouts", enforceWithinRoot: true);
        Assert.Equal(Path.GetFullPath(Path.Combine("/Users/site/public", "themes/foo/layouts")), result);
    }

    [Fact]
    public void MakeAbsolute_Should_PreserveOldBehavior_When_DefaultOverload()
    {
        var rooted = BuildPathUtils.MakeAbsolute("/Users/site/public", "/etc/passwd");
        Assert.Equal("/etc/passwd", rooted);

        var relative = BuildPathUtils.MakeAbsolute("/Users/site/public", "../../../etc/passwd");
        Assert.Equal(Path.GetFullPath("/etc/passwd"), relative);

        var safe = BuildPathUtils.MakeAbsolute("/Users/site/public", "images/logo.png");
        Assert.Equal(Path.GetFullPath("/Users/site/public/images/logo.png"), safe);
    }
}
