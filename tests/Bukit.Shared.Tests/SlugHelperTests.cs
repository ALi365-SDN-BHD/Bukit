using System.Globalization;
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

        Assert.Equal("机器学习", result);
    }

    [Fact]
    public void Slugify_MixedCjkWithLatin_SeparatesCorrectly()
    {
        var result = SlugHelper.Slugify("AI 机器学习");

        Assert.Equal("ai-机器学习", result);
    }

    [Fact]
    public void Slugify_SingleDashPreserved_ConsecutiveDashesCollapsed()
    {
        var result = SlugHelper.Slugify("hello--world");

        Assert.Equal("hello-world", result);
    }

    [Theory]
    [InlineData("café", "cafe\u0301")]
    [InlineData("über", "u\u0308ber")]
    public void Slugify_CanonicallyEquivalentAccents_ProduceSameSlug(string composed, string decomposed)
    {
        Assert.Equal(SlugHelper.Slugify(composed), SlugHelper.Slugify(decomposed));
    }

    [Theory]
    [InlineData(" --中文._.ABC-- ", "中文-abc")]
    [InlineData("🚀 Hello 🌍", "hello")]
    [InlineData("Æ Œ Ø ß", "ae-oe-o-ss")]
    public void Slugify_UnicodeAndSeparatorBoundaries_ReturnExpected(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.Slugify(input));
    }

    [Fact]
    public void Slugify_TurkishCurrentCulture_DoesNotChangeOutput()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("istanbul-cafe", SlugHelper.Slugify("ISTANBUL CAFÉ"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Slugify_SeededValidUnicodeInputs_AreIdempotentAndHaveValidSeparators()
    {
        const int seed = 20261024;
        string[] tokens = ["A", "z", "0", "9", "中", "文", "é", "e\u0301", "ß", "œ", "ø", "🚀", " ", "-", "_", ".", "/", "\\", "\t", "!", "&"];
        var random = new Random(seed);
        for (var sample = 0; sample < 512; sample++)
        {
            var input = string.Concat(Enumerable.Range(0, random.Next(0, 65)).Select(_ => tokens[random.Next(tokens.Length)]));
            var slug = SlugHelper.Slugify(input);
            var context = $"seed={seed}, sample={sample}, input={System.Text.Json.JsonSerializer.Serialize(input)}, slug={slug}";

            Assert.True(slug == SlugHelper.Slugify(slug), context);
            Assert.True(slug.All(c => char.IsLetterOrDigit(c) || c == '-'), context);
            Assert.True(!slug.StartsWith('-') && !slug.EndsWith('-') && !slug.Contains("--", StringComparison.Ordinal), context);
        }
    }
}
