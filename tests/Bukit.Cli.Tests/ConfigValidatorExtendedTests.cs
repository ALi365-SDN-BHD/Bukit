using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ConfigValidatorExtendedTests
{
    private static AppConfig CreateMinimalConfig(Action<SiteConfig>? siteOverride = null, Action<ContentConfig>? contentOverride = null)
    {
        var site = new SiteConfig { Name = "test", Title = "Test" };
        var content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } };

        if (siteOverride is not null)
        {
            var mutableSite = site with { };
            siteOverride(mutableSite);
            site = mutableSite;
        }

        return new AppConfig
        {
            Site = site,
            Content = content
        };
    }

    [Fact]
    public void Validate_ValidConfig_DoesNotThrow()
    {
        var config = CreateMinimalConfig();
        ConfigValidator.Validate(config);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("foo/../../../etc")]
    public void Validate_BuildOutputWithTraversal_Throws(string output)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } },
            Build = new BuildConfig { Output = output }
        };

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_AbsoluteBuildOutput_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } },
            Build = new BuildConfig { Output = "/var/www" }
        };

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_MarkdownDirWithTraversal_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = new ContentConfig
            {
                Provider = "markdown",
                Markdown = new MarkdownConfig { Dir = "../../../etc" }
            }
        };

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Theory]
    [InlineData("../theme")]
    [InlineData("foo/../../bar")]
    public void Validate_ThemeLayoutsWithTraversal_Throws(string layouts)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } },
            Theme = new ThemeConfig { Layouts = layouts }
        };

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_NotionPageSizeOutOfRange_Throws()
    {
        Environment.SetEnvironmentVariable("NOTION_TOKEN", "test-token-for-validation");
        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test" },
                Content = new ContentConfig
                {
                    Provider = "notion",
                    Notion = new NotionConfig { DatabaseId = "abc", PageSize = 200 }
                }
            };

            Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTION_TOKEN", null);
        }
    }

    [Fact]
    public void Validate_InvalidTimezone_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", Timezone = "Invalid/Timezone_XYZ" },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } }
        };

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_MediaDownloadDirWithTraversal_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = new ContentConfig
            {
                Provider = "markdown",
                Markdown = new MarkdownConfig { Dir = "content" },
                Media = new MediaConfig { DownloadDir = "../../../tmp" }
            }
        };

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }
}
