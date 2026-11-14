using System.Reflection;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine;
using Bukit.Engine.Tests.Infrastructure;
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

    [Fact]
    public async Task Create_CodeBuiltSingleEntryLanguages_UsesThatLanguageForUndeterminedPages()
    {
        // A config built in code with one entry in site.languages and no site.language: pages
        // without a language belong to that language, so same-named files collide as they do
        // in a loaded config and fall back to path slugs.
        using var sandbox = new SiteSandbox();
        sandbox.Write("content/a/foo.md", "---\ntitle: Foo A\nlanguage: en\n---\nFirst.\n");
        sandbox.Write("content/b/foo.md", "---\ntitle: Foo B\n---\nSecond.\n");
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/", Languages = ["en"] },
            Content = ContentConfigFactory.FromSources([TestContent.MarkdownSource()])
        };

        var provider = new DefaultContentProviderFactory().Create(config, sandbox.Root, false, new ConsoleLogger(LogLevel.Error));
        var result = await provider.LoadRawAsync();

        Assert.Equal(["a/foo", "b/foo"], result.Documents.Select(document => document.Slug).Order(StringComparer.Ordinal));
    }
}
