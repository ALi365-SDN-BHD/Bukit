using Bukit.Engine.Plugins.BuiltIn.ImageProcessing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ImageVariantPlanTests
{
    [Fact]
    public void GeneratedWidths_ExcludesOriginalWidth()
    {
        Assert.Equal([480, 768], ImageVariantPlan.GeneratedWidths([480, 768, 1200], 1000));
    }

    [Fact]
    public void GeneratedWidths_WhenSourceIsAtOrBelowConfiguredSizes_IsEmpty()
    {
        Assert.Empty(ImageVariantPlan.GeneratedWidths([480, 768], 480));
        Assert.Empty(ImageVariantPlan.GeneratedWidths([480, 768], 320));
    }

    [Fact]
    public void GeneratedWidths_DeduplicatesAndOrders()
    {
        Assert.Equal([480, 768], ImageVariantPlan.GeneratedWidths([768, 480, 480, 768], 1000));
    }

    [Fact]
    public void CandidateWidths_IncludesOriginalWidthAndDeduplicates()
    {
        Assert.Equal([480, 768, 1000], ImageVariantPlan.CandidateWidths([768, 480, 480, 768], 1000));
        Assert.Equal([400], ImageVariantPlan.CandidateWidths([480, 768], 400));
    }

    [Fact]
    public void VariantFileName_PreservesDirectoryExtensionAndCase()
    {
        Assert.Equal("/tmp/assets/hero-480w.jpg", ImageVariantPlan.VariantFileName("/tmp/assets/hero.jpg", 480, ".jpg"));
        Assert.Equal("/assets/hero-480w.webp", ImageVariantPlan.VariantFileName("/assets/hero.jpg", 480, ".webp"));
        Assert.Equal("hero-480w.JPG", ImageVariantPlan.VariantFileName("hero.jpg", 480, ".JPG"));
        Assert.Equal("/assets/hero.v1-480w.jpg", ImageVariantPlan.VariantFileName("/assets/hero.v1.jpg", 480, ".jpg"));
    }

    [Theory]
    [InlineData("/assets/hero.v1.jpg", 480, "/assets/hero.v1-480w.jpg")]
    [InlineData("/assets/hero.v1.jpg?fit=cover/1", 480, "/assets/hero.v1-480w.jpg?fit=cover/1")]
    [InlineData("https://site.test/assets/hero.jpg#large/1", 768, "https://site.test/assets/hero-768w.jpg#large/1")]
    public void VariantUrl_PreservesUrlSeparatorsAndSuffix(string source, int width, string expected)
    {
        Assert.Equal(expected, ImageVariantPlan.VariantUrl(source, width));
    }

    [Theory]
    [InlineData("hero-480w.jpg", 480)]
    [InlineData("/tmp/assets/hero-1200w.webp", 1200)]
    [InlineData("/tmp/assets/hero.v1-480w.jpg", 480)]
    [InlineData("hero-1w.jpg", 1)]
    [InlineData("hero-00480w.jpg", 480)]
    public void TryParseVariantFileName_ReadsWidth(string path, int expected)
    {
        Assert.True(ImageVariantPlan.TryParseVariantFileName(path, out var width));
        Assert.Equal(expected, width);
    }

    [Theory]
    [InlineData("hero.jpg")]
    [InlineData("hero-w.jpg")]
    [InlineData("-480w.jpg")]
    [InlineData("hero-480W.jpg")]
    [InlineData("hero-480w.jpg.tmp")]
    public void TryParseVariantFileName_RejectsNonVariants(string path)
    {
        Assert.False(ImageVariantPlan.TryParseVariantFileName(path, out _));
    }

    [Fact]
    public void VariantFileName_RoundTripsThroughParser()
    {
        var variant = ImageVariantPlan.VariantFileName("/tmp/assets/hero.jpg", 768, ".jpg");
        Assert.True(ImageVariantPlan.TryParseVariantFileName(variant, out var width));
        Assert.Equal(768, width);
    }
}
