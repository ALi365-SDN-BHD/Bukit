using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

/// <summary>
/// Locks the output of a single-language site. A site with one language must build the same
/// way however that language is configured: at the output root, with no language directory,
/// no hreflang alternates, no redirect page, and the same slugs for same-named files.
/// </summary>
public sealed class SingleLanguageSiteTests
{
    private const string LanguagesYaml = "  languages: [en]\n";
    private const string LanguagesWithDefaultYaml = "  languages: [en]\n  defaultLanguage: en\n";

    private static SiteBuilder EnglishSite(string languageYaml)
    {
        var site = new SiteBuilder();
        var path = site.Sandbox.PathFor("site.yaml");
        var yaml = File.ReadAllText(path).ReplaceLineEndings("\n")
            .Replace("  languages: [en]\n", languageYaml, StringComparison.Ordinal);
        Assert.Contains(languageYaml, yaml, StringComparison.Ordinal);
        File.WriteAllText(path, yaml);
        site.Sandbox.Write("content/index.md", "---\ntitle: Home\n---\nHome page.\n");
        site.Sandbox.Write("content/en/a/foo.md", "---\ntitle: Foo A\n---\nFirst foo.\n");
        site.Sandbox.Write("content/en/b/foo.md", "---\ntitle: Foo B\n---\nSecond foo.\n");
        return site;
    }

    private static async Task<SiteBuilder> BuildAsync(string languageYaml)
    {
        var site = EnglishSite(languageYaml);
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false });
        return site;
    }

    public static TheoryData<string> LanguageForms => new() { LanguagesYaml, LanguagesWithDefaultYaml };

    [Theory]
    [MemberData(nameof(LanguageForms))]
    public async Task SingleLanguage_BuildsAtTheOutputRoot(string languageYaml)
    {
        using var site = await BuildAsync(languageYaml);

        var home = File.ReadAllText(Path.Combine(site.Output, "index.html"));
        Assert.Contains("lang=\"en\"", home, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(site.Output, "en", "index.html")));
        Assert.DoesNotContain("http-equiv=\"refresh\"", home, StringComparison.OrdinalIgnoreCase);
    }

    // A forward lock: SeoAlternatesService already skips groups with fewer than two languages,
    // so this guards against future changes rather than against a one-entry multilingual build.
    [Theory]
    [MemberData(nameof(LanguageForms))]
    public async Task SingleLanguage_WritesNoHreflangAlternates(string languageYaml)
    {
        using var site = await BuildAsync(languageYaml);

        foreach (var html in Directory.EnumerateFiles(site.Output, "*.html", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("hreflang", File.ReadAllText(html), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(LanguageForms))]
    public async Task SingleLanguage_KeepsTheLanguageFolderInCollidingSlugs(string languageYaml)
    {
        using var site = await BuildAsync(languageYaml);

        Assert.True(File.Exists(Path.Combine(site.Output, "en", "a", "foo", "index.html")));
        Assert.True(File.Exists(Path.Combine(site.Output, "en", "b", "foo", "index.html")));
        Assert.False(File.Exists(Path.Combine(site.Output, "a", "foo", "index.html")));
        Assert.False(File.Exists(Path.Combine(site.Output, "b", "foo", "index.html")));
    }

    [Fact]
    public async Task SingleEntryLanguages_MetricsRecordTheConfiguredListAndTheDerivedLanguage()
    {
        using var site = EnglishSite(LanguagesYaml);
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false, MetricsPath = "metrics.json" });

        using var metrics = JsonDocument.Parse(File.ReadAllText(site.Sandbox.PathFor("metrics.json")));
        var metricsSite = metrics.RootElement.GetProperty("site");
        Assert.Equal("en", metricsSite.GetProperty("language").GetString());
        Assert.Equal(["en"], metricsSite.GetProperty("languages").EnumerateArray().Select(item => item.GetString()));
    }

    // Spec scenario "Single-language search record language".
    [Theory]
    [MemberData(nameof(LanguageForms))]
    public async Task SingleLanguage_SearchRecordsUseTheSiteLanguage(string languageYaml)
    {
        using var site = await BuildAsync(languageYaml);

        using var search = JsonDocument.Parse(File.ReadAllText(Path.Combine(site.Output, "search.json")));
        var records = search.RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(records);
        Assert.All(records, record => Assert.Equal("en", record.GetProperty("language").GetString()));
    }

    [Fact]
    public async Task CodeBuiltConfig_SiteLanguageComesFromSiteLanguages()
    {
        // The config is built in code, so ConfigLoader never derives the language; the engine must.
        using var site = EnglishSite(LanguagesYaml);
        var config = site.Config with
        {
            Site = site.Config.Site with { Language = "zh-CN", Languages = ["en", "fr"], DefaultLanguage = "fr" }
        };

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false, MetricsPath = "metrics.json" });

        using var metrics = JsonDocument.Parse(File.ReadAllText(site.Sandbox.PathFor("metrics.json")));
        Assert.Equal("fr", metrics.RootElement.GetProperty("site").GetProperty("language").GetString());
    }
}
