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
            Content = new ContentConfig { Provider = "markdown" }
        };

        var context = new BuildContext
        {
            Config = config,
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

        new SitemapPlugin().AfterBuild(context);

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
            Content = new ContentConfig { Provider = "markdown" }
        };

        var context = new BuildContext
        {
            Config = config,
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

        new SitemapPlugin().AfterBuild(context);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.Contains("<loc>https://example.com/pages/b/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<lastmod>2024-02-03</lastmod>", sitemap, StringComparison.Ordinal);
    }
}
