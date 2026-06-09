using System.Reflection;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DefaultContentProviderFactoryTests
{
    [Fact]
    public void Create_WithContentSources_ReturnsContentProvider()
    {
        var factory = new DefaultContentProviderFactory();
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = ContentConfigFactory.FromSources(
            [
                TestContent.MarkdownSource()
            ])
        };
        var logger = new ConsoleLogger(LogLevel.Debug);

        var provider = factory.Create(config, "/tmp/test", false, logger);

        Assert.NotNull(provider);
    }

    [Fact]
    public void Create_WithMultipleSources_ReturnsCompositeProvider()
    {
        var factory = new DefaultContentProviderFactory();
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = ContentConfigFactory.FromSources(
            [
                TestContent.MarkdownSource(collection: "content"),
                TestContent.MarkdownSource(collection: "docs")
            ])
        };
        var logger = new ConsoleLogger(LogLevel.Debug);

        var provider = factory.Create(config, "/tmp/test", false, logger);

        Assert.NotNull(provider);
    }

    [Fact]
    public void Create_WithEmptySources_ThrowsConfigException()
    {
        var factory = new DefaultContentProviderFactory();
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = ContentConfigFactory.FromSources([])
        };
        var logger = new ConsoleLogger(LogLevel.Debug);

        var ex = Assert.Throws<ConfigException>(() => factory.Create(config, "/tmp/test", false, logger));
        Assert.Contains("content.sources is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithCiFlag_ReturnsProvider()
    {
        var factory = new DefaultContentProviderFactory();
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = ContentConfigFactory.FromSources(
            [
                TestContent.MarkdownSource()
            ])
        };
        var logger = new ConsoleLogger(LogLevel.Debug);

        var provider = factory.Create(config, "/tmp/test", true, logger);

        Assert.NotNull(provider);
    }
}
