using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class UrlRedactorTests
{
    [Fact]
    public void Redact_NullOrEmpty_ReturnsSameValue()
    {
        Assert.Null(Bukit.Shared.UrlRedactor.Redact(null!));
        Assert.Equal("", Bukit.Shared.UrlRedactor.Redact(""));
        Assert.Equal("   ", Bukit.Shared.UrlRedactor.Redact("   "));
    }

    [Fact]
    public void Redact_SimpleUrl_KeepsOnlySchemeAndHost()
    {
        Assert.Equal(
            "https://example.com/<redacted-path>",
            Bukit.Shared.UrlRedactor.Redact("https://example.com/page"));
    }

    [Fact]
    public void Redact_RemovesQueryStringAndPath()
    {
        var result = Bukit.Shared.UrlRedactor.Redact("https://example.com/page?token=secret&user=admin");
        Assert.Equal("https://example.com/<redacted-path>", result);
    }

    [Fact]
    public void Redact_RemovesFragmentAndPath()
    {
        var result = Bukit.Shared.UrlRedactor.Redact("https://example.com/page#section");
        Assert.Equal("https://example.com/<redacted-path>", result);
    }

    [Fact]
    public void Redact_RemovesBothQueryAndFragment()
    {
        var result = Bukit.Shared.UrlRedactor.Redact("https://example.com/page?x=1#section");
        Assert.Equal("https://example.com/<redacted-path>", result);
    }

    [Fact]
    public void Redact_UserInfoIsNeverLogged()
    {
        var result = Bukit.Shared.UrlRedactor.Redact("https://user:pass@example.test/a.png");
        Assert.Equal("https://example.test/<redacted-path>", result);
    }
}
