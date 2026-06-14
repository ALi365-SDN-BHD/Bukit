using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ConfigValidatorExtendedTests
{
    private static AppConfig CreateMinimalConfig(Action<SiteConfig>? siteOverride = null, Action<ContentConfig>? contentOverride = null)
    {
        var site = new SiteConfig { Name = "test", Title = "Test" };
        var content = ContentConfigFactory.SingleMarkdown();

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
            Content = ContentConfigFactory.SingleMarkdown(),
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
            Content = ContentConfigFactory.SingleMarkdown(),
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
            Content = ContentConfigFactory.SingleMarkdown(dir: "../../../etc")
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
            Content = ContentConfigFactory.SingleMarkdown(),
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
                Content = ContentConfigFactory.FromSources(
                    [
                        new ContentSourceConfig
                        {
                            Type = "notion",
                            Name = "page",
                            Collection = "page",
                            Notion = new NotionConfig { DatabaseId = "abc", PageSize = 200 }
                        }
                    ])
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
            Content = ContentConfigFactory.SingleMarkdown()
        };

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_MediaDownloadDirWithTraversal_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = ContentConfigFactory.SingleMarkdown(
                media: new MediaConfig { DownloadDir = "../../../tmp" })
        };

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Fact]
    public void ValidateThemeYaml_NoFile_ReturnsIssues()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var result = ConfigValidator.ValidateThemeYaml(tempDir);
        Assert.NotEmpty(result);
        Assert.Contains(result, w => w.Contains("theme.yaml not found"));
    }

    [Fact]
    public void ValidateThemeYaml_ValidYaml_ReturnsEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
name: test-theme
version: 1.0.0
engine: bukit
description: A test
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Empty(result!);
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }

    [Fact]
    public void ValidateThemeYaml_MissingName_ReturnsWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
version: 1.0.0
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Contains(result!, w => w.Contains("name"));
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }

    [Fact]
    public void ValidateThemeYaml_InvalidVersion_ReturnsWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
name: test
version: not-a-version
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Contains(result!, w => w.Contains("version"));
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }

    [Fact]
    public void ValidateThemeYaml_RejectsUnknownRootField()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
name: test
version: 1.0.0
engine: bukit
requires_bukit: ^2.0.0
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Contains(result!, w => w.Contains("requires_bukit"));
            Assert.Contains(result!, w => w.Contains("unknown field"));
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }

    [Fact]
    public void ValidateThemeYaml_RejectsUnknownSectionField()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
name: test
version: 1.0.0
engine: bukit
sections:
  hero:
    template: sections/hero.html
    unknown: should-fail
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Contains(result!, w => w.Contains("theme.yaml.sections.hero"));
            Assert.Contains(result!, w => w.Contains("unknown field"));
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }

    [Fact]
    public void ValidateThemeYaml_RejectsUnknownComponentField()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
name: test
version: 1.0.0
engine: bukit
components:
  card:
    template: components/card.html
    bad: value
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Contains(result!, w => w.Contains("theme.yaml.components.card"));
            Assert.Contains(result!, w => w.Contains("unknown field"));
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }

    [Fact]
    public void ValidateThemeYaml_RejectsInvalidTemplatePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
name: test
version: 1.0.0
engine: bukit
sections:
  hero:
    template: ../../malicious.html
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Contains(result!, w => w.Contains("has path traversal") || w.Contains("outside theme scope"));
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }

    [Fact]
    public void ValidateThemeYaml_RejectsInvalidExtendsName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
name: test
version: 1.0.0
engine: bukit
extends: ../invalid
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Contains(result!, w => w.Contains("extends") && w.Contains("invalid"));
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }

    [Fact]
    public void ValidateThemeYaml_RejectsNonStringExtends()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
name: test
version: 1.0.0
engine: bukit
extends:
  - parent
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Contains(result!, w => w.Contains("theme.yaml.extends") && w.Contains("must be a string"));
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }

    [Fact]
    public void ValidateThemeYaml_RejectsMissingParentTheme()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), """
name: test
version: 1.0.0
engine: bukit
extends: parent
""");

            var result = ConfigValidator.ValidateThemeYaml(tempDir);
            Assert.NotNull(result);
            Assert.Contains(result!, w => w.Contains("not found") || w.Contains("parent theme"));
        }
        finally { TestCleanup.DeleteDirectory(tempDir, true); }
    }
}
