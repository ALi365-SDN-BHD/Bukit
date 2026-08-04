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
    public void Redact_SimpleUrl_KeepsOnlySchemeAndHost()
    {
        Assert.Equal("https://example.com/<redacted-path>", UrlRedactor.Redact("https://example.com/page"));
    }

    [Fact]
    public void Redact_UrlWithQueryString_RemovesQueryAndPath()
    {
        Assert.Equal(
            "https://example.com/<redacted-path>",
            UrlRedactor.Redact("https://example.com/page?token=secret&user=admin"));
    }

    [Fact]
    public void Redact_UrlWithFragment_RemovesFragmentAndPath()
    {
        Assert.Equal(
            "https://example.com/<redacted-path>",
            UrlRedactor.Redact("https://example.com/page#section"));
    }

    [Fact]
    public void Redact_NonDefaultPort_PreservesPort()
    {
        Assert.Equal(
            "https://example.com:8443/<redacted-path>",
            UrlRedactor.Redact("https://example.com:8443/media/a.png"));
    }

    [Fact]
    public void Redact_UnparseableValue_ReturnsFixedMarker()
    {
        Assert.Equal("<redacted-url>", UrlRedactor.Redact("not a url"));
    }

    [Theory]
    [InlineData("https://user:pass@example.test/secret/token.png?key=x")]
    [InlineData("https://token@api.example.test/v1/items/42#anchor")]
    public void Redact_RemovesUserInfoPathQueryAndFragment(string value)
    {
        var result = UrlRedactor.Redact(value);

        Assert.DoesNotContain("user", result, StringComparison.Ordinal);
        Assert.DoesNotContain("pass", result, StringComparison.Ordinal);
        Assert.DoesNotContain("token", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/v1/", result, StringComparison.Ordinal);
        Assert.Equal(
            new Uri(value).Scheme + "://" + new Uri(value).Host + "/<redacted-path>",
            result);
    }
}
