using Xunit;

namespace Bukit.Shared.Tests;

public sealed class UrlRedactorTests
{
    [Fact]
    public void Redact_Null_ReturnsNull()
    {
        Assert.Null(UrlRedactor.Redact(null!));
    }

    [Fact]
    public void Redact_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, UrlRedactor.Redact(string.Empty));
    }

    [Fact]
    public void Redact_Whitespace_ReturnsWhitespace()
    {
        Assert.Equal("   ", UrlRedactor.Redact("   "));
    }

    [Fact]
    public void Redact_SimpleUrl_ReturnsUnchanged()
    {
        Assert.Equal("https://example.com/page", UrlRedactor.Redact("https://example.com/page"));
    }

    [Fact]
    public void Redact_UrlWithQueryString_StripsQuery()
    {
        var result = UrlRedactor.Redact("https://example.com/page?token=secret&user=admin");
        Assert.Equal("https://example.com/page?[REDACTED]", result);
    }

    [Fact]
    public void Redact_UrlWithFragment_StripsFragment()
    {
        var result = UrlRedactor.Redact("https://example.com/page#section");
        Assert.Equal("https://example.com/page?[REDACTED]", result);
    }

    [Fact]
    public void Redact_UrlWithBothQueryAndFragment_QueryFirst_PrefersQuery()
    {
        var result = UrlRedactor.Redact("https://example.com/page?x=1#section");
        Assert.Equal("https://example.com/page?[REDACTED]", result);
    }

    [Fact]
    public void Redact_UrlWithBothQueryAndFragment_FragmentFirst_PrefersFragment()
    {
        var result = UrlRedactor.Redact("https://example.com/page#section?x=1");
        Assert.Equal("https://example.com/page?[REDACTED]", result);
    }

    [Fact]
    public void Redact_UrlWithOnlyQuestionMark_IsRedacted()
    {
        var result = UrlRedactor.Redact("https://example.com/page?");
        Assert.Equal("https://example.com/page?[REDACTED]", result);
    }

    [Fact]
    public void Redact_PlainStringWithoutQueryOrFragment_ReturnsSame()
    {
        var result = UrlRedactor.Redact("just-a-string");
        Assert.Equal("just-a-string", result);
    }
}
