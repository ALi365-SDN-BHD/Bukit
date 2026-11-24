using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class OpenGraphLocaleTests
{
    [Theory]
    [InlineData("en", "en_US")]
    [InlineData("EN", "en_US")]
    [InlineData("zh", "zh_CN")]
    [InlineData("ms", "ms_MY")]
    [InlineData("zh-CN", "zh_CN")]
    [InlineData("zh_tw", "zh_TW")]
    [InlineData("en-GB", "en_GB")]
    [InlineData("zh-Hant", "zh_TW")]
    [InlineData("zh-Hans", "zh_CN")]
    [InlineData("zh-Hant-HK", "zh_HK")]
    [InlineData("es-419", "es_ES")]
    [InlineData("de-u-co-phonebk", "de_DE")]
    [InlineData("en-x-custom", "en_US")]
    [InlineData("sr-Latn-RS", "sr_RS")]
    [InlineData(" pt ", "pt_BR")]
    [InlineData("xx", "")]
    [InlineData("und", "")]
    [InlineData("e", "")]
    [InlineData("12", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void From_MapsToLanguageAndRegion(string? language, string expected)
        => Assert.Equal(expected, OpenGraphLocale.From(language));

    [Fact]
    public void Alternates_ListsOtherLanguagesOnce()
    {
        SeoAlternateModel[] alternates =
        [
            new("x-default", "https://example.com/en/"),
            new("en", "https://example.com/en/"),
            new("zh-CN", "https://example.com/zh-CN/"),
            new("zh", "https://example.com/zh/"),
            new("xx", "https://example.com/xx/"),
            new("ms", "https://example.com/ms/")
        ];

        Assert.Equal(["zh_CN", "ms_MY"], OpenGraphLocale.Alternates("en_US", alternates));
        Assert.Empty(OpenGraphLocale.Alternates("", alternates));
    }
}
