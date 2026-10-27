using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Routing.Tests;

[Trait("Category", "Unit")]
public sealed class RouteSecurityValidatorTests
{
    [Theory]
    [InlineData("/post/hello/")]
    [InlineData("/")]
    [InlineData("/tags/dotnet/")]
    public void ValidUrls_Pass(string url)
    {
        var ex = Record.Exception(() => RouteSecurityValidator.ValidateInternalUrl(url));
        Assert.Null(ex);
    }

    [Fact]
    public void EmptyUrl_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl(""));
        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, ex.Code);
    }

    [Fact]
    public void ProtocolRelative_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl("//evil.com/"));
        Assert.Contains("protocol-relative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AbsoluteExternalUrl_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl("https://evil.com/"));
        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, ex.Code);
    }

    [Theory]
    [InlineData("posts/example")]
    [InlineData("./posts/example")]
    [InlineData("\\posts\\example")]
    public void RelativeUrlWithoutLeadingSlash_Throws(string url)
    {
        var ex = Assert.Throws<ConfigException>(() =>
            RouteSecurityValidator.ValidateInternalUrl(url, "test-route"));

        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, ex.Code);
        Assert.Contains("start with '/'", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test-route", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ControlCharacters_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl("/post/\0hidden"));
        Assert.Contains("control", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/%2e%2e/secret/")]
    [InlineData("/a/%2e%2e/b/")]
    public void EncodedTraversal_Throws(string url)
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl(url));

        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, ex.Code);
    }

    [Theory]
    [InlineData("/a%2fb/")]
    [InlineData("/a%2Fb/")]
    public void EncodedSlash_Throws(string url)
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl(url));

        Assert.Equal(DiagnosticCode.RouteEncodedSlashInPath, ex.Code);
    }

    [Fact]
    public void InvalidPercentEscape_IsTreatedAsLiteralSegment()
    {
        // Uri.UnescapeDataString does not throw for malformed escapes on .NET 10;
        // the validator therefore keeps '%zz' as a literal segment.
        Assert.Equal("bad%zz/index.html", RoutePathBuilder.BuildOutputPathFromUrl("/bad%zz/"));
    }

    [Theory]
    [InlineData("/%00/")]
    [InlineData("/%0a/")]
    public void EncodedControlCharacter_Throws(string url)
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl(url));

        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, ex.Code);
    }

    [Theory]
    [InlineData("safe/index.html")]
    [InlineData("blog/2024/post.html")]
    public void ValidateOutputPath_SafePaths_Pass(string path)
    {
        RouteSecurityValidator.ValidateOutputPath(path);
    }

    [Theory]
    [InlineData("/absolute.html", DiagnosticCode.RouteUnsafeOutputPath)]
    [InlineData("C:/absolute.html", DiagnosticCode.RouteUnsafeOutputPath)]
    [InlineData("C:\\absolute.html", DiagnosticCode.RouteUnsafeOutputPath)]
    [InlineData("", DiagnosticCode.RouteUnsafeOutputPath)]
    [InlineData("a//b.html", DiagnosticCode.RouteUnsafeOutputPath)]
    [InlineData("../escape/index.html", DiagnosticCode.RouteUnsafeOutputPath)]
    [InlineData("a/../../b.html", DiagnosticCode.RouteUnsafeOutputPath)]
    [InlineData("CON/index.html", DiagnosticCode.RouteReservedWindowsPath)]
    [InlineData("bad\0path.html", DiagnosticCode.RouteUnsafeOutputPath)]
    public void ValidateOutputPath_UnsafePaths_Throw(string path, DiagnosticCode code)
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateOutputPath(path));

        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void ValidateWithSource_IncludesSourceInMessage()
    {
        var ex = Assert.Throws<ConfigException>(() =>
            RouteSecurityValidator.ValidateInternalUrl("/../bad", "test-collection"));
        Assert.Contains("test-collection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/docs/?view=all")]
    [InlineData("/docs/#intro")]
    public void Validate_RouteWithQueryOrFragment_Throws(string route)
    {
        Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl(route));
    }

    [Theory]
    [InlineData("/con./")]
    [InlineData("/name. /")]
    [InlineData("/CON.foo.bar/")]
    public void Validate_WindowsAlias_ThrowsOnEveryPlatform(string route)
    {
        Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl(route));
    }
}
