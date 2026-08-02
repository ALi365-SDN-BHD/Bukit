using Bukit.Cli.Commands.SeoInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoObservationUrlNormalizerTests
{
    [Theory]
    [InlineData(
        "HTTPS://EXAMPLE.COM:443/a/?utm_source=x&b=2#part",
        "https://example.com/a/?b=2")]
    [InlineData(
        "https://example.com/%E9%A9%AC%E6%9D%A5%E8%A5%BF%E4%BA%9A",
        "https://example.com/%E9%A9%AC%E6%9D%A5%E8%A5%BF%E4%BA%9A/")]
    public void Normalize_ProducesCanonicalObservationKey(string input, string expected)
    {
        var result = SeoObservationUrlNormalizer.Normalize(input, Options());

        Assert.True(result.Success);
        Assert.Equal(expected, result.NormalizedUrl);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Normalize_ProducesHostIndependentMatchKey()
    {
        var result = SeoObservationUrlNormalizer.Normalize(
            "https://example.com/articles/item?lang=zh",
            Options());

        Assert.True(result.Success);
        Assert.Equal("https://example.com/articles/item/?lang=zh", result.NormalizedUrl);
        Assert.Equal("/articles/item/?lang=zh", result.MatchKey);
    }

    [Fact]
    public void Normalize_PreservesSortsAndSafelyEncodesRetainedQueryPairs()
    {
        var result = SeoObservationUrlNormalizer.Normalize(
            "https://example.com/search?z=%2F&a=two%20words&a=one&UTM_Source=campaign",
            Options());

        Assert.True(result.Success);
        Assert.Equal("https://example.com/search/?a=one&a=two%20words&z=%2F", result.NormalizedUrl);
        Assert.Equal("/search/?a=one&a=two%20words&z=%2F", result.MatchKey);
    }

    [Fact]
    public void Normalize_NormalizesSchemeUnicodeHostAndDefaultPort()
    {
        var options = new SeoObservationUrlOptions(
            "bücher.example",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));

        var result = SeoObservationUrlNormalizer.Normalize(
            "HTTPS://BÜCHER.EXAMPLE:443/path",
            options);

        Assert.True(result.Success);
        Assert.Equal("https://xn--bcher-kva.example/path/", result.NormalizedUrl);
        Assert.Equal("/path/", result.MatchKey);
    }

    [Fact]
    public void Normalize_AcceptsOnlyDeclaredHostAliases()
    {
        var options = new SeoObservationUrlOptions(
            "example.com",
            new HashSet<string>(["www.example.com"], StringComparer.Ordinal),
            new HashSet<string>(["utm_source"], StringComparer.Ordinal));

        var accepted = SeoObservationUrlNormalizer.Normalize("https://WWW.EXAMPLE.COM/page", options);
        var rejected = SeoObservationUrlNormalizer.Normalize("https://other.example.com/page", options);

        Assert.True(accepted.Success);
        Assert.Equal("https://www.example.com/page/", accepted.NormalizedUrl);
        Assert.False(rejected.Success);
        Assert.Equal("host_not_allowed", rejected.ErrorCode);
    }

    [Fact]
    public void Normalize_DoesNotAddTrailingSlashToExtensionPath()
    {
        var result = SeoObservationUrlNormalizer.Normalize("https://example.com/feed.xml", Options());

        Assert.True(result.Success);
        Assert.Equal("https://example.com/feed.xml", result.NormalizedUrl);
        Assert.Equal("/feed.xml", result.MatchKey);
    }

    [Theory]
    [InlineData("not-an-absolute-url", "invalid_url")]
    [InlineData("ftp://example.com/file", "unsupported_scheme")]
    [InlineData("https://user:secret@example.com/page", "credentials_not_allowed")]
    [InlineData("https://undeclared.example/page", "host_not_allowed")]
    public void Normalize_RejectsInvalidOrUntrustedObservedUrls(string input, string expectedErrorCode)
    {
        var result = SeoObservationUrlNormalizer.Normalize(input, Options());

        Assert.False(result.Success);
        Assert.Null(result.NormalizedUrl);
        Assert.Null(result.MatchKey);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    private static SeoObservationUrlOptions Options()
        => new(
            "example.com",
            new HashSet<string>(["www.example.com"], StringComparer.Ordinal),
            new HashSet<string>(["utm_source"], StringComparer.Ordinal));
}
