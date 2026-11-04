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

    [Theory]
    [InlineData("https://example.com", "https://example.com/repo/en/guides/")]
    [InlineData(null, "/repo/en/guides/")]
    public void GenerateRootOutputs_SubPathRoot_KeepsSingleBaseAndSitemapScope(string? siteUrl, string expectedLlmsPrefix)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-i18n-subpath", Guid.NewGuid().ToString("N"));
        try
        {
            var outputDir = Path.Combine(root, "dist");
            var variantDir = Path.Combine(outputDir, "en");
            var guide = Document("guide-1", "article", "guides");
            var copy = Document("guide-copy", "article", "guides");
            var syndicated = Document("guide-syndicated", "article", "guides");
            var guideRoute = new RouteInfo("/guides/guide-1/", "guides/guide-1/index.html", "guide.html");
            var copyRoute = new RouteInfo("/guides/copy/", "guides/copy/index.html", "guide.html");
            var syndicatedRoute = new RouteInfo("/guides/syndicated/", "guides/syndicated/index.html", "guide.html");
            foreach (var route in new[] { guideRoute, copyRoute, syndicatedRoute })
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
                    Url = siteUrl,
                    BaseUrl = "/repo/",
                    Languages = ["en", "zh"],
                    SitemapMode = "merged"
                },
                Content = TestContent.Markdown()
            };
            var origin = siteUrl ?? string.Empty;
            var guideCanonical = origin + "/repo/en/guides/guide-1/";
            var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [guideRoute.OutputPath] = Entry(guide, guideRoute, guideCanonical),
                [copyRoute.OutputPath] = Entry(copy, copyRoute, guideCanonical),
                [syndicatedRoute.OutputPath] = Entry(syndicated, syndicatedRoute, "https://medium.com/@someone/original")
            };
            var variant = new BuildVariantResult(
                Language: "en",
                OutputDir: variantDir,
                BaseUrl: "/repo/en",
                SearchSnippetsEnabled: false,
                BodyStore: EmptyContentBodyStore.Instance,
                DerivedRoutes: Array.Empty<(RouteInfo Route, DateTimeOffset LastModified)>(),
                SeoIndex: seoIndex,
                SeoModels: new Dictionary<string, Bukit.Rendering.SeoModel>(StringComparer.OrdinalIgnoreCase),
                PluginExecutions: Array.Empty<PluginExecutionInfo>(),
                RenderedCount: 3,
                SkippedCount: 0,
                RenderReasons: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                StageMetrics: BuildStageMetrics.Empty,
                RoutedDocuments:
                [
                    new RoutedContentDocument(guide, guideRoute),
                    new RoutedContentDocument(copy, copyRoute),
                    new RoutedContentDocument(syndicated, syndicatedRoute)
                ],
                ContentGraph: CanonicalContentGraph.Empty,
                ListRouteGraph: ListRouteGraph.Empty,
                DerivedDocuments: []);

            I18nOutputMerger.GenerateRootOutputs(
                config,
                outputDir,
                "/repo",
                [variant],
                new ConsoleLogger(LogLevel.Error),
                new DefaultSearchIndexBuilder());

            var llms = File.ReadAllText(Path.Combine(outputDir, "llms.txt"));
            Assert.Contains("(" + expectedLlmsPrefix + "guide-1/)", llms, StringComparison.Ordinal);
            Assert.DoesNotContain("/repo/repo/", llms, StringComparison.Ordinal);

            var sitemapPath = Path.Combine(outputDir, "sitemap.xml");
            if (siteUrl is null)
            {
                Assert.False(File.Exists(sitemapPath));
                return;
            }

            var sitemap = File.ReadAllText(sitemapPath);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(sitemap, "<loc>https://example.com/repo/en/guides/guide-1/</loc>"));
            Assert.DoesNotContain("medium.com", sitemap, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }

        static SeoIndexEntry Entry(ContentDocument document, RouteInfo route, string canonical)
            => new(route, canonical, null, true, document.PublishAt, document.Id, document.Record.Identity.ContentType);
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
