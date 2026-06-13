using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Markdown;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PagesByIdDataPluginTests
{
    [Fact]
    public void DerivePages_PopulatesSiteDataPagesById()
    {
        var item = ContentDocument.Create(
            id: "page-1",
            title: "Hello",
            slug: "hello",
            publishAt: new DateTimeOffset(2026, 02, 08, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>hi</p>",
            fields: ContentFieldReader.WithValues(new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["tags"] = new ContentField("list", new List<string> { "a", "b" })
            }, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["summary"] = "S"
            }));

        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (item, new RouteInfo("/blog/hello/", "blog/hello/index.html", "pages/post.html"))
        };

        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "t",
                Title = "t",
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["taxonomy"] = new PluginToggleConfig { Enabled = false },
                    ["sitemap"] = new PluginToggleConfig { Enabled = false },
                    ["feed"] = new PluginToggleConfig { Enabled = false },
                    ["search-index"] = new PluginToggleConfig { Enabled = false },
                    ["pagination"] = new PluginToggleConfig { Enabled = false },
                    ["archive"] = new PluginToggleConfig { Enabled = false },
                    ["pages-index"] = new PluginToggleConfig { Enabled = true }
                }
            },
            Content = TestContent.Markdown()
        };

        var ctx = new BuildContext
        {
            Config = config,
            RootDir = "C:\\",
            OutputDir = "C:\\out",
            BaseUrl = "/",
            LayoutsDir = "C:\\layouts",
            RoutedDocuments = routed.ToRoutedDocuments(),
            ContentGraph = new CanonicalContentGraph(
            [
                new ContentRecord(
                    new ContentIdentity("page-1", "hello", "hello", "post", "published"),
                    new ContentPresentation("Hello", "Canonical summary", "<p>hi</p>", "en", []),
                    new ContentClassification("post", "post", [], ["a", "b"]),
                    new ContentOwnership(null, null, null, null),
                    new ContentLifecycle(item.PublishAt, null, null, null),
                    new ProvenanceRecord(null, null, [], [], null),
                    new TrustMetadata(null, "published", []),
                    [],
                    [],
                    [])
            ], []),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        PluginRunner.RunDerivePages(ctx);

        Assert.True(ctx.Data.TryGetValue("pages_by_id", out var indexObj));
        var index = Assert.IsType<Dictionary<string, object>>(indexObj);

        Assert.True(index.TryGetValue("page-1", out var pageObj));
        var page = Assert.IsType<Dictionary<string, object>>(pageObj);
        Assert.Equal("Hello", page["title"]);
        Assert.Equal("/blog/hello/", page["url"]);
        Assert.Equal("Canonical summary", page["summary"]);
    }

    [Fact]
    public void DerivePages_ShouldUseFirstCanonicalRecord_WhenGraphContainsDuplicateI18nIds()
    {
        var item = ContentDocument.Create(
            id: "greeting",
            title: "你好",
            slug: "greeting",
            publishAt: new DateTimeOffset(2026, 06, 05, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>hi</p>",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase));
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = TestContent.Markdown()
            },
            RootDir = "C:\\",
            OutputDir = "C:\\out",
            BaseUrl = "/",
            LayoutsDir = "C:\\layouts",
            RoutedDocuments = new[] { (item, new RouteInfo("/zh-CN/pages/greeting/", "zh-CN/pages/greeting/index.html", "pages/page.html")) }.ToRoutedDocuments(),
            ContentGraph = new CanonicalContentGraph(
            [
                CreateRecord("greeting", "zh-CN", "中文摘要"),
                CreateRecord("greeting", "en-US", "English summary")
            ], []),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugin = new PagesIndexPlugin();
        plugin.DerivePages(ctx);

        var index = Assert.IsType<Dictionary<string, object>>(ctx.Data["pages_by_id"]);
        var page = Assert.IsType<Dictionary<string, object>>(index["greeting"]);
        Assert.Equal("中文摘要", page["summary"]);
    }

    [Fact]
    public async Task DerivePages_WithMarkdownProviderOutput_PopulatesIndex()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var contentDir = Path.Combine(root, "content");
        Directory.CreateDirectory(contentDir);

        var mdPath = Path.Combine(contentDir, "hello.md");
        await File.WriteAllTextAsync(mdPath, """
                                             ---
                                             title: Hello
                                             slug: hello
                                             type: post
                                             collection: post
                                             markdown:
                                               dir: content
                                             ---
                                             Hi
                                             """);

        var provider = new MarkdownFolderProvider(new MarkdownFolderProviderOptions(contentDir));
        var loadResult = await provider.LoadRawAsync();
        var documents = ContentDocumentNormalizer.ToDocuments(loadResult.Documents);
        Assert.Single(documents);

        var document = documents[0];
        var route = RouteGenerator.Generate(document, collections: new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new RouteGenerator.CollectionRouteRule("/blog/{slug}/", string.Empty)
        });

        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["post"] = new CollectionConfig { Permalink = "/blog/{slug}/" }
                    }
                },
                Content = TestContent.Markdown()
            },
            RootDir = root,
            OutputDir = Path.Combine(root, "out"),
            BaseUrl = "/",
            LayoutsDir = Path.Combine(root, "layouts"),
            RoutedDocuments = new[] { new RoutedContentDocument(document, route) },
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugin = new Bukit.Engine.Plugins.BuiltIn.PagesIndexPlugin();
        plugin.DerivePages(ctx);

        var index = Assert.IsType<Dictionary<string, object>>(ctx.Data["pages_by_id"]);
        var page = Assert.IsType<Dictionary<string, object>>(index["hello"]);
        Assert.Equal("/blog/hello/", page["url"]);
        Assert.Equal("Hello", page["title"]);
    }

    private static ContentRecord CreateRecord(string id, string language, string summary)
        => new(
            new ContentIdentity(id, id, id, "page", "published"),
            new ContentPresentation("Greeting", summary, "<p>body</p>", language, []),
            new ContentClassification("page", "page", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(new DateTimeOffset(2026, 06, 05, 0, 0, 0, TimeSpan.Zero), null, null, null),
            new ProvenanceRecord(null, null, [], [], null),
            new TrustMetadata(null, "published", []),
            [],
            [],
            []);

    [Fact]
    public void DerivePages_WhenConfigured_ResolvesNotionRelationIdsIntoIndex()
    {
        var oldToken = Environment.GetEnvironmentVariable("NOTION_TOKEN");
        Environment.SetEnvironmentVariable("NOTION_TOKEN", "secret_dummy");
        try
        {
            var item = ContentDocument.Create(
                id: "page-1",
                title: "Hello",
                slug: "hello",
                publishAt: new DateTimeOffset(2026, 02, 08, 0, 0, 0, TimeSpan.Zero),
                contentHtml: "<p>hi</p>",
                fields: ContentFieldReader.WithValues(new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["related_posts"] = new ContentField("list", new List<string> { "missing-1" })
                }, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "post", ["collection"] = "post" }));

            var routed = new List<(ContentDocument Item, RouteInfo Route)>
            {
                (item, new RouteInfo("/blog/hello/", "blog/hello/index.html", "pages/post.html"))
            };

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["pages-index"] = new PluginToggleConfig { Enabled = true }
                    }
                },
                Content = TestContent.Notion() with { Media = new MediaConfig { DownloadToLocal = false } },
                Theme = new ThemeConfig
                {
                    Params = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["pages_index"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["resolve_notion"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["enabled"] = true,
                                ["field_keys"] = new List<object> { "related_posts" },
                                ["max_items"] = 10,
                                ["concurrency"] = 1
                            }
                        }
                    }
                }
            };

            var ctx = new BuildContext
            {
                Config = config,
                RootDir = "C:\\",
                OutputDir = "C:\\out",
                BaseUrl = "/",
                LayoutsDir = "C:\\layouts",
                RoutedDocuments = routed.ToRoutedDocuments(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            var plugin = new Bukit.Engine.Plugins.BuiltIn.PagesIndexPlugin(new FakeFetcher());
            plugin.DerivePages(ctx);

            var index = Assert.IsType<Dictionary<string, object>>(ctx.Data["pages_by_id"]);
            var resolved = Assert.IsType<Dictionary<string, object>>(index["missing-1"]);
            Assert.Equal("Missing Title", resolved["title"]);
            Assert.Equal(string.Empty, resolved["url"]);
            Assert.Equal("https://www.notion.so/missing-1", resolved["external_url"]);
            var fields = Assert.IsType<Dictionary<string, object>>(resolved["fields"]);
            var cover = Assert.IsType<Dictionary<string, object>>(fields["cover"]);
            Assert.Equal("text", cover["type"]);
            Assert.Equal("https://img.example/1.jpg", cover["value"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTION_TOKEN", oldToken);
        }
    }

    [Fact]
    public void DerivePages_WhenCacheReadonly_UsesCacheAndDoesNotCallFetcher()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var cachePath = Path.Combine(root, ".cache", "notion", "pages-index.json");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        File.WriteAllText(cachePath, """
                                     {
                                       "missing-1": {
                                         "id": "missing-1",
                                         "title": "Cached",
                                         "url": "",
                                         "external_url": "https://www.notion.so/missing-1",
                                         "slug": "cached",
                                         "type": "notion",
                                         "publish_date": null,
                                         "summary": "",
                                         "fields": {}
                                       }
                                     }
                                     """);

        var item = ContentDocument.Create(
            id: "page-1",
            title: "Hello",
            slug: "hello",
            publishAt: new DateTimeOffset(2026, 02, 08, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>hi</p>",
            fields: ContentFieldReader.WithValues(new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["related_posts"] = new ContentField("list", new List<string> { "missing-1" })
            }, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "post", ["collection"] = "post" }));

        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = TestContent.Notion(),
                Theme = new ThemeConfig
                {
                    Params = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["pages_index"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["resolve_notion"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["enabled"] = true,
                                ["field_keys"] = new List<object> { "related_posts" },
                                ["cache_mode"] = "readonly",
                                ["cache_path"] = cachePath
                            }
                        }
                    }
                }
            },
            RootDir = root,
            OutputDir = Path.Combine(root, "out"),
            BaseUrl = "/",
            LayoutsDir = Path.Combine(root, "layouts"),
            RoutedDocuments = new List<(ContentDocument Item, RouteInfo Route)>
            {
                (item, new RouteInfo("/blog/hello/", "blog/hello/index.html", "pages/post.html"))
            }.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugin = new Bukit.Engine.Plugins.BuiltIn.PagesIndexPlugin(new ThrowingFetcher());
        plugin.DerivePages(ctx);

        var index = Assert.IsType<Dictionary<string, object>>(ctx.Data["pages_by_id"]);
        var resolved = Assert.IsType<Dictionary<string, object>>(index["missing-1"]);
        Assert.Equal("Cached", resolved["title"]);
    }

    private sealed class FakeFetcher : Bukit.Engine.Plugins.BuiltIn.INotionPageFetcher
    {
        public Task<Bukit.Engine.Plugins.BuiltIn.NotionFetchedPage?> FetchAsync(
            Bukit.Content.Notion.NotionApiClient client,
            string pageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Bukit.Engine.Plugins.BuiltIn.NotionFetchedPage?>(
                new Bukit.Engine.Plugins.BuiltIn.NotionFetchedPage(
                    pageId,
                    "Missing Title",
                    "missing-title",
                    $"https://www.notion.so/{pageId}",
                    new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["cover"] = new ContentField("text", "https://img.example/1.jpg")
                    }));
        }
    }

    private sealed class ThrowingFetcher : Bukit.Engine.Plugins.BuiltIn.INotionPageFetcher
    {
        public Task<Bukit.Engine.Plugins.BuiltIn.NotionFetchedPage?> FetchAsync(
            Bukit.Content.Notion.NotionApiClient client,
            string pageId,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Fetcher should not be called when cache_mode=readonly");
        }
    }
}
