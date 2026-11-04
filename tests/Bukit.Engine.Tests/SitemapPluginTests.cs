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

public sealed class SitemapPluginTests
{
    [Fact]
    public void AfterBuild_RobotsNoindex_ExcludesUrlFromSitemap()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "index.html"), "<html><head></head></html>");
        Directory.CreateDirectory(Path.Combine(outDir, "blog"));
        File.WriteAllText(Path.Combine(outDir, "blog", "index.html"), "<html><head></head></html>");
        Directory.CreateDirectory(Path.Combine(outDir, "pages"));
        File.WriteAllText(Path.Combine(outDir, "pages", "index.html"), "<html><head></head></html>");

        var pageDir = Path.Combine(outDir, "pages", "a");
        Directory.CreateDirectory(pageDir);
        File.WriteAllText(Path.Combine(pageDir, "index.html"), "<html><head><meta name=\"robots\" content=\"noindex\" /></head></html>");

        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "a",
            publishAt: new DateTimeOffset(2024, 01, 02, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "");

        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "n", Title = "t", Url = "https://example.com" },
            Content = TestContent.Markdown()
        };

        var context = new BuildContext
        {
            RootDir = root,
            OutputDir = outDir,
            BaseUrl = "/",
            LayoutsDir = root,
            RoutedDocuments = new List<(ContentDocument Item, RouteInfo Route)>
            {
                (item, new RouteInfo("/pages/a/", "pages/a/index.html", "pages/page.html"))
            }.ToRoutedDocuments(),
            SeoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["pages/a/index.html"] = new(
                    new RouteInfo("/pages/a/", "pages/a/index.html", "pages/page.html"),
                    "https://example.com/pages/a/",
                    "noindex",
                    Indexable: false,
                    item.PublishAt,
                    item.Id,
                    "page")
            },
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new SitemapPlugin(config).AfterBuild(context);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.DoesNotContain("https://example.com/pages/a/", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_UsesUpdateTimeAsLastmod()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var pageDir = Path.Combine(outDir, "pages", "b");
        Directory.CreateDirectory(pageDir);
        File.WriteAllText(Path.Combine(pageDir, "index.html"), "<html><head></head></html>");

        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "b",
            publishAt: new DateTimeOffset(2024, 01, 02, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["update_time"] = new("date", new DateTimeOffset(2024, 02, 03, 4, 5, 6, TimeSpan.Zero))
            });

        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "n", Title = "t", Url = "https://example.com" },
            Content = TestContent.Markdown()
        };

        var context = new BuildContext
        {
            RootDir = root,
            OutputDir = outDir,
            BaseUrl = "/",
            LayoutsDir = root,
            RoutedDocuments = new List<(ContentDocument Item, RouteInfo Route)>
            {
                (item, new RouteInfo("/pages/b/", "pages/b/index.html", "pages/page.html"))
            }.ToRoutedDocuments(),
            SeoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["pages/b/index.html"] = new(
                    new RouteInfo("/pages/b/", "pages/b/index.html", "pages/page.html"),
                    "https://example.com/pages/b/",
                    Robots: null,
                    Indexable: true,
                    new DateTimeOffset(2024, 02, 03, 4, 5, 6, TimeSpan.Zero),
                    item.Id,
                    "page")
            },
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        new SitemapPlugin(config).AfterBuild(context);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.Contains("<loc>https://example.com/pages/b/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<lastmod>2024-02-03</lastmod>", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_ExternalAndDuplicateCanonicals_AreNotListed()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var outDir = Path.Combine(root, "dist");
            var pages = new (string Slug, string Canonical)[]
            {
                ("a", "https://example.com/pages/a/"),
                ("syndicated", "https://medium.com/@someone/original"),
                ("copy-of-a", "https://example.com/pages/a/")
            };
            var context = CreateCanonicalContext(root, outDir, "/", pages);
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "n", Title = "t", Url = "https://example.com" },
                Content = TestContent.Markdown()
            };

            new SitemapPlugin(config).AfterBuild(context);

            var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(sitemap, "<loc>https://example.com/pages/a/</loc>"));
            Assert.DoesNotContain("medium.com", sitemap, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public void AfterBuild_SubPathBaseUrl_ListsOnlyUrlsUnderBase()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var outDir = Path.Combine(root, "dist");
            var pages = new (string Slug, string Canonical)[]
            {
                ("inside", "https://example.com/repo/pages/inside/"),
                ("outside", "https://example.com/other-project/page/")
            };
            var context = CreateCanonicalContext(root, outDir, "/repo/", pages);
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "n", Title = "t", Url = "https://example.com/", BaseUrl = "/repo/" },
                Content = TestContent.Markdown()
            };

            new SitemapPlugin(config).AfterBuild(context);

            var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
            Assert.Contains("<loc>https://example.com/repo/pages/inside/</loc>", sitemap, StringComparison.Ordinal);
            Assert.DoesNotContain("other-project", sitemap, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("https://example.com", "/", "https://example.com/a/", true)]
    [InlineData("https://example.com", "/", "HTTPS://EXAMPLE.COM/a/", true)]
    [InlineData("https://example.com", "/", "https://other.example/a/", false)]
    [InlineData("https://example.com", "/", "https://example.com.evil.test/a/", false)]
    [InlineData("https://example.com/", "/repo/", "https://example.com/repo/a/", true)]
    [InlineData("https://example.com", "repo", "https://example.com/repo/", true)]
    [InlineData("https://example.com", "/repo/", "https://example.com/repository/a/", false)]
    [InlineData("https://example.com", "/repo/", "https://example.com/a/", false)]
    [InlineData("https://example.com", "/", "", false)]
    [InlineData("https://example.com", "/repo/", "https://example.com/REPO/a/", false)]
    [InlineData("https://example.com", "/repo/", "HTTPS://Example.COM/repo/a/", true)]
    [InlineData("https://example.com", "/", "https://example.com", true)]
    [InlineData("https://example.com:8443", "/", "https://example.com/a/", false)]
    [InlineData("https://example.com", "/", "https://example.com:443/a/", true)]
    [InlineData("https://example.com:443", "/repo/", "https://example.com/repo/a/", true)]
    [InlineData("http://example.com", "/", "http://example.com:80/a/", true)]
    [InlineData("https://example.com", "/", "https://example.com./a/", true)]
    [InlineData("https://example.com", "/", "http://example.com/a/", false)]
    public void SitemapLocFilter_AcceptsOnlyUrlsUnderSitemapLocation(string siteUrl, string baseUrl, string canonical, bool expected)
    {
        var filter = new SitemapLocFilter(siteUrl, baseUrl);

        Assert.Equal(expected, filter.IsInScope(canonical));
    }

    [Fact]
    public void SitemapLocFilter_RejectsRepeatedCanonical()
    {
        var filter = new SitemapLocFilter("https://example.com", "/");

        Assert.Equal(SitemapLocDecision.Accepted, filter.Evaluate("https://example.com/a/"));
        Assert.Equal(SitemapLocDecision.Duplicate, filter.Evaluate("https://example.com/a/"));
        Assert.Equal(SitemapLocDecision.Accepted, filter.Evaluate("https://example.com/b/"));
    }

    [Fact]
    public void SitemapLocFilter_LogsWhyRoutesAreLeftOut()
    {
        var filter = new SitemapLocFilter("https://example.com", "/repo/");
        var logger = new RecordingLogger();

        Assert.True(filter.TryAccept("/a/", "https://example.com/repo/a/", logger));
        Assert.False(filter.TryAccept("/syndicated/", "https://medium.com/@someone/original", logger));
        Assert.False(filter.TryAccept("/copy/", "https://example.com/repo/a/", logger));

        var info = Assert.Single(logger.Infos);
        Assert.Contains("event=sitemap.canonical_out_of_scope", info, StringComparison.Ordinal);
        Assert.Contains("route=/syndicated/", info, StringComparison.Ordinal);
        Assert.Contains("scope=https://example.com/repo/", info, StringComparison.Ordinal);
        var debug = Assert.Single(logger.Debugs);
        Assert.Contains("event=sitemap.canonical_duplicate route=/copy/", debug, StringComparison.Ordinal);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void AfterBuild_ExternalCanonical_LogsExclusion()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var outDir = Path.Combine(root, "dist");
            var logger = new RecordingLogger();
            var context = CreateCanonicalContext(root, outDir, "/", [("syndicated", "https://medium.com/@someone/original")], logger);
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "n", Title = "t", Url = "https://example.com" },
                Content = TestContent.Markdown()
            };

            new SitemapPlugin(config).AfterBuild(context);

            Assert.Contains(logger.Infos, message =>
                message.Contains("event=sitemap.canonical_out_of_scope route=/pages/syndicated/", StringComparison.Ordinal));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Debugs { get; } = [];
        public List<string> Infos { get; } = [];
        public List<string> Warnings { get; } = [];
        public void Debug(string message) => Debugs.Add(message);
        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) => Warnings.Add(message);
    }

    private static BuildContext CreateCanonicalContext(
        string root,
        string outDir,
        string baseUrl,
        IReadOnlyList<(string Slug, string Canonical)> pages,
        ILogger? logger = null)
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>();
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slug, canonical) in pages)
        {
            var outputPath = $"pages/{slug}/index.html";
            Directory.CreateDirectory(Path.Combine(outDir, "pages", slug));
            File.WriteAllText(Path.Combine(outDir, outputPath), "<html><head></head></html>");
            var item = ContentDocument.Create(
                id: slug,
                title: slug,
                slug: slug,
                publishAt: new DateTimeOffset(2024, 01, 02, 0, 0, 0, TimeSpan.Zero),
                contentHtml: "");
            var route = new RouteInfo($"/pages/{slug}/", outputPath, "pages/page.html");
            routed.Add((item, route));
            seoIndex[outputPath] = new SeoIndexEntry(route, canonical, Robots: null, Indexable: true, item.PublishAt, item.Id, "page");
        }

        return new BuildContext
        {
            RootDir = root,
            OutputDir = outDir,
            BaseUrl = baseUrl,
            LayoutsDir = root,
            RoutedDocuments = routed.ToRoutedDocuments(),
            SeoIndex = seoIndex,
            Logger = logger ?? new ConsoleLogger(LogLevel.Error)
        };
    }

    [Fact]
    public void AfterBuild_CollectionSitemapDisabledExcludesContentAndDerivedDocuments()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-sitemap-collection", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        var news = CreateDocument("news-1", "article", "news");
        var archive = CreateDocument("news-archive", "derived", "news");
        var guide = CreateDocument("guide-1", "article", "guides");
        var hiddenGuide = CreateDocument("guide-hidden", "article", "guides", excludeFromSitemap: true);
        var newsRoute = new RouteInfo("/news/news-1/", "news/news-1/index.html", "news.html");
        var archiveRoute = new RouteInfo("/news/archive/", "news/archive/index.html", "archive.html");
        var guideRoute = new RouteInfo("/guides/guide-1/", "guides/guide-1/index.html", "guide.html");
        var hiddenGuideRoute = new RouteInfo("/guides/hidden/", "guides/hidden/index.html", "guide.html");
        foreach (var route in new[] { newsRoute, archiveRoute, guideRoute, hiddenGuideRoute })
        {
            var path = Path.Combine(outDir, route.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "<html><head></head></html>");
        }

        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["news"] = new() { Permalink = "/news/{slug}/", Output = new() { Sitemap = false } },
                    ["guides"] = new() { Permalink = "/guides/{slug}/", Output = new() { Sitemap = true } }
                }
            },
            Content = TestContent.Markdown()
        };
        var context = new BuildContext
        {
            RootDir = root,
            OutputDir = outDir,
            BaseUrl = "/",
            LayoutsDir = root,
            RoutedDocuments =
            [
                new RoutedContentDocument(news, newsRoute),
                new RoutedContentDocument(guide, guideRoute),
                new RoutedContentDocument(hiddenGuide, hiddenGuideRoute)
            ],
            SeoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [newsRoute.OutputPath] = Entry(news, newsRoute),
                [archiveRoute.OutputPath] = Entry(archive, archiveRoute),
                [guideRoute.OutputPath] = Entry(guide, guideRoute),
                [hiddenGuideRoute.OutputPath] = Entry(hiddenGuide, hiddenGuideRoute)
            },
            Logger = new ConsoleLogger(LogLevel.Error)
        };
        context.DerivedDocuments.Add(new RoutedContentDocument(archive, archiveRoute));

        new SitemapPlugin(config).AfterBuild(context);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.DoesNotContain("https://example.com/news/news-1/", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/news/archive/", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/guides/hidden/", sitemap, StringComparison.Ordinal);
        Assert.Contains("https://example.com/guides/guide-1/", sitemap, StringComparison.Ordinal);

        static SeoIndexEntry Entry(ContentDocument document, RouteInfo route)
            => new(route, "https://example.com" + route.Url, null, true, document.PublishAt, document.Id, document.Record.Identity.ContentType);
    }

    private static ContentDocument CreateDocument(string id, string type, string collection, bool excludeFromSitemap = false)
        => ContentDocument.Create(
            id,
            id,
            id,
            new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
            string.Empty,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = type,
                ["collection"] = collection,
                ["sitemapExclude"] = excludeFromSitemap
            }));
}
