using Bukit.Config;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nOutputLayoutTests
{
    private static SiteConfig Site(string baseUrl, bool inSubdir, params string[] languages) => new()
    {
        Name = "layout-test",
        Title = "Layout test",
        BaseUrl = baseUrl,
        Languages = languages.Length == 0 ? null : languages,
        DefaultLanguage = languages.Length == 0 ? null : languages[0],
        DefaultLanguageInSubdir = inSubdir
    };

    [Theory]
    [InlineData("/")]
    [InlineData("/repo")]
    public void PrefixedLayout_KeepsEveryLanguageUnderItsCode(string baseUrl)
    {
        var site = Site(baseUrl, inSubdir: true, "en", "zh-CN");
        var layout = I18nOutputLayout.Create(site, ["en", "zh-CN"], site.BaseUrl);

        Assert.True(layout.IsMultilingual);
        Assert.False(layout.IsRootLanguage("en"));
        Assert.False(layout.IsRootLanguage("zh-CN"));
        Assert.Equal(baseUrl == "/" ? "/en" : "/repo/en", layout.BaseUrl("en"));
        Assert.Equal(baseUrl == "/" ? "/zh-CN" : "/repo/zh-CN", layout.BaseUrl("zh-CN"));
        Assert.Equal("en", layout.RelativeOutputPrefix("en"));
        Assert.Equal(Path.Combine("/out", "en"), layout.OutputDir("/out", "en"));
        Assert.Equal(Path.Combine("/out", "en", ".bukit"), layout.ReportDir("/out", "en"));
        Assert.Empty(layout.ReservedPrefixes);
    }

    [Theory]
    [InlineData("/", "/", "/zh-CN")]
    [InlineData("/repo", "/repo", "/repo/zh-CN")]
    public void RootLayout_PutsDefaultAtRootAndKeepsOtherCodes(string baseUrl, string expectedDefault, string expectedOther)
    {
        var site = Site(baseUrl, inSubdir: false, "en", "zh-CN");
        var layout = I18nOutputLayout.Create(site, ["en", "zh-CN"], site.BaseUrl);

        Assert.True(layout.IsRootLanguage("en"));
        Assert.False(layout.IsRootLanguage("zh-CN"));
        Assert.Equal(expectedDefault, layout.BaseUrl("en"));
        Assert.Equal(expectedOther, layout.BaseUrl("zh-CN"));
        Assert.Equal(string.Empty, layout.RelativeOutputPrefix("en"));
        Assert.Equal("zh-CN", layout.RelativeOutputPrefix("zh-CN"));
        Assert.Equal("/out", layout.OutputDir("/out", "en"));
        Assert.Equal(Path.Combine("/out", "zh-CN"), layout.OutputDir("/out", "zh-CN"));
        Assert.Equal(Path.Combine("/out", ".bukit", "variants", "en"), layout.ReportDir("/out", "en"));
        Assert.Equal(Path.Combine("/out", "zh-CN", ".bukit"), layout.ReportDir("/out", "zh-CN"));
        Assert.Equal(["zh-CN"], layout.ReservedPrefixes);
        Assert.Equal(Path.Combine("/out", ".bukit", "variants", "en"), layout.VariantReportDir("/out", "en"));
        Assert.Equal(Path.Combine("/out", "zh-CN", ".bukit"), layout.VariantReportDir(Path.Combine("/out", "zh-CN"), "zh-CN"));
    }

    private static AppConfig Config(string sitemapMode, string feedMode = "split", string searchMode = "split")
    {
        var site = Site("/", inSubdir: false, "en", "zh-CN") with
        {
            Url = "https://example.com",
            SitemapMode = sitemapMode,
            Feed = new FeedConfig { Mode = feedMode, Formats = ["rss", "atom", "json"], Path = "feeds" },
            Search = new SearchDetailConfig { Mode = searchMode },
            Seo = new SeoConfig
            {
                RobotsTxt = new SeoRobotsTxtConfig { Enabled = true },
                Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true, LlmsFullTxt = true }
            }
        };
        return new AppConfig { Site = site, Content = TestContent.Markdown() };
    }

    [Theory]
    [InlineData("split", "sitemap.xml")]
    [InlineData("index", "en/sitemap.xml")]
    [InlineData("merged", null)]
    public void RootLanguagePlan_FollowsSitemapModeAndSkipsRootOwnedFiles(string mode, string? sitemap)
    {
        var plan = PublicOutputLifecycle.ProjectionPlan(Config(mode), [], language: "en");
        var destinations = plan.Select(item => item.Destination).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("agent-manifest.json", destinations);
        Assert.DoesNotContain("robots.txt", destinations);
        Assert.DoesNotContain("llms.txt", destinations);
        Assert.DoesNotContain("llms-full.txt", destinations);
        if (sitemap is null)
        {
            Assert.DoesNotContain(destinations, path => path.EndsWith("sitemap.xml", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(sitemap, destinations);
        }
    }

    [Theory]
    [InlineData("split", true)]
    [InlineData("merged", false)]
    public void PrefixedLanguagePlan_MatchesAggregateMode(string mode, bool writesOwnFiles)
    {
        var plan = PublicOutputLifecycle.ProjectionPlan(Config(mode, feedMode: mode), [], language: "zh-CN");
        var destinations = plan.Select(item => item.Destination).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(writesOwnFiles, destinations.Contains("sitemap.xml"));
        Assert.Equal(writesOwnFiles, destinations.Contains("rss.xml"));
        Assert.Contains("agent-manifest.json", destinations);
    }

    [Fact]
    public void RootAggregatePlan_ListsOnlyRootOwnedFilesForItsMode()
    {
        var plan = PublicOutputLifecycle.ProjectionPlan(Config("merged", feedMode: "merged", searchMode: "merged"), [], root: true);
        var destinations = plan.Select(item => item.Destination).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("agent-manifest.json", destinations);
        Assert.Contains("robots.txt", destinations);
        Assert.Contains("llms.txt", destinations);
        Assert.Contains("llms-full.txt", destinations);
        Assert.Contains("sitemap.xml", destinations);
        Assert.Contains("search.json", destinations);
        Assert.Contains("rss.xml", destinations);
        Assert.DoesNotContain("bukit-search.html", destinations);
    }

    [Fact]
    public void EmptyLanguages_IgnoreTheRootSetting()
    {
        var site = Site("/", inSubdir: false);
        var layout = I18nOutputLayout.Create(site, [], site.BaseUrl);

        Assert.False(layout.IsMultilingual);
        Assert.False(layout.IsRootLanguage("en"));
        Assert.Equal("/", layout.BaseUrl("en"));
        Assert.Equal(string.Empty, layout.RelativeOutputPrefix("en"));
        Assert.Equal("/out", layout.OutputDir("/out", "en"));
        Assert.Empty(layout.ReservedPrefixes);
    }
}
