using Bukit.Routing;
using Xunit;

namespace Bukit.Routing.Tests;

public sealed class RoutePathBuilderTests
{
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
        Assert.EndsWith(expected.Replace('/', System.IO.Path.DirectorySeparatorChar), result);
    }

    [Theory]
    [InlineData("none", "hello-world/index.html")]
    [InlineData("url", "hello-world/index.html")]
    public void OutputPathEncoding_PreservesAsciiChars(string encoding, string expected)
    {
        var url = "/hello-world/";
        var result = RoutePathBuilder.BuildOutputPathFromUrl(url, encoding);
        Assert.EndsWith(expected.Replace('/', System.IO.Path.DirectorySeparatorChar), result);
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
