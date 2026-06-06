using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class FeedPluginTests
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

    private static ContentDocument CreateDocument(string id, string title, string collection, bool excludeFromFeed)
    {
        var record = new ContentRecord(
            new ContentIdentity(id, id, id, "post", "published"),
            new ContentPresentation(title, "Summary " + title, $"<p>{title}</p>", "en", []),
            new ContentClassification("post", collection, [], []),
            new ContentOwnership("Ali", null, null, null),
            new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
            new ProvenanceRecord("markdown", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [],
            [],
            []);

        return new ContentDocument(
            record,
            new ContentBodyRef($"<p>{title}</p>", null, null, null),
            new ContentRoutePolicy(null, null, null, null, collection),
            new ContentPublishPolicy(false, false, false, excludeFromFeed, false, false, false),
            new Dictionary<string, ContentField>(),
            Array.Empty<ContentDiagnostic>());
    }

    [Fact]
    public void AfterBuild_CreatesFeedFile()
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

            var plugin = new FeedPlugin();
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
    public void AfterBuild_WithRoutedDocuments_UsesTypedFeedProjection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_rss_docs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var route1 = CreateRoute("/included/", "included/index.html");
            var route2 = CreateRoute("/excluded/", "excluded/index.html");
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Typed Blog",
                    Url = "https://example.com",
                    Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["post"] = new() { Permalink = "/{slug}/", Output = new() { Rss = true } }
                    }
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
                RoutedDocuments = new[]
                {
                    (CreateDocument("included", "Included", "post", excludeFromFeed: false), route1),
                    (CreateDocument("excluded", "Excluded", "post", excludeFromFeed: true), route2)
                },
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
                SeoIndex = new Dictionary<string, SeoIndexEntry>
                {
                    ["included/index.html"] = new(route1, "https://example.com/included/", null, true, DateTimeOffset.UnixEpoch, "included", "post"),
                    ["excluded/index.html"] = new(route2, "https://example.com/excluded/", null, true, DateTimeOffset.UnixEpoch, "excluded", "post")
                }
            };

            var plugin = new FeedPlugin();
            plugin.AfterBuild(context);

            var rss = File.ReadAllText(Path.Combine(tempDir, "rss.xml"));
            Assert.Contains("<title>Included</title>", rss, StringComparison.Ordinal);
            Assert.DoesNotContain("<title>Excluded</title>", rss, StringComparison.Ordinal);
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

            var plugin = new FeedPlugin();
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

            var plugin = new FeedPlugin();
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

            var plugin = new FeedPlugin();
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
