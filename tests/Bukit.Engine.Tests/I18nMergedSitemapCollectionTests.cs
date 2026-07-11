using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nMergedSitemapCollectionTests
{
    [Fact]
    public void GenerateRootOutputs_CollectionSitemapDisabledExcludesContentAndDerivedDocuments()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-i18n-sitemap-collection", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(root, "dist");
        var variantDir = Path.Combine(outputDir, "en");
        var news = Document("news-1", "article", "news");
        var archive = Document("news-archive", "derived", "news");
        var guide = Document("guide-1", "article", "guides");
        var hiddenGuide = Document("guide-hidden", "article", "guides", excludeFromSitemap: true);
        var newsRoute = new RouteInfo("/news/news-1/", "news/news-1/index.html", "news.html");
        var archiveRoute = new RouteInfo("/news/archive/", "news/archive/index.html", "archive.html");
        var guideRoute = new RouteInfo("/guides/guide-1/", "guides/guide-1/index.html", "guide.html");
        var hiddenGuideRoute = new RouteInfo("/guides/hidden/", "guides/hidden/index.html", "guide.html");
        foreach (var route in new[] { newsRoute, archiveRoute, guideRoute, hiddenGuideRoute })
        {
            var path = Path.Combine(variantDir, route.OutputPath);
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
                Languages = ["en", "zh"],
                SitemapMode = "merged",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["news"] = new() { Permalink = "/news/{slug}/", Output = new() { Sitemap = false } },
                    ["guides"] = new() { Permalink = "/guides/{slug}/", Output = new() { Sitemap = true } }
                }
            },
            Content = TestContent.Markdown()
        };
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            [newsRoute.OutputPath] = Entry(news, newsRoute),
            [archiveRoute.OutputPath] = Entry(archive, archiveRoute),
            [guideRoute.OutputPath] = Entry(guide, guideRoute),
            [hiddenGuideRoute.OutputPath] = Entry(hiddenGuide, hiddenGuideRoute)
        };
        var variant = new BuildVariantResult(
            Language: "en",
            OutputDir: variantDir,
            BaseUrl: "/en",
            SearchSnippetsEnabled: false,
            BodyStore: EmptyContentBodyStore.Instance,
            DerivedRoutes: Array.Empty<(RouteInfo Route, DateTimeOffset LastModified)>(),
            SeoIndex: seoIndex,
            SeoModels: new Dictionary<string, Bukit.Rendering.SeoModel>(StringComparer.OrdinalIgnoreCase),
            PluginExecutions: Array.Empty<PluginExecutionInfo>(),
            RenderedCount: 4,
            SkippedCount: 0,
            RenderReasons: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            StageMetrics: BuildStageMetrics.Empty,
            RoutedDocuments:
            [
                new RoutedContentDocument(news, newsRoute),
                new RoutedContentDocument(guide, guideRoute),
                new RoutedContentDocument(hiddenGuide, hiddenGuideRoute)
            ],
            ContentGraph: CanonicalContentGraph.Empty,
            ListRouteGraph: ListRouteGraph.Empty,
            DerivedDocuments: [new RoutedContentDocument(archive, archiveRoute)]);

        I18nOutputMerger.GenerateRootOutputs(
            config,
            outputDir,
            "/",
            [variant],
            new ConsoleLogger(LogLevel.Error),
            new DefaultSearchIndexBuilder());

        var sitemap = File.ReadAllText(Path.Combine(outputDir, "sitemap.xml"));
        Assert.DoesNotContain("https://example.com/en/news/news-1/", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/en/news/archive/", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/en/guides/hidden/", sitemap, StringComparison.Ordinal);
        Assert.Contains("https://example.com/en/guides/guide-1/", sitemap, StringComparison.Ordinal);

        SeoIndexEntry Entry(ContentDocument document, RouteInfo route)
            => new(
                route,
                "https://example.com/en" + route.Url,
                null,
                true,
                document.PublishAt,
                document.Id,
                document.Record.Identity.ContentType);
    }

    private static ContentDocument Document(string id, string type, string collection, bool excludeFromSitemap = false)
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
