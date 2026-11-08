using Xunit;
using Bukit.Engine.Plugins.BuiltIn;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyTermValueNormalizerTests
{
    [Theory]
    [InlineData("%e4%b8%80%e5%b8%a6%e4%b8%80%e8%b7%af", "一带一路")]
    [InlineData("Istana%20Budaya", "Istana Budaya")]
    public void Normalize_PercentEncodedUtf8_IsDecoded(string raw, string expected)
    {
        var result = TaxonomyTermValueNormalizer.Normalize(raw, out var decoded);

        Assert.True(decoded);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("一带一路")]
    [InlineData("100% 原创")]
    [InlineData("50%off")]
    [InlineData("%ff%fe")]
    public void Normalize_NotValidEncoding_IsKept(string raw)
    {
        var result = TaxonomyTermValueNormalizer.Normalize(raw, out var decoded);

        Assert.False(decoded);
        Assert.Equal(raw, result);
    }

    [Fact]
    public void Normalize_CollapsesWhitespaceIncludingFullWidth()
    {
        var result = TaxonomyTermValueNormalizer.Normalize("  Istana　　 Budaya \t", out var decoded);

        Assert.False(decoded);
        Assert.Equal("Istana Budaya", result);
    }

    [Fact]
    public void Normalize_DecodedValue_SlugifiesToReadableSlug()
    {
        var value = TaxonomyTermValueNormalizer.Normalize("%e4%b8%80", out _);

        Assert.DoesNotContain("e4b8", Bukit.Shared.SlugHelper.Slugify(value), StringComparison.Ordinal);
    }
}
