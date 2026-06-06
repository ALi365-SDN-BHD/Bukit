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
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceMode"] = new ContentField("text", "data"),
                ["locale"] = new ContentField("text", "zh")
            });

        var items = new[] { dataItem };

        var matched = I18nOutputMerger.FilterItemsByLanguage(items, "zh", "en");
        Assert.Single(matched);

        var unmatched = I18nOutputMerger.FilterItemsByLanguage(items, "en", "en");
        Assert.Empty(unmatched);
    }

    [Fact]
    public void FilterItemsByLanguage_OrphanContentExcluded_WhenNotDefaultLanguage()
    {
        var zhItem = CreateItem("1", "zh");
        var items = new[] { zhItem };

        var result = I18nOutputMerger.FilterItemsByLanguage(items, "en", "en");

        Assert.Empty(result);
    }

    [Fact]
    public void FilterItemsByLanguage_OrphanContentIncluded_WhenMatchingLanguage()
    {
        var zhItem = CreateItem("1", "zh");
        var items = new[] { zhItem };

        var result = I18nOutputMerger.FilterItemsByLanguage(items, "zh", "en");

        Assert.Single(result);
        Assert.Equal("1", result[0].Id);
    }

    [Fact]
    public void FilterItemsByLanguage_DefaultIsZh_FiltersCorrectly()
    {
        var enItem = CreateItem("1", "en");
        var zhItem = CreateItem("2", "zh");
        var items = new[] { enItem, zhItem };

        var enResult = I18nOutputMerger.FilterItemsByLanguage(items, "en", "zh");
        var zhResult = I18nOutputMerger.FilterItemsByLanguage(items, "zh", "zh");

        Assert.Single(enResult);
        Assert.Equal("1", enResult[0].Id);
        Assert.Single(zhResult);
        Assert.Equal("2", zhResult[0].Id);
    }

    [Fact]
    public void FilterItemsByLanguage_LanguageCaseInsensitive()
    {
        var item1 = CreateItem("1", "zh-CN");
        var item2 = CreateItem("2", "ZH-CN");
        var items = new[] { item1, item2 };

        var result = I18nOutputMerger.FilterItemsByLanguage(items, "zh-cn", "en");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterItemsByLanguage_UsesStructuredLanguageField()
    {
        var item = new ContentItem(
            Id: "1",
            Title: "Structured",
            Slug: "structured",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>hi</p>",
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["language"] = new("text", "ms-MY")
            });

        var result = I18nOutputMerger.FilterItemsByLanguage(new[] { item }, "ms-my", "en");

        Assert.Single(result);
        Assert.Equal("1", result[0].Id);
    }

    [Fact]
    public void FilterItemsByLanguage_LanguageMismatch_Excludes()
    {
        var item = CreateItem("1", "zh-CN");
        var items = new[] { item };

        var result = I18nOutputMerger.FilterItemsByLanguage(items, "zh", "en");

        Assert.Empty(result);
    }

    [Fact]
    public void FilterItemsByLanguage_I18nKeyPair_FiltersByLanguage()
    {
        var enItem = CreateItemWithMeta("1", "en", new Dictionary<string, object> { ["language"] = "en", ["i18nKey"] = "about" });
        var zhItem = CreateItemWithMeta("2", "zh", new Dictionary<string, object> { ["language"] = "zh", ["i18nKey"] = "about" });
        var items = new[] { enItem, zhItem };

        var enResult = I18nOutputMerger.FilterItemsByLanguage(items, "en", "en");
        var zhResult = I18nOutputMerger.FilterItemsByLanguage(items, "zh", "en");

        Assert.Single(enResult);
        Assert.Equal("1", enResult[0].Id);
        Assert.Single(zhResult);
        Assert.Equal("2", zhResult[0].Id);
    }

    [Fact]
    public void GetLanguages_DeduplicatesCaseInsensitively()
    {
        var site = new SiteConfig { Name = "t", Title = "t", Languages = new[] { "en-US", "en-us", "EN-US", "zh-CN" } };
        var result = I18nOutputMerger.GetLanguages(site);

        Assert.Equal(2, result.Count);
        Assert.Contains("en-US", result);
        Assert.Contains("zh-CN", result);
    }

    [Theory]
    [InlineData("/", "", "/")]
    [InlineData("/", "  ", "/")]
    [InlineData("/base/", "en", "/base/en")]
    [InlineData("/base/", "zh-CN", "/base/zh-CN")]
    public void CombineBaseUrlWithLanguage_EdgeCases(string baseUrl, string lang, string expected)
    {
        var result = I18nOutputMerger.CombineBaseUrlWithLanguage(baseUrl, lang);
        Assert.Equal(expected, result);
    }

    private static ContentItem CreateItem(string id, string? language)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (language is not null)
        {
            fields["language"] = new ContentField("text", language);
        }

        return new ContentItem(
            Id: id,
            Title: $"Item {id}",
            Slug: $"item-{id}",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>hi</p>",
            Fields: fields);
    }

    private static ContentItem CreateItemWithMeta(string id, string? language, Dictionary<string, object> extraMeta)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (language is not null)
        {
            fields["language"] = new ContentField("text", language);
        }

        foreach (var (key, value) in extraMeta)
        {
            fields[key] = new ContentField("test", value);
        }

        return new ContentItem(
            Id: id,
            Title: $"Item {id}",
            Slug: $"item-{id}",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>hi</p>",
            Fields: fields);
    }
}
