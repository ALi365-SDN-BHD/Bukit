using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Routing.Tests;

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

    [Fact]
    public void ControlCharacters_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl("/post/\0hidden"));
        Assert.Contains("control", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateWithSource_IncludesSourceInMessage()
    {
        var ex = Assert.Throws<ConfigException>(() =>
            RouteSecurityValidator.ValidateInternalUrl("/../bad", "test-collection"));
        Assert.Contains("test-collection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
