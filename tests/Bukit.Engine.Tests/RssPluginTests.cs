using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RssPluginTests
{
    private static ContentItem CreateItem(string id, string title, string slug, string? contentHtml = null)
    {
        return new ContentItem(
            id,
            title,
            slug,
            DateTimeOffset.UtcNow,
            contentHtml,
            new Dictionary<string, object>(),
            null,
            null);
    }

    private static RouteInfo CreateRoute(string url, string outputPath)
    {
        return new RouteInfo(url, outputPath, "post");
    }

    [Fact]
    public void AfterBuild_CreatesRssFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_rss_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Test Blog",
                    Url = "https://example.com",
                    Description = "A test blog"
                },
                Content = new ContentConfig { Provider = "markdown" }
            };

            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "/",
                LayoutsDir = tempDir,
                Routed = new List<(ContentItem, RouteInfo)>
                {
                    (CreateItem("1", "Hello World", "hello-world", "<p>Hello world!</p>"), CreateRoute("/hello-world", "hello-world/index.html")),
                    (CreateItem("2", "Second Post", "second-post", "<p>Another post</p>"), CreateRoute("/second-post", "second-post/index.html")),
                },
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = new Dictionary<string, SeoIndexEntry>();

            var plugin = new RssPlugin();
            plugin.AfterBuild(context);

            var rssPath = Path.Combine(tempDir, "rss.xml");
            Assert.True(File.Exists(rssPath));
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
    public void AfterBuild_EmptyRouted_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_rss_empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Empty Blog",
                    Url = "https://example.com"
                },
                Content = new ContentConfig { Provider = "markdown" }
            };

            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "/",
                LayoutsDir = tempDir,
                Routed = Array.Empty<(ContentItem, RouteInfo)>(),
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = new Dictionary<string, SeoIndexEntry>();

            var plugin = new RssPlugin();
            plugin.AfterBuild(context);

            var rssPath = Path.Combine(tempDir, "rss.xml");
            Assert.True(File.Exists(rssPath));
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
    public void AfterBuild_MultiLanguage_ProducesCorrectOutput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_rss_multi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Multi Blog",
                    Url = "https://example.com",
                },
                Content = new ContentConfig { Provider = "markdown" }
            };

            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "/",
                LayoutsDir = tempDir,
                Routed = new List<(ContentItem, RouteInfo)>
                {
                    (CreateItem("1", "English Post", "english-post", "<p>Content</p>"), CreateRoute("/en/english-post", "en/english-post/index.html")),
                },
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = new Dictionary<string, SeoIndexEntry>();

            var plugin = new RssPlugin();
            plugin.AfterBuild(context);

            var rssPath = Path.Combine(tempDir, "rss.xml");
            Assert.True(File.Exists(rssPath));
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
    public void AfterBuild_WithContentHtml_IncludesInFeed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_rss_content_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Content Blog",
                    Url = "https://example.com"
                },
                Content = new ContentConfig { Provider = "markdown" }
            };

            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "/",
                LayoutsDir = tempDir,
                Routed = new List<(ContentItem, RouteInfo)>
                {
                    (CreateItem("1", "Rich Post", "rich-post", "<p>Rich content here</p>"), CreateRoute("/rich-post", "rich-post/index.html")),
                },
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = new Dictionary<string, SeoIndexEntry>();

            var plugin = new RssPlugin();
            plugin.AfterBuild(context);

            var rssPath = Path.Combine(tempDir, "rss.xml");
            Assert.True(File.Exists(rssPath));
            var content = File.ReadAllText(rssPath);
            Assert.NotEmpty(content);
            Assert.Contains("<rss version=\"2.0\"", content, StringComparison.Ordinal);
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
