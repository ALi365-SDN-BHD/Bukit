using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
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
        var projection = PublishProjectionAdapters.Aggregate()
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

    [Fact]
    public void Project_ExternalAndDuplicateCanonicals_AreLeftOutAndNotReportedMissing()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-sitemap-canonical-scope", Guid.NewGuid().ToString("N"));
        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test", Url = "https://example.com" },
                Content = TestContent.Markdown()
            };
            var local = Article("local", canonical: null);
            var copy = Article("copy", canonical: "https://example.com/posts/local/");
            var syndicated = Article("syndicated", canonical: "https://medium.com/@someone/original");
            var routed = new[]
            {
                new RoutedContentDocument(local, new RouteInfo("/posts/local/", "posts/local/index.html", "post.html")),
                new RoutedContentDocument(copy, new RouteInfo("/posts/copy/", "posts/copy/index.html", "post.html")),
                new RoutedContentDocument(syndicated, new RouteInfo("/posts/syndicated/", "posts/syndicated/index.html", "post.html"))
            };
            foreach (var routedDocument in routed)
            {
                var path = Path.Combine(outputDir, routedDocument.Route.OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "<html><head></head><body></body></html>");
            }

            var index = SeoIndexBuilder.Build(
                config,
                "/",
                routed,
                Array.Empty<RouteInfo>(),
                new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal));
            Assert.True(index.Entries["posts/syndicated/index.html"].Indexable);
            Assert.Equal("https://medium.com/@someone/original", index.Entries["posts/syndicated/index.html"].Canonical);
            var graph = CanonicalContentGraphBuilder.BuildFromDocuments([local, copy, syndicated]);
            var logger = new RecordingLogger();
            var context = new PublishProjectionContext(config, outputDir, graph, index.Entries, index.Models, routed, Logger: logger);

            var result = PublishProjectionAdapters.Aggregate()
                .Single(adapter => adapter.Representation.Kind == "sitemap")
                .Project(context);

            var sitemap = File.ReadAllText(Path.Combine(outputDir, "sitemap.xml"));
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(sitemap, "<loc>https://example.com/posts/local/</loc>"));
            Assert.DoesNotContain("medium.com", sitemap, StringComparison.Ordinal);
            Assert.Contains(logger.Infos, message =>
                message.Contains("event=sitemap.canonical_out_of_scope route=/posts/syndicated/", StringComparison.Ordinal));

            var audit = MachineReadabilityTrustAuditBuilder.Build(
                config, outputDir, index.Entries, index.Models, graph,
                requireHreflangTargets: false, projectionResults: [result]);
            Assert.DoesNotContain(SitemapMissingIssues(audit), issue => issue.Route == "/posts/syndicated/");
            Assert.DoesNotContain(SitemapMissingIssues(audit), issue => issue.Route == "/posts/local/");
            var syndicatedDocument = audit.PublishReport.Documents.Single(document => document.RouteUrl == "/posts/syndicated/");
            Assert.DoesNotContain(syndicatedDocument.RepresentationKinds, kind => kind == "sitemap");

            // Control: an in-scope page that is really missing from the sitemap is still reported.
            File.WriteAllText(
                Path.Combine(outputDir, "sitemap.xml"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n</urlset>\n");
            var controlAudit = MachineReadabilityTrustAuditBuilder.Build(
                config, outputDir, index.Entries, index.Models, graph, requireHreflangTargets: false);
            Assert.Contains(controlAudit.SeoReport.Issues, issue => issue.Route == "/posts/local/" && issue.Code == "seo.sitemap_missing_url");
            Assert.Contains(controlAudit.PublishReport.Issues, issue => issue.Route == "/posts/local/" && issue.Code == "publish.sitemap_missing_route");
            Assert.DoesNotContain(SitemapMissingIssues(controlAudit), issue => issue.Route == "/posts/syndicated/");
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }

        static IEnumerable<(string Code, string? Route)> SitemapMissingIssues(MachineReadabilityTrustAuditResult audit)
            => audit.SeoReport.Issues.Select(issue => (issue.Code, issue.Route))
                .Concat(audit.PublishReport.Issues.Select(issue => (issue.Code, issue.Route)))
                .Where(issue => issue.Code is "seo.sitemap_missing_url" or "publish.sitemap_missing_route")
                .Select(issue => (issue.Code, issue.Route));
    }

    [Theory]
    [InlineData("https://example.com:443/posts/local/")]
    [InlineData("HTTPS://Example.COM/posts/local/")]
    public void Project_CanonicalSpelledDifferently_IsDedupedAndNotReportedMissing(string copyCanonical)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-sitemap-canonical-identity", Guid.NewGuid().ToString("N"));
        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test", Url = "https://example.com" },
                Content = TestContent.Markdown()
            };
            var local = Article("local", canonical: null);
            var copy = Article("copy", canonical: copyCanonical);
            var routed = new[]
            {
                new RoutedContentDocument(local, new RouteInfo("/posts/local/", "posts/local/index.html", "post.html")),
                new RoutedContentDocument(copy, new RouteInfo("/posts/copy/", "posts/copy/index.html", "post.html"))
            };
            foreach (var routedDocument in routed)
            {
                var path = Path.Combine(outputDir, routedDocument.Route.OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "<html><head></head><body></body></html>");
            }

            var index = SeoIndexBuilder.Build(
                config,
                "/",
                routed,
                Array.Empty<RouteInfo>(),
                new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal));
            var graph = CanonicalContentGraphBuilder.BuildFromDocuments([local, copy]);
            var context = new PublishProjectionContext(config, outputDir, graph, index.Entries, index.Models, routed, Logger: new RecordingLogger());

            var result = PublishProjectionAdapters.Aggregate()
                .Single(adapter => adapter.Representation.Kind == "sitemap")
                .Project(context);

            var sitemap = File.ReadAllText(Path.Combine(outputDir, "sitemap.xml"));
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(sitemap, "<loc>"));
            Assert.All(result.Outputs, output => Assert.True(output.Exists, output.Url));

            foreach (var projectionResults in new IReadOnlyList<PublishProjectionResult>?[] { [result], null })
            {
                var audit = MachineReadabilityTrustAuditBuilder.Build(
                    config, outputDir, index.Entries, index.Models, graph,
                    requireHreflangTargets: false, projectionResults: projectionResults);
                Assert.DoesNotContain(audit.SeoReport.Issues, issue => issue.Code == "seo.sitemap_missing_url");
                Assert.DoesNotContain(audit.PublishReport.Issues, issue => issue.Code == "publish.sitemap_missing_route");
            }
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    private static ContentDocument Article(string id, string? canonical)
    {
        var fields = new Dictionary<string, object> { ["type"] = "article" };
        if (canonical is not null)
        {
            fields["canonical"] = canonical;
        }

        return ContentDocument.Create(
            id,
            id,
            id,
            new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
            "<p>Body</p>",
            ContentFieldReader.ToFieldMap(fields));
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Infos { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) { }
        public void Error(string message) { }
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
