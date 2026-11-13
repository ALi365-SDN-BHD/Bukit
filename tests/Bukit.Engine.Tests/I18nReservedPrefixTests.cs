using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nReservedPrefixTests
{
    private static AppConfig Config(params string[] languages) => new()
    {
        Site = new SiteConfig
        {
            Name = "reserved-prefix",
            Title = "Reserved prefix",
            Language = languages.Length == 0 ? "zh-CN" : languages[0],
            Languages = languages.Length == 0 ? null : languages,
            DefaultLanguage = languages.Length == 0 ? null : languages[0],
            DefaultLanguageInSubdir = false
        },
        Content = TestContent.Markdown()
    };

    [Theory]
    [InlineData("zh-CN/notes/index.html", "zh-CN")]
    [InlineData("ZH-cn/notes/index.html", "zh-CN")]
    [InlineData("/zh-cn/notes/index.html", "zh-CN")]
    [InlineData("en/notes/index.html", null)]
    [InlineData("notes/index.html", null)]
    [InlineData("", null)]
    public void FindReservedPrefix_ComparesFirstSegmentCaseInsensitively(string path, string? expected)
    {
        var reserved = I18nOutputLayout.Create(Config("en", "zh-CN").Site, ["en", "zh-CN"], "/")
            .ReservedPrefixes;

        Assert.Equal(expected, I18nReservedPrefixGuard.FindReservedPrefix(path, reserved));
    }

    [Fact]
    public void ValidateAssetPlan_RejectsStaticAndAssetPathsUnderAnotherLanguage()
    {
        var config = Config("en", "zh-CN");
        var staticItem = new AssetOutputItem("static/x.txt", "zh-CN/x.txt", AssetOutputCategory.Static);
        var assetItem = new AssetOutputItem("assets/logo.svg", "assets/logo.svg", AssetOutputCategory.Assets);
        var tokenItem = new AssetOutputItem("theme tokens", "assets/css/theme-tokens.css", AssetOutputCategory.Tokens);
        var defaultCode = new AssetOutputItem("static/en.txt", "en/x.txt", AssetOutputCategory.Static);

        var ex = Assert.Throws<BukitException>(() =>
            I18nReservedPrefixGuard.ValidateAssetPlan(config, [staticItem]));
        Assert.Equal(DiagnosticCode.I18nRouteReservedLanguagePrefix, ex.Code);
        Assert.Contains("zh-CN", ex.Message, StringComparison.Ordinal);
        I18nReservedPrefixGuard.ValidateAssetPlan(config, [defaultCode]);
        I18nReservedPrefixGuard.ValidateAssetPlan(config with { Site = config.Site with { Language = "zh-CN" } }, [staticItem]);

        var prefixEx = Assert.Throws<BukitException>(() =>
            I18nReservedPrefixGuard.ValidateAssetPlan(config with
            {
                Site = config.Site with { Languages = ["en", "assets"], DefaultLanguage = "en" }
            }, [assetItem]));
        Assert.Contains("assets", prefixEx.Message, StringComparison.Ordinal);

        var tokenEx = Assert.Throws<BukitException>(() =>
            I18nReservedPrefixGuard.ValidateAssetPlan(config with
            {
                Site = config.Site with { Languages = ["en", "assets"], DefaultLanguage = "en" }
            }, [tokenItem]));
        Assert.Contains("assets", tokenEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAssetPlan_RejectsProjectionFeedUnderAnotherLanguage()
    {
        var config = Config("en", "zh-CN");
        var feed = new AssetOutputItem("feed", "zh-CN/news.xml", AssetOutputCategory.Projection, Operation: AssetOutputOperation.Generate);

        var ex = Assert.Throws<BukitException>(() => I18nReservedPrefixGuard.ValidateAssetPlan(config, [feed]));

        Assert.Equal(DiagnosticCode.I18nRouteReservedLanguagePrefix, ex.Code);
        Assert.Contains("zh-CN/news.xml", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatePluginOutputs_RejectsReservedPath()
    {
        var config = Config("en", "zh-CN");

        var ex = Assert.Throws<BukitException>(() =>
            I18nReservedPrefixGuard.ValidatePluginOutputs(config,
                [new PluginOutputTrackingInfo("service-worker", "AfterBuild", "zh-CN/sw.js")]));

        Assert.Equal(DiagnosticCode.I18nRouteReservedLanguagePrefix, ex.Code);
        Assert.Contains("zh-CN/sw.js", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectPrimaryFailure_PrefersDefaultLanguageReservedPrefix()
    {
        var reserved = new BukitException("reserved", DiagnosticCode.I18nRouteReservedLanguagePrefix);
        var other = new InvalidOperationException("other");

        var selected = I18nReservedPrefixGuard.SelectPrimaryFailure(
            new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = other,
                ["en"] = reserved
            },
            "en");

        Assert.Same(reserved, selected);
        var fallback = I18nReservedPrefixGuard.SelectPrimaryFailure(
            new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = other,
                ["en"] = new InvalidOperationException("en")
            },
            "en");
        Assert.Equal("en", Assert.IsType<InvalidOperationException>(fallback).Message);
    }

    [Fact]
    public async Task Build_RejectsReservedPermalinkCaseInsensitively()
    {
        using var site = new SiteBuilder("i18n-site");
        var config = RootConfig(site.Config) with
        {
            Site = RootConfig(site.Config).Site with
            {
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["page"] = new() { Permalink = "/ZH-cn/{slug}/", Template = "pages/page.html" }
                }
            }
        };

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.I18nRouteReservedLanguagePrefix, ex.Code);
        Assert.Contains("ZH-cn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Build_AllowsLegacyAliasUnderDefaultLanguageCode()
    {
        using var site = new SiteBuilder("i18n-site");
        var contentPath = site.Sandbox.PathFor("content/en/index.md");
        File.WriteAllText(contentPath, File.ReadAllText(contentPath)
            .Replace("collection: page\n", "collection: page\naliases:\n  - /en/about/\n", StringComparison.Ordinal));

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(RootConfig(site.Config), site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "en", "about", "index.html")));
        var llmsPath = Path.Combine(site.Output, "llms.txt");
        if (File.Exists(llmsPath))
        {
            Assert.DoesNotContain("/en/about/", File.ReadAllText(llmsPath), StringComparison.Ordinal);
        }

        using var report = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(site.Output, ".bukit", "publish-audit-report.json")));
        var alias = report.RootElement.GetProperty("documents").EnumerateArray()
            .Single(document => document.GetProperty("routeUrl").GetString() == "/en/about/");
        Assert.False(alias.GetProperty("indexable").GetBoolean());
    }

    [Fact]
    public async Task Build_RejectsStaticFileUnderAnotherLanguageCode()
    {
        using var site = new SiteBuilder("i18n-site");
        site.Sandbox.Write("static/zh-CN/x.txt", "reserved");

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(RootConfig(site.Config), site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.I18nRouteReservedLanguagePrefix, ex.Code);
        Assert.Contains("static/zh-CN/x.txt", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Build_AddingLanguageRejectsExistingDefaultLanguageRoute()
    {
        using var site = new SiteBuilder("i18n-site");
        var baseConfig = RootConfig(site.Config);
        var config = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Languages = ["en", "zh-CN", "ms"],
                DefaultLanguage = "en",
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["page"] = new() { Permalink = "/ms/{slug}/", Template = "pages/page.html" }
                }
            }
        };

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.I18nRouteReservedLanguagePrefix, ex.Code);
        Assert.Contains("ms", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Build_AllowsOtherLanguageCodeInsideNonDefaultLanguage()
    {
        using var site = new SiteBuilder("i18n-site");
        var contentPath = site.Sandbox.PathFor("content/zh/index.md");
        File.WriteAllText(contentPath, File.ReadAllText(contentPath)
            .Replace("collection: page\n", "collection: page\naliases:\n  - /ms/old/\n", StringComparison.Ordinal));
        var baseConfig = RootConfig(site.Config);
        var config = baseConfig with
        {
            Site = baseConfig.Site with { Languages = ["en", "zh-CN", "ms"], DefaultLanguage = "en" }
        };

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "ms", "old", "index.html")));
    }

    private static AppConfig RootConfig(AppConfig config)
    {
        var path = config;
        return path with
        {
            Site = path.Site with
            {
                DefaultLanguageInSubdir = false,
                SitemapMode = "split",
                Search = path.Site.Search with { Mode = "split" }
            }
        };
    }
}
