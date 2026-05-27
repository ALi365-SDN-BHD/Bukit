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
            Content = new ContentConfig
            {
                Provider = "markdown",
                Sources = new List<ContentSourceConfig>
                {
                    new ContentSourceConfig { Type = "markdown", Name = "content" }
                }
            }
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
            Content = new ContentConfig
            {
                Provider = "markdown",
                Sources = new List<ContentSourceConfig>
                {
                    new ContentSourceConfig { Type = "markdown", Name = "content" },
                    new ContentSourceConfig { Type = "markdown", Name = "docs" }
                }
            }
        };
        var logger = new ConsoleLogger(LogLevel.Debug);

        var provider = factory.Create(config, "/tmp/test", false, logger);

        Assert.NotNull(provider);
    }

    [Fact]
    public void Create_WithEmptySources_ReturnsProvider()
    {
        var factory = new DefaultContentProviderFactory();
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = new ContentConfig
            {
                Provider = "markdown",
                Sources = new List<ContentSourceConfig>()
            }
        };
        var logger = new ConsoleLogger(LogLevel.Debug);

        var provider = factory.Create(config, "/tmp/test", false, logger);

        Assert.NotNull(provider);
    }

    [Fact]
    public void Create_WithCiFlag_ReturnsProvider()
    {
        var factory = new DefaultContentProviderFactory();
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = new ContentConfig
            {
                Provider = "markdown",
                Sources = new List<ContentSourceConfig>
                {
                    new ContentSourceConfig { Type = "markdown", Name = "content" }
                }
            }
        };
        var logger = new ConsoleLogger(LogLevel.Debug);

        var provider = factory.Create(config, "/tmp/test", true, logger);

        Assert.NotNull(provider);
    }
}
