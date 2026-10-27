using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigPhase2ValidatorTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }

    private string WriteConfig(string siteExtra, string additional = "")
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-config-phase2-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, $"""
            site:
              name: myblog
              title: My Blog
            {siteExtra}
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            {additional}
            """);
        _tempFiles.Add(path);
        return path;
    }

    private static ConfigException ValidateFails(string path)
    {
        var config = ConfigLoader.Load(path);
        return Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Fact]
    public void Load_MinimalConfig_ShouldApplyDocumentedDefaults()
    {
        var config = ConfigLoader.Load(WriteConfig(""));
        ConfigValidator.Validate(config);

        Assert.Equal("/", config.Site.BaseUrl);
        Assert.Equal("zh-CN", config.Site.Language);
        Assert.Null(config.Site.Languages);
        Assert.Null(config.Site.DefaultLanguage);
        Assert.Equal("split", config.Site.SitemapMode);
        Assert.Equal("Asia/Shanghai", config.Site.Timezone);
        Assert.Equal("split", config.Site.Feed.Mode);
        Assert.Equal(new[] { "rss" }, config.Site.Feed.Formats);
        Assert.Equal(20, config.Site.Feed.Limit);
        Assert.Equal("feed", config.Site.Feed.Path);
        Assert.Equal("split", config.Site.Search.Mode);
        Assert.Equal(8000, config.Site.Search.MaxContentLength);
        Assert.Equal("dist", config.Build.Output);
        Assert.True(config.Build.Clean);
        Assert.Equal("auto", config.Build.ListPageContentMode);
        Assert.Equal("warn", config.Build.SchemaFailMode);
        Assert.Equal("size-time", config.Build.FingerprintMode);
        Assert.Equal(1, config.Build.LanguageJobs);
        Assert.True(config.Build.Report.Enabled);
        Assert.Equal("auto", config.Build.Report.SecurityFailMode);
        Assert.Equal("layouts", config.Theme.Layouts);
        Assert.Equal("assets", config.Theme.Assets);
        Assert.Equal("static", config.Theme.Static);
        Assert.Equal("off", config.Theme.ComponentValidation);
        Assert.Equal("info", config.Logging.Level);
        Assert.Equal("assets/uploads", config.Content.Media.DownloadDir);
        Assert.True(config.Content.Media.BlockPrivateNetworks);
    }

    [Fact]
    public void Validate_DuplicateLanguages_ShouldFail()
    {
        var ex = ValidateFails(WriteConfig("""
              languages: [en, EN]
            """));

        Assert.Contains("site.languages has duplicate language", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_UnsafeLanguageTag_ShouldFail()
    {
        var ex = ValidateFails(WriteConfig("""
              languages: ["en/US"]
            """));

        Assert.Contains("safe alphanumeric subtags", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DefaultLanguageOutsideLanguages_ShouldFail()
    {
        var ex = ValidateFails(WriteConfig("""
              languages: [en, zh-CN]
              defaultLanguage: ms
            """));

        Assert.Contains("defaultLanguage must be included in site.languages", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_HomeTitleTemplateWithUnsupportedPlaceholder_ShouldFail()
    {
        var ex = ValidateFails(WriteConfig("""
              seo:
                homeTitleTemplate: "{author}"
            """));

        Assert.Contains("site.seo.homeTitleTemplate contains unsupported placeholder {author}", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PageTitleTemplateWithoutPagePlaceholder_ShouldFail()
    {
        var ex = ValidateFails(WriteConfig("""
              seo:
                pageTitleTemplate: "{siteTitle}"
            """));

        Assert.Contains("site.seo.pageTitleTemplate must contain {pageTitle}", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_InvalidTimezone_ShouldFail()
    {
        var ex = ValidateFails(WriteConfig("""
              timezone: Mars/Olympus
            """));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("site.timezone 'Mars/Olympus' is not a valid time zone identifier", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("layouts")]
    [InlineData("assets")]
    [InlineData("static")]
    [InlineData("name")]
    public void Validate_ThemePathTraversal_ShouldFail(string field)
    {
        var ex = ValidateFails(WriteConfig("", $"theme:\n  {field}: ../outside\n"));

        Assert.Equal(DiagnosticCode.ConfigPathTraversal, ex.Code);
        Assert.Contains($"theme.{field} must not contain '..' path traversal segments", ex.Message, StringComparison.Ordinal);
    }
}
