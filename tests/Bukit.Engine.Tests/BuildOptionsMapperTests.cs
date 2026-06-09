using Bukit.Engine;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildOptionsMapperTests
{
    [Fact]
    public void ToAppConfig_MapsAllProperties()
    {
        var options = new BuildOptions
        {
            OutputDir = "public",
            SiteUrl = "https://mysite.com",
            SiteTitle = "My Site",
            BaseUrl = "/blog",
            Clean = false,
            IsCI = true,
            GenerateSitemap = false,
            GenerateRss = false,
            OutputPathEncoding = "unicode"
        };

        var config = BuildOptionsMapper.ToAppConfig(options, "public");

        Assert.Equal("My Site", config.Site.Name);
        Assert.Equal("My Site", config.Site.Title);
        Assert.Equal("https://mysite.com", config.Site.Url);
        Assert.Equal("/blog", config.Site.BaseUrl);
        Assert.False(config.Site.Seo.Enabled);
        Assert.Equal("en", config.Site.Language);
        Assert.Equal("unicode", config.Site.OutputPathEncoding);
        Assert.Equal("public", config.Build.Output);
        Assert.False(config.Build.Clean);
        var source = Assert.Single(config.Content.Sources!);
        Assert.Equal("markdown", source.Type);
        Assert.Equal("page", source.Collection);
    }

    [Fact]
    public void ToAppConfig_DefaultValues()
    {
        var options = new BuildOptions();

        var config = BuildOptionsMapper.ToAppConfig(options, "dist");

        Assert.Equal("Bukit", config.Site.Name);
        Assert.Equal("Bukit", config.Site.Title);
        Assert.Null(config.Site.Url);
        Assert.Equal("/", config.Site.BaseUrl);
        Assert.Equal("dist", config.Build.Output);
        Assert.True(config.Build.Clean);
        Assert.Equal("none", config.Site.OutputPathEncoding);
    }

    [Fact]
    public void ToAppConfig_WithSiteUrl()
    {
        var options = new BuildOptions { SiteUrl = "https://example.com" };

        var config = BuildOptionsMapper.ToAppConfig(options, "dist");

        Assert.Equal("https://example.com", config.Site.Url);
    }
}
