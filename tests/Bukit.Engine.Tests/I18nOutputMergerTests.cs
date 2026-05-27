using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nOutputMergerTests
{
    [Fact]
    public void GetLanguages_ReturnsEmpty_WhenNull()
    {
        var site = new SiteConfig { Name = "t", Title = "t" };
        var result = I18nOutputMerger.GetLanguages(site);
        Assert.Empty(result);
    }

    [Fact]
    public void GetLanguages_ReturnsEmpty_WhenEmpty()
    {
        var site = new SiteConfig { Name = "t", Title = "t", Languages = Array.Empty<string>() };
        var result = I18nOutputMerger.GetLanguages(site);
        Assert.Empty(result);
    }

    [Fact]
    public void GetLanguages_ReturnsTrimmed_AndDeduplicated()
    {
        var site = new SiteConfig { Name = "t", Title = "t", Languages = new[] { " zh-CN ", "en-US", "ZH-CN" } };
        var result = I18nOutputMerger.GetLanguages(site);
        Assert.Equal(new[] { "zh-CN", "en-US" }, result);
    }

    [Fact]
    public void GetDefaultLanguage_ReturnsSiteLanguage_WhenNoLanguages()
    {
        var site = new SiteConfig { Name = "t", Title = "t", Language = "ja-JP" };
        var langs = I18nOutputMerger.GetLanguages(site);
        var result = I18nOutputMerger.GetDefaultLanguage(site, langs);
        Assert.Equal("ja-JP", result);
    }

    [Fact]
    public void GetDefaultLanguage_UsesFirst_WhenDefaultNotSpecified()
    {
        var site = new SiteConfig { Name = "t", Title = "t", Languages = new[] { "en", "zh" } };
        var langs = I18nOutputMerger.GetLanguages(site);
        var result = I18nOutputMerger.GetDefaultLanguage(site, langs);
        Assert.Equal("en", result);
    }

    [Fact]
    public void GetDefaultLanguage_UsesDefault_WhenSpecified()
    {
        var site = new SiteConfig { Name = "t", Title = "t", DefaultLanguage = "zh", Languages = new[] { "en", "zh" } };
        var langs = I18nOutputMerger.GetLanguages(site);
        var result = I18nOutputMerger.GetDefaultLanguage(site, langs);
        Assert.Equal("zh", result);
    }

    [Fact]
    public void GetDefaultLanguage_FallsBack_WhenDefaultNotInList()
    {
        var site = new SiteConfig { Name = "t", Title = "t", DefaultLanguage = "fr", Languages = new[] { "en", "zh" } };
        var langs = I18nOutputMerger.GetLanguages(site);
        var result = I18nOutputMerger.GetDefaultLanguage(site, langs);
        Assert.Equal("en", result);
    }

    [Theory]
    [InlineData("/", "en", "/en")]
    [InlineData("/", "zh-CN", "/zh-CN")]
    [InlineData("/", "/en/", "/en")]
    [InlineData("", "en", "/en")]
    [InlineData("/base/", "en", "/base/en")]
    public void CombineBaseUrlWithLanguage(string baseUrl, string lang, string expected)
    {
        var result = I18nOutputMerger.CombineBaseUrlWithLanguage(baseUrl, lang);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FilterItemsByLanguage_FiltersRegularItems()
    {
        var enItem = CreateItem("1", "en");
        var zhItem = CreateItem("2", "zh");
        var items = new[] { enItem, zhItem };

        var result = I18nOutputMerger.FilterItemsByLanguage(items, "zh", "en");

        Assert.Single(result);
        Assert.Equal("2", result[0].Id);
    }

    [Fact]
    public void FilterItemsByLanguage_IncludesItemsWithoutLanguage_WhenMatchingDefault()
    {
        var enItem = CreateItem("1", "en");
        var noLangItem = CreateItem("3", null);
        var items = new[] { enItem, noLangItem };

        var result = I18nOutputMerger.FilterItemsByLanguage(items, "en", "en");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterItemsByLanguage_ExcludesItemsWithoutLanguage_WhenNotMatchingDefault()
    {
        var zhItem = CreateItem("2", "zh");
        var noLangItem = CreateItem("3", null);
        var items = new[] { zhItem, noLangItem };

        var result = I18nOutputMerger.FilterItemsByLanguage(items, "zh", "en");

        Assert.Single(result);
        Assert.Equal("2", result[0].Id);
    }

    [Fact]
    public void FilterItemsByLanguage_FiltersDataItemsByLocale()
    {
        var dataItem = new ContentItem(
            Id: "data-1",
            Title: "Data",
            Slug: "data",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceMode"] = "data"
            },
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["locale"] = new ContentField("text", "zh")
            });

        var items = new[] { dataItem };

        var matched = I18nOutputMerger.FilterItemsByLanguage(items, "zh", "en");
        Assert.Single(matched);

        var unmatched = I18nOutputMerger.FilterItemsByLanguage(items, "en", "en");
        Assert.Empty(unmatched);
    }

    private static ContentItem CreateItem(string id, string? language)
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (language is not null)
        {
            meta["language"] = language;
        }

        return new ContentItem(
            Id: id,
            Title: $"Item {id}",
            Slug: $"item-{id}",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>hi</p>",
            Meta: meta,
            Fields: null);
    }
}
