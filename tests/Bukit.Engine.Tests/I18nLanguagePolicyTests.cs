using Bukit.Config;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nLanguagePolicyTests
{
    private static SiteConfig Site(IReadOnlyList<string>? languages, string? defaultLanguage = null)
        => new() { Name = "s", Title = "S", Languages = languages?.ToList(), DefaultLanguage = defaultLanguage };

    [Theory]
    [InlineData(new[] { "en" }, null, "en")]
    [InlineData(new[] { " en ", "EN" }, null, "en")]
    [InlineData(new[] { "en", "fr" }, null, "en")]
    [InlineData(new[] { "en", "fr" }, "fr", "fr")]
    [InlineData(new[] { "en", "fr" }, "de", "en")]
    public void NormalizeSite_SetsTheDefaultLanguage(string[] languages, string? defaultLanguage, string expected)
    {
        var site = I18nLanguagePolicy.NormalizeSite(Site(languages, defaultLanguage));

        Assert.Equal(expected, site.Language);
    }

    public static TheoryData<string[]?> NoLanguages => new() { null, Array.Empty<string>(), new[] { "", " " } };

    [Theory]
    [MemberData(nameof(NoLanguages))]
    public void NormalizeSite_WithoutLanguages_KeepsTheSite(string[]? languages)
    {
        var site = Site(languages) with { Language = "ja" };

        Assert.Same(site, I18nLanguagePolicy.NormalizeSite(site));
    }

    [Fact]
    public void GetLanguages_TrimsAndDropsBlankAndDuplicateEntries()
    {
        Assert.Equal(["en", "fr"], I18nLanguagePolicy.GetLanguages(Site(["en", " ", "fr ", "EN"])));
    }
}
