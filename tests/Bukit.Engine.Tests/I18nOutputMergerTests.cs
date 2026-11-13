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
    public void FilterDocumentsByLanguage_FiltersRegularItems()
    {
        var enItem = CreateItem("1", "en");
        var zhItem = CreateItem("2", "zh");
        var items = new[] { enItem, zhItem };

        var result = I18nOutputMerger.FilterDocumentsByLanguage(items, "zh", "en");

        Assert.Single(result);
        Assert.Equal("2", result[0].Id);
    }

    [Fact]
    public void FilterDocumentsByLanguage_IncludesItemsWithoutLanguage_WhenMatchingDefault()
    {
        var enItem = CreateItem("1", "en");
        var noLangItem = CreateItem("3", null);
        var items = new[] { enItem, noLangItem };

        var result = I18nOutputMerger.FilterDocumentsByLanguage(items, "en", "en");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterDocumentsByLanguage_ExcludesItemsWithoutLanguage_WhenNotMatchingDefault()
    {
        var zhItem = CreateItem("2", "zh");
        var noLangItem = CreateItem("3", null);
        var items = new[] { zhItem, noLangItem };

        var result = I18nOutputMerger.FilterDocumentsByLanguage(items, "zh", "en");

        Assert.Single(result);
        Assert.Equal("2", result[0].Id);
    }

    [Fact]
    public void FilterDocumentsByLanguage_FiltersDataItemsByLocale()
    {
        var dataItem = ContentDocument.Create(
            id: "data-1",
            title: "Data",
            slug: "data",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "",
            fields: ContentFieldReader.WithValues(new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["locale"] = new ContentField("text", "zh")
            }, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceMode"] = "data"
            }));

        var items = new[] { dataItem };

        var matched = I18nOutputMerger.FilterDocumentsByLanguage(items, "zh", "en");
        Assert.Single(matched);

        var unmatched = I18nOutputMerger.FilterDocumentsByLanguage(items, "en", "en");
        Assert.Empty(unmatched);
    }

    [Fact]
    public void FilterDocumentsByLanguage_OrphanContentExcluded_WhenNotDefaultLanguage()
    {
        var zhItem = CreateItem("1", "zh");
        var items = new[] { zhItem };

        var result = I18nOutputMerger.FilterDocumentsByLanguage(items, "en", "en");

        Assert.Empty(result);
    }

    [Fact]
    public void FilterDocumentsByLanguage_OrphanContentIncluded_WhenMatchingLanguage()
    {
        var zhItem = CreateItem("1", "zh");
        var items = new[] { zhItem };

        var result = I18nOutputMerger.FilterDocumentsByLanguage(items, "zh", "en");

        Assert.Single(result);
        Assert.Equal("1", result[0].Id);
    }

    [Fact]
    public void FilterDocumentsByLanguage_DefaultIsZh_FiltersCorrectly()
    {
        var enItem = CreateItem("1", "en");
        var zhItem = CreateItem("2", "zh");
        var items = new[] { enItem, zhItem };

        var enResult = I18nOutputMerger.FilterDocumentsByLanguage(items, "en", "zh");
        var zhResult = I18nOutputMerger.FilterDocumentsByLanguage(items, "zh", "zh");

        Assert.Single(enResult);
        Assert.Equal("1", enResult[0].Id);
        Assert.Single(zhResult);
        Assert.Equal("2", zhResult[0].Id);
    }

    [Fact]
    public void FilterDocumentsByLanguage_LanguageCaseInsensitive()
    {
        var item1 = CreateItem("1", "zh-CN");
        var item2 = CreateItem("2", "ZH-CN");
        var items = new[] { item1, item2 };

        var result = I18nOutputMerger.FilterDocumentsByLanguage(items, "zh-cn", "en");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterDocumentsByLanguage_UsesStructuredLanguageField()
    {
        var item = ContentDocument.Create(
            id: "1",
            title: "Structured",
            slug: "structured",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>hi</p>",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["language"] = new("text", "ms-MY")
            });

        var result = I18nOutputMerger.FilterDocumentsByLanguage(new[] { item }, "ms-my", "en");

        Assert.Single(result);
        Assert.Equal("1", result[0].Id);
    }

    [Fact]
    public void FilterDocumentsByLanguage_LanguageMismatch_Excludes()
    {
        var item = CreateItem("1", "zh-CN");
        var items = new[] { item };

        var result = I18nOutputMerger.FilterDocumentsByLanguage(items, "zh", "en");

        Assert.Empty(result);
    }

    [Fact]
    public void FilterDocumentsByLanguage_I18nKeyPair_FiltersByLanguage()
    {
        var enItem = CreateItemWithMeta("1", "en", new Dictionary<string, object> { ["language"] = "en", ["i18nKey"] = "about" });
        var zhItem = CreateItemWithMeta("2", "zh", new Dictionary<string, object> { ["language"] = "zh", ["i18nKey"] = "about" });
        var items = new[] { enItem, zhItem };

        var enResult = I18nOutputMerger.FilterDocumentsByLanguage(items, "en", "en");
        var zhResult = I18nOutputMerger.FilterDocumentsByLanguage(items, "zh", "en");

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

    private static ContentDocument CreateItem(string id, string? language)
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (language is not null)
        {
            meta["language"] = language;
        }

        return ContentDocument.Create(
            id: id,
            title: $"Item {id}",
            slug: $"item-{id}",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>hi</p>",
            fields: ContentFieldReader.ToFieldMap(meta));
    }

    [Fact]
    public void ContentDocumentCreate_UsesI18nKeyAliasForCanonicalUrlKey()
    {
        var document = CreateItemWithMeta("1", "en", new Dictionary<string, object> { ["i18n_key"] = "about" });

        Assert.Equal("about", document.Record.Identity.CanonicalUrlKey);
    }

    [Fact]
    public void WarnDocumentLanguageIssues_SingleLanguageWithDefaultLanguage_DoesNotWarn()
    {
        // site.languages always has a value now, and validation keeps defaultLanguage in it.
        var site = new SiteConfig { Name = "t", Title = "t", Language = "en", Languages = ["en"], DefaultLanguage = "en" };
        var logger = new ListLogger();

        I18nLanguagePolicy.WarnDocumentLanguageIssues([], site, logger);

        Assert.DoesNotContain(logger.Warnings, message =>
            message.Contains("i18n.config.default_language_ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void WarnDocumentLanguageIssues_WarnsForUnknownLanguage()
    {
        var site = new SiteConfig { Name = "t", Title = "t", Languages = ["en", "zh-CN"], DefaultLanguage = "en" };
        var logger = new ListLogger();
        var item = CreateItemWithMeta("1", "zh", new Dictionary<string, object>());

        I18nLanguagePolicy.WarnDocumentLanguageIssues([item], site, logger);

        Assert.Contains(logger.Warnings, message =>
            message.Contains("i18n.document.language_unknown", StringComparison.Ordinal) &&
            message.Contains("language=zh", StringComparison.Ordinal) &&
            message.Contains("configured=en,zh-CN", StringComparison.Ordinal));
    }

    [Fact]
    public void WarnDocumentLanguageIssues_WarnsForDuplicateI18nKey()
    {
        var site = new SiteConfig { Name = "t", Title = "t", Languages = ["en", "zh-CN"], DefaultLanguage = "en" };
        var logger = new ListLogger();
        var first = CreateItemWithMeta("1", "en", new Dictionary<string, object> { ["i18nKey"] = "about" });
        var second = CreateItemWithMeta("2", "en", new Dictionary<string, object> { ["i18nKey"] = "about" });

        I18nLanguagePolicy.WarnDocumentLanguageIssues([first, second], site, logger);

        Assert.Contains(logger.Warnings, message =>
            message.Contains("i18n.document.i18n_key_duplicate", StringComparison.Ordinal) &&
            message.Contains("key=about", StringComparison.Ordinal) &&
            message.Contains("ids=1,2", StringComparison.Ordinal));
    }

    [Fact]
    public void WarnDocumentLanguageIssues_SingleEntryLanguagesWarnsLikeASingleLanguageSite()
    {
        // One entry is a single-language site: the same warnings as a site with only site.language.
        var single = new SiteConfig { Name = "t", Title = "t", Language = "en", Languages = ["en"] };
        var legacy = new SiteConfig { Name = "t", Title = "t", Language = "en" };
        var documents = new[]
        {
            CreateItemWithMeta("1", "en", new Dictionary<string, object> { ["i18nKey"] = "about" }),
            CreateItemWithMeta("2", "en", new Dictionary<string, object> { ["i18nKey"] = "about" }),
            CreateItemWithMeta("3", "fr", new Dictionary<string, object>())
        };
        var singleLogger = new ListLogger();
        var legacyLogger = new ListLogger();

        I18nLanguagePolicy.WarnDocumentLanguageIssues(documents, single, singleLogger);
        I18nLanguagePolicy.WarnDocumentLanguageIssues(documents, legacy, legacyLogger);

        Assert.Equal(legacyLogger.Warnings, singleLogger.Warnings);
        Assert.Contains(singleLogger.Warnings, message =>
            message.Contains("i18n.document.i18n_key_duplicate", StringComparison.Ordinal));
        Assert.Contains(singleLogger.Warnings, message =>
            message.Contains("i18n.document.language_unknown", StringComparison.Ordinal) &&
            message.Contains("configured=en", StringComparison.Ordinal));
    }

    [Fact]
    public void WarnDocumentLanguageIssues_DoesNotWarnForKnownTranslations()
    {
        var site = new SiteConfig { Name = "t", Title = "t", Languages = ["en", "zh-CN"], DefaultLanguage = "en" };
        var logger = new ListLogger();
        var enItem = CreateItemWithMeta("1", "en", new Dictionary<string, object> { ["i18nKey"] = "about" });
        var zhItem = CreateItemWithMeta("2", "zh-CN", new Dictionary<string, object> { ["i18nKey"] = "about" });

        I18nLanguagePolicy.WarnDocumentLanguageIssues([enItem, zhItem], site, logger);

        Assert.Empty(logger.Warnings);
    }

    private static ContentDocument CreateItemWithMeta(string id, string? language, Dictionary<string, object> extraMeta)
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (language is not null)
        {
            meta["language"] = language;
        }

        foreach (var (key, value) in extraMeta)
        {
            meta[key] = value;
        }

        return ContentDocument.Create(
            id: id,
            title: $"Item {id}",
            slug: $"item-{id}",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>hi</p>",
            fields: ContentFieldReader.ToFieldMap(meta));
    }

    private sealed class ListLogger : ILogger
    {
        internal List<string> Warnings { get; } = [];

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }
}
