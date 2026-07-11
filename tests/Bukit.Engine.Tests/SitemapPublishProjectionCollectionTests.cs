using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SitemapPublishProjectionCollectionTests
{
    [Fact]
    public void Project_UsesDocumentAndListCollectionExclusionsOnFinalSitemap()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-sitemap-projection", Guid.NewGuid().ToString("N"));
        var news = Document("news-1", "article", "news");
        var archive = Document("news-archive", "derived", "news");
        var hiddenGuide = Document("guide-hidden", "article", "guides", excludeFromSitemap: true);
        var guide = Document("guide-1", "article", "guides");
        var newsRoute = new RouteInfo("/news/news-1/", "news/news-1/index.html", "news.html");
        var archiveRoute = new RouteInfo("/news/archive/", "news/archive/index.html", "archive.html");
        var hiddenGuideRoute = new RouteInfo("/guides/hidden/", "guides/hidden/index.html", "guide.html");
        var guideRoute = new RouteInfo("/guides/guide-1/", "guides/guide-1/index.html", "guide.html");
        var newsList = new ListRoutePlan
        {
            RouteId = "collection:news:1",
            Kind = ListRouteKind.CollectionList,
            Url = "/news/",
            OutputPath = "news/index.html",
            Template = "news-list.html",
            Collection = "news",
            TotalItems = 1,
            CanonicalUrl = "/news/"
        };
        var listRoute = newsList.ToRouteInfo();
        foreach (var route in new[] { newsRoute, archiveRoute, hiddenGuideRoute, guideRoute, listRoute })
        {
            var path = Path.Combine(outputDir, route.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "<html><head></head></html>");
        }

        var context = new PublishProjectionContext(
            Config: new AppConfig
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
            },
            OutputDir: outputDir,
            ContentGraph: CanonicalContentGraph.Empty,
            SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [newsRoute.OutputPath] = Entry(news, newsRoute),
                [archiveRoute.OutputPath] = Entry(archive, archiveRoute),
                [hiddenGuideRoute.OutputPath] = Entry(hiddenGuide, hiddenGuideRoute),
                [guideRoute.OutputPath] = Entry(guide, guideRoute),
                [listRoute.OutputPath] = new(listRoute, "https://example.com/news/", null, true, DateTimeOffset.UnixEpoch, "collection:news:1", "derived")
            },
            SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
            RoutedDocuments:
            [
                new RoutedContentDocument(news, newsRoute),
                new RoutedContentDocument(hiddenGuide, hiddenGuideRoute),
                new RoutedContentDocument(guide, guideRoute)
            ],
            ListRouteGraph: ListRouteGraph.Create([newsList]),
            DerivedDocuments: [new RoutedContentDocument(archive, archiveRoute)]);
        var projection = PublishRepresentationRegistry.AggregateProjectionAdapters()
            .Single(adapter => adapter.Representation.Kind == "sitemap");

        projection.Project(context);

        var sitemap = File.ReadAllText(Path.Combine(outputDir, "sitemap.xml"));
        Assert.DoesNotContain("https://example.com/news/news-1/", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/news/archive/", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/news/", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/guides/hidden/", sitemap, StringComparison.Ordinal);
        Assert.Contains("https://example.com/guides/guide-1/", sitemap, StringComparison.Ordinal);

        static SeoIndexEntry Entry(ContentDocument document, RouteInfo route)
            => new(route, "https://example.com" + route.Url, null, true, document.PublishAt, document.Id, document.Record.Identity.ContentType);
    }

    private static ContentDocument Document(string id, string type, string collection, bool excludeFromSitemap = false)
        => ContentDocument.Create(
            id,
            id,
            id,
            DateTimeOffset.UnixEpoch,
            string.Empty,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = type,
                ["collection"] = collection,
                ["sitemapExclude"] = excludeFromSitemap
            }));
}
