using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Routing.Tests;

public sealed class RoutePathBuilderTests
{
    [Theory]
    [InlineData("/%e8%81%94%e7%b3%bb/", "/联系/")]
    [InlineData("/%E8%81%94/a b/", "/联/a b/")]
    [InlineData("/联系/", "/联系/")]
    [InlineData("/news/", "/news/")]
    [InlineData("/100%/", "/100%/")]
    [InlineData("/a%2Fb/", "/a%2Fb/")]
    [InlineData("/%2e%2e/x/", "/%2e%2e/x/")]
    [InlineData("/a%5Cb/", "/a%5Cb/")]
    [InlineData("/%2541/", "/%2541/")]
    [InlineData("/%0A/", "/%0A/")]
    [InlineData("/%ff%fe/", "/%ff%fe/")]
    [InlineData("/😀/%e8%81%94/", "/😀/联/")]
    [InlineData("/a%E2%80%AEb/", "/a%E2%80%AEb/")]
    [InlineData("/a%E2%80%8Bb/", "/a%E2%80%8Bb/")]
    [InlineData("/a%C2%A0b/", "/a%C2%A0b/")]
    [InlineData("/a%E3%80%80b/", "/a%E3%80%80b/")]
    [InlineData("/a%EF%BB%BFb/", "/a%EF%BB%BFb/")]
    public void DecodePercentEncodedUrl_DecodesOnlySafeUtf8Escapes(string input, string expected)
    {
        Assert.Equal(expected, RoutePathBuilder.DecodePercentEncodedUrl(input));
    }

    [Theory]
    [InlineData("post", "/post/")]
    [InlineData("/post", "/post/")]
    [InlineData("post/", "/post/")]
    [InlineData("/post/", "/post/")]
    [InlineData("/", "/")]
    [InlineData("  /hello  ", "/hello/")]
    public void NormalizeUrl_EnsuresLeadingAndTrailingSlash(string input, string expected)
    {
        Assert.Equal(expected, RoutePathBuilder.NormalizeUrl(input));
    }

    // Explicit URLs are files only for common web extensions; any other dot stays a directory.
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("/page.html", "/page.html")]
    [InlineData("/blog/post.html", "/blog/post.html")]
    [InlineData("/page.HTM", "/page.HTM")]
    [InlineData("/feed.xml", "/feed.xml")]
    [InlineData("/data/index.json", "/data/index.json")]
    [InlineData("/robots.txt", "/robots.txt")]
    [InlineData("/page.html/", "/page.html/")]
    [InlineData("/docs/v1.0", "/docs/v1.0/")]
    [InlineData("/blog/release.2026", "/blog/release.2026/")]
    [InlineData("/tags/node.js", "/tags/node.js/")]
    [InlineData("/download/app.php", "/download/app.php/")]
    public void NormalizeUrl_ExplicitUrl_OnlyWebExtensionsAreFiles(string input, string expected)
    {
        Assert.Equal(expected, RoutePathBuilder.NormalizeUrl(input));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("/docs/v1.0", "docs/v1.0/index.html")]
    [InlineData("/blog/release.2026", "blog/release.2026/index.html")]
    [InlineData("/feed.xml", "feed.xml")]
    [InlineData("/page.htm", "page.htm")]
    public void BuildOutputPathFromUrl_ExplicitUrl_OnlyWebExtensionsAreFiles(string url, string expected)
    {
        Assert.Equal(expected, RoutePathBuilder.BuildOutputPathFromUrl(url));
    }

    [Fact]
    public void NormalizeListRoute_DottedRouteWithoutSlash_RemainsDirectory()
    {
        Assert.Equal("/tags/release.2026/", RoutePathBuilder.NormalizeListRoute("/tags/release.2026"));
    }

    [Fact]
    public void NormalizeListRoute_EmptyIsRoot()
    {
        Assert.Equal("/", RoutePathBuilder.NormalizeListRoute(""));
    }

    [Theory]
    [InlineData("/post/", "post/index.html")]
    [InlineData("/", "index.html")]
    [InlineData("/blog/2024/my-post/", "blog/2024/my-post/index.html")]
    public void BuildOutputPathFromUrl_DefaultEncoding(string url, string expected)
    {
        var result = RoutePathBuilder.BuildOutputPathFromUrl(url);
        Assert.EndsWith(expected, result);
    }

    [Theory]
    [InlineData("/docs/?view=all")]
    [InlineData("/docs/#intro")]
    public void BuildOutputPathFromUrl_QueryOrFragment_Throws(string url)
    {
        var exception = Assert.Throws<ConfigException>(() => RoutePathBuilder.BuildOutputPathFromUrl(url));

        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, exception.Code);
    }

    [Fact]
    public void BuildOutputPathFromUrl_RelativeUrlWithoutLeadingSlash_Throws()
    {
        var exception = Assert.Throws<ConfigException>(() =>
            RoutePathBuilder.BuildOutputPathFromUrl("posts/example"));

        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, exception.Code);
    }

    [Theory]
    [InlineData("none", "hello-world/index.html")]
    [InlineData("url", "hello-world/index.html")]
    public void OutputPathEncoding_PreservesAsciiChars(string encoding, string expected)
    {
        var url = "/hello-world/";
        var result = RoutePathBuilder.BuildOutputPathFromUrl(url, encoding);
        Assert.EndsWith(expected, result);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("/Hello World/", "slug", "hello-world/index.html")]
    [InlineData("/a b/", "urlencode", "a%20b/index.html")]
    [InlineData("/page", "none", "page/index.html")]
    [InlineData("/page.html", "none", "page.html")]
    [InlineData("/blog/post.html", "none", "blog/post.html")]
    [InlineData("/Hello World.html", "slug", "hello-world.html")]
    [InlineData("/a b.html", "urlencode", "a%20b.html")]
    [InlineData("/", "none", "index.html")]
    public void BuildOutputPathFromUrl_AppliesEncodingAndDirectoryRules(string url, string encoding, string expected)
    {
        Assert.Equal(expected, RoutePathBuilder.BuildOutputPathFromUrl(url, encoding));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("File Name.pdf", "sanitize", "File-Name.pdf")]
    [InlineData("bad<>name?.txt", "sanitize", "badname.txt")]
    [InlineData("page.html", "none", "page.html")]
    [InlineData("Hello World/index.html", "slug", "hello-world/index.html")]
    public void NormalizeOutputPath_AppliesEncoding(string outputPath, string encoding, string expected)
    {
        Assert.Equal(expected, RoutePathBuilder.NormalizeOutputPath(outputPath, encoding));
    }

    [Fact]
    public void NormalizeOutputPath_StripsLeadingSeparators()
    {
        var result = RoutePathBuilder.NormalizeOutputPath("///post/index.html");
        Assert.DoesNotContain("///", result);
        Assert.EndsWith("index.html", result);
    }

    [Fact]
    public void NormalizeOutputPath_NullInput_ReturnsEmpty()
    {
        var result = RoutePathBuilder.NormalizeOutputPath(null!);
        Assert.Empty(result);
    }
}
