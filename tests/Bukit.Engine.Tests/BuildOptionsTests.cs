using Bukit.Engine;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildOptionsTests
{
    [Fact]
    public void Defaults_AreSet()
    {
        var opts = new BuildOptions();

        Assert.Equal("dist", opts.OutputDir);
        Assert.Null(opts.SiteUrl);
        Assert.Equal("Bukit", opts.SiteTitle);
        Assert.Equal("/", opts.BaseUrl);
        Assert.Equal("none", opts.OutputPathEncoding);
        Assert.Null(opts.AssetsDir);
        Assert.True(opts.Clean);
        Assert.False(opts.IsCI);
        Assert.True(opts.GenerateSitemap);
        Assert.True(opts.GenerateRss);
    }

    [Fact]
    public void WithValues_PropertiesSet()
    {
        var opts = new BuildOptions
        {
            OutputDir = "public",
            SiteUrl = "https://example.com",
            SiteTitle = "My Site",
            BaseUrl = "/blog",
            Clean = false,
            IsCI = true,
            GenerateSitemap = false,
            GenerateRss = false
        };

        Assert.Equal("public", opts.OutputDir);
        Assert.Equal("https://example.com", opts.SiteUrl);
        Assert.Equal("My Site", opts.SiteTitle);
        Assert.Equal("/blog", opts.BaseUrl);
        Assert.False(opts.Clean);
        Assert.True(opts.IsCI);
        Assert.False(opts.GenerateSitemap);
        Assert.False(opts.GenerateRss);
    }

    [Fact]
    public void WithValues_AssetsDir()
    {
        var opts = new BuildOptions { AssetsDir = "assets" };

        Assert.Equal("assets", opts.AssetsDir);
    }

    [Fact]
    public void WithValues_OutputPathEncoding()
    {
        var opts = new BuildOptions { OutputPathEncoding = "unicode" };

        Assert.Equal("unicode", opts.OutputPathEncoding);
    }

    [Fact]
    public void RecordSemantics_Equality()
    {
        var a = new BuildOptions { OutputDir = "dist", SiteTitle = "Site" };
        var b = new BuildOptions { OutputDir = "dist", SiteTitle = "Site" };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void RecordSemantics_Inequality()
    {
        var a = new BuildOptions { OutputDir = "dist" };
        var b = new BuildOptions { OutputDir = "public" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void RecordClone_ProducesCopy()
    {
        var original = new BuildOptions { OutputDir = "dist", Clean = false };
        var cloned = original with { OutputDir = "public" };

        Assert.Equal("public", cloned.OutputDir);
        Assert.False(cloned.Clean);
        Assert.Equal("dist", original.OutputDir);
    }
}
