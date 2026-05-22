using Bukit.Config;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ConfigApplierTests
{
    private static AppConfig DefaultConfig() => new()
    {
        Site = new SiteConfig
        {
            Name = "test",
            Title = "Test",
            BaseUrl = "/",
            OutputPathEncoding = "none",
            Language = "en",
            Timezone = "Asia/Shanghai",
            SitemapMode = "split",
            RssMode = "split",
            SearchMode = "split",
            PluginFailMode = "strict",
            DeriveConflictPolicy = "fail"
            // DESKTOP-REMOVED: ExternalAssemblyTrustMode disabled (AOT-only).
            // ExternalAssemblyTrustMode = "warn"
        },
        Build = new BuildConfig
        {
            Output = "dist",
            Clean = true,
            Draft = false,
            ListPageContentMode = "auto"
        },
        Content = new ContentConfig
        {
            Provider = "markdown",
            Markdown = new MarkdownConfig()
        }
    };

    [Fact]
    public void Apply_BaseUrlOverride_UpdatesSiteBaseUrl()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { BaseUrl = "/blog/" };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.Equal("/blog/", result.Site.BaseUrl);
    }

    [Fact]
    public void Apply_OutputOverride_UpdatesBuildOutput()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { Output = "output" };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.Equal("output", result.Build.Output);
    }

    [Fact]
    public void Apply_CleanOverrideTrue_UpdatesBuildClean()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { Clean = true };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.True(result.Build.Clean);
    }

    [Fact]
    public void Apply_CleanOverrideFalse_UpdatesBuildClean()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { Clean = false };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.False(result.Build.Clean);
    }

    [Fact]
    public void Apply_DraftOverrideTrue_UpdatesBuildDraft()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { Draft = true };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.True(result.Build.Draft);
    }

    [Fact]
    public void Apply_DraftOverrideFalse_UpdatesBuildDraft()
    {
        var config = DefaultConfig() with
        {
            Build = DefaultConfig().Build with { Draft = true }
        };
        var overrides = new ConfigOverrides { Draft = false };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.False(result.Build.Draft);
    }

    [Fact]
    public void Apply_NoOverrides_ConfigUnchanged()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides();

        var result = ConfigApplier.Apply(config, overrides);

        Assert.Equal(config.Site.BaseUrl, result.Site.BaseUrl);
        Assert.Equal(config.Build.Output, result.Build.Output);
        Assert.Equal(config.Build.Clean, result.Build.Clean);
        Assert.Equal(config.Build.Draft, result.Build.Draft);
    }

    [Fact]
    public void Apply_PartialOverrides_OnlySpecifiedFieldsChanged()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { BaseUrl = "/blog/" };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.Equal("/blog/", result.Site.BaseUrl);
        Assert.Equal(config.Build.Output, result.Build.Output);
        Assert.Equal(config.Build.Clean, result.Build.Clean);
        Assert.Equal(config.Build.Draft, result.Build.Draft);
    }

    [Fact]
    public void Apply_BaseUrlEmpty_DoesNotOverride()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { BaseUrl = "" };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.Equal(config.Site.BaseUrl, result.Site.BaseUrl);
    }

    [Fact]
    public void Apply_BaseUrlWhitespace_DoesNotOverride()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { BaseUrl = "   " };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.Equal(config.Site.BaseUrl, result.Site.BaseUrl);
    }

    [Fact]
    public void Apply_OutputEmpty_DoesNotOverride()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { Output = "" };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.Equal(config.Build.Output, result.Build.Output);
    }

    [Fact]
    public void Apply_OutputWhitespace_DoesNotOverride()
    {
        var config = DefaultConfig();
        var overrides = new ConfigOverrides { Output = "   " };

        var result = ConfigApplier.Apply(config, overrides);

        Assert.Equal(config.Build.Output, result.Build.Output);
    }
}
