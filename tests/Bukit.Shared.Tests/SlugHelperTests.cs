using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class SlugHelperTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("hello world", "hello-world")]
    [InlineData("  Hello  World  ", "hello-world")]
    [InlineData("hello_world.name", "hello-world-name")]
    [InlineData("c#-sharp", "c-sharp")]
    [InlineData("hello!@#$%^&*()world", "helloworld")]
    public void Slugify_VariousInputs_ReturnsExpected(string input, string expected)
    {
        var result = SlugHelper.Slugify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("!!!")]
    public void Slugify_EmptyOrWhitespaceOnly_ReturnsEmpty(string input)
    {
        var result = SlugHelper.Slugify(input);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Slugify_Null_ReturnsEmpty()
    {
        var result = SlugHelper.Slugify(null!);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData("café", "cafe")]
    [InlineData("naïve", "naive")]
    [InlineData("über", "uber")]
    [InlineData("façade", "facade")]
    public void Slugify_AccentedLatin_TransliteratesAccents(string input, string expected)
    {
        var result = SlugHelper.Slugify(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Slugify_GermanEszett_TransliteratesToSs()
    {
        var result = SlugHelper.Slugify("Straße");
        Assert.Equal("strasse", result);
    }

    [Theory]
    [InlineData("æon", "aeon")]
    [InlineData("œuvre", "oeuvre")]
    [InlineData("københavn", "kobenhavn")]
    public void Slugify_SpecialLatinLigatures_Transliterates(string input, string expected)
    {
        var result = SlugHelper.Slugify(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Slugify_CjkCharacters_RetainsCharacters()
    {
        var result = SlugHelper.Slugify("机器学习");

        Assert.True(result.Length > 0);
        Assert.DoesNotContain("-", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Slugify_MixedCjkWithLatin_SeparatesCorrectly()
    {
        var result = SlugHelper.Slugify("AI 机器学习");

        Assert.StartsWith("ai", result, StringComparison.Ordinal);
        Assert.Contains("-", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Slugify_SingleDashPreserved_ConsecutiveDashesCollapsed()
    {
        var result = SlugHelper.Slugify("hello--world");

        Assert.Equal("hello-world", result);
    }
}
