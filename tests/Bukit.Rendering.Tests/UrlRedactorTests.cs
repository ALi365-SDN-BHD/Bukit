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
    public void Redact_SimpleUrl_ReturnsUnchanged()
    {
        Assert.Equal("https://example.com/page", Bukit.Shared.UrlRedactor.Redact("https://example.com/page"));
    }

    [Fact]
    public void Redact_RemovesQueryString()
    {
        var result = Bukit.Shared.UrlRedactor.Redact("https://example.com/page?token=secret&user=admin");
        Assert.Equal("https://example.com/page?[REDACTED]", result);
    }

    [Fact]
    public void Redact_RemovesFragment()
    {
        var result = Bukit.Shared.UrlRedactor.Redact("https://example.com/page#section");
        Assert.Equal("https://example.com/page?[REDACTED]", result);
    }

    [Fact]
    public void Redact_RemovesBothQueryAndFragment_PreferFirst()
    {
        var result = Bukit.Shared.UrlRedactor.Redact("https://example.com/page?x=1#section");
        Assert.Equal("https://example.com/page?[REDACTED]", result);
    }

    [Fact]
    public void Redact_FragmentBeforeQuery_PreferFragment()
    {
        var result = Bukit.Shared.UrlRedactor.Redact("https://example.com/page#section?x=1");
        Assert.Equal("https://example.com/page?[REDACTED]", result);
    }
}
