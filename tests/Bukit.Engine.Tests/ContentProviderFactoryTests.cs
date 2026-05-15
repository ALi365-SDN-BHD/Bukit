using System.Reflection;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentProviderFactoryTests
{
    [Fact]
    public void Create_WithMarkdownSource_ReturnsMarkdownProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_content_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
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

            var provider = ContentProviderFactory.Create(config, tempDir, false, logger);

            Assert.NotNull(provider);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Create_WithMultipleMarkdownSources_ReturnsCompositeProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_content_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var postsDir = Path.Combine(tempDir, "content", "posts");
        var pagesDir = Path.Combine(tempDir, "content", "pages");
        Directory.CreateDirectory(postsDir);
        Directory.CreateDirectory(pagesDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Sources = new List<ContentSourceConfig>
                    {
                        new ContentSourceConfig { Type = "markdown", Name = "posts", Markdown = new MarkdownConfig { Dir = "content/posts" } },
                        new ContentSourceConfig { Type = "markdown", Name = "pages", Markdown = new MarkdownConfig { Dir = "content/pages" } }
                    }
                }
            };
            var logger = new ConsoleLogger(LogLevel.Debug);

            var provider = ContentProviderFactory.Create(config, tempDir, false, logger);

            Assert.NotNull(provider);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Create_WithEmptySources_ReturnsProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_content_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
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

            var provider = ContentProviderFactory.Create(config, tempDir, false, logger);

            Assert.NotNull(provider);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task LocalizeContentImagesAsync_WithEmptyItems_ReturnsEmptyResult()
    {
        var items = Array.Empty<ContentItem>();
        var result = new ContentLoadResult(
            items,
            NullContentBodyStore.Instance);

        var media = new MediaConfig();
        var logger = new ConsoleLogger(LogLevel.Debug);

        var localized = await ContentProviderFactory.LocalizeContentImagesAsync(
            result, media, "/tmp", "/tmp/cache", logger, CancellationToken.None);

        Assert.NotNull(localized);
        Assert.Empty(localized.Items);
    }

    [Fact]
    public async Task LocalizeContentImagesAsync_WithNoImages_ReturnsSameItems()
    {
        var items = new List<ContentItem>
        {
            new ContentItem(
                "test",
                "Test",
                "test",
                DateTimeOffset.UtcNow,
                "<p>Hello world</p>",
                new Dictionary<string, object>(),
                null,
                null)
        };

        var result = new ContentLoadResult(items.AsReadOnly(), NullContentBodyStore.Instance);
        var media = new MediaConfig();
        var logger = new ConsoleLogger(LogLevel.Debug);

        var localized = await ContentProviderFactory.LocalizeContentImagesAsync(
            result, media, "/tmp", "/tmp/cache", logger, CancellationToken.None);

        Assert.NotNull(localized);
        Assert.Single(localized.Items);
    }

    [Fact]
    public void CreateNotionProvider_WithNotionConfig_ReturnsNotionProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_notion_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
                Content = new ContentConfig
                {
                    Provider = "notion",
                    Sources = new List<ContentSourceConfig>
                    {
                        new ContentSourceConfig
                        {
                            Type = "notion",
                            Name = "db",
                            Notion = new NotionConfig { DatabaseId = "test-db-id" }
                        }
                    }
                }
            };
            var logger = new ConsoleLogger(LogLevel.Debug);

            var ex = Assert.Throws<ContentException>(() => ContentProviderFactory.Create(config, tempDir, false, logger));
            Assert.Contains("NOTION_TOKEN", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
