using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PublishRepresentationRegistryTests
{
    [Fact]
    public void PublicContentProjectionPolicy_ReplacesNotionIdentityAndSanitizesGraphIdentifiers()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        var record = new ContentRecord(
            new ContentIdentity($"posts:{notionId}", "safe-route", notionId, "post", "published"),
            new ContentPresentation("Safe title", null, null, "en", []),
            new ContentClassification("post", "posts", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.Parse("2026-07-13T00:00:00Z"), null, null, null),
            new ProvenanceRecord("notion", null, [], [], null),
            new TrustMetadata(null, "published", []),
            [
                new EntityRecord("page", "Safe entity", Id: notionId),
                new EntityRecord("page", notionId, Id: notionId)
            ],
            [
                new ContentRelation("related", "Safe target", TargetId: notionId),
                new ContentRelation("related", notionId, TargetId: notionId)
            ],
            []);

        Assert.Equal("/safe-route/", PublicContentProjectionPolicy.ResolvePublicId(record, "/safe-route/"));
        var entities = PublicContentProjectionPolicy.SanitizeEntities(record);
        var relations = PublicContentProjectionPolicy.SanitizeRelations(record);

        var entity = Assert.Single(entities);
        Assert.Equal("Safe entity", entity.Name);
        Assert.Null(entity.Id);
        var relation = Assert.Single(relations);
        Assert.Equal("Safe target", relation.Target);
        Assert.Null(relation.TargetId);
    }

    [Fact]
    public void BuildAgentManifestEntries_RemovesRelatedNotionUuidEntityNames()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        const string relatedNotionId = "aaaaaaaa-1111-4222-8333-bbbbbbbbbbbb";
        var record = new ContentRecord(
            new ContentIdentity($"posts:{notionId}", "safe-route", notionId, "post", "published"),
            new ContentPresentation("Safe title", null, null, "en", []),
            new ContentClassification("post", "posts", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.Parse("2026-07-13T00:00:00Z"), null, null, null),
            new ProvenanceRecord("notion", null, [], [], null),
            new TrustMetadata(null, "published", []),
            [new EntityRecord("company", "Bukit"), new EntityRecord("page", relatedNotionId)],
            [],
            []);
        var document = new ContentDocument(record, new ContentBodyRef(null, null));
        var route = new RouteInfo("/safe-route/", "safe-route/index.html", "post.html");
        var context = new PublishProjectionContext(
            new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test" },
                Content = TestContent.Notion()
            },
            Path.GetTempPath(),
            new CanonicalContentGraph([record], [], [], []),
            new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
            [new RoutedContentDocument(document, route)]);

        var entry = Assert.Single(DefaultContentProjectionWriter.BuildAgentManifestEntries(context));

        Assert.Equal(["Bukit"], entry.Entities);
    }

    [Fact]
    public void DocumentKinds_ReturnsCanonicalDocumentRepresentations()
    {
        var kinds = PublishRepresentationRegistry.DocumentKinds(includeJsonLd: true);

        Assert.Equal(["html", "semantic-html", "json", "markdown", "jsonld"], kinds);
    }

    [Fact]
    public void AggregateRepresentations_ReturnsExistingAggregateOutputPaths()
    {
        var representations = PublishRepresentationRegistry.AggregateRepresentations().ToArray();

        Assert.Contains(representations, x => x.Kind == "sitemap" && x.Path == "sitemap.xml");
        Assert.Contains(representations, x => x.Kind == "search" && x.Path == "search.json");
        Assert.Contains(representations, x => x.Kind == "atom" && x.Path == "feed/atom.xml");
        Assert.Contains(representations, x => x.Kind == "robots" && x.Path == "robots.txt");
        Assert.Contains(representations, x => x.Kind == "llms" && x.Path == "llms.txt");
        Assert.Contains(representations, x => x.Kind == "llms-full" && x.Path == "llms-full.txt");
        Assert.Contains(representations, x => x.Kind == "agent-manifest" && x.Path == "agent-manifest.json");
    }

    [Fact]
    public void BuiltInProjectionClasses_ImplementProjectionContract()
    {
        Assert.IsAssignableFrom<IPublishProjection>(new JsonContentDocumentProjection());
        Assert.IsAssignableFrom<IPublishProjection>(new MarkdownContentDocumentProjection());
        Assert.IsAssignableFrom<IPublishProjection>(new AgentManifestProjection());
        foreach (var projection in PublishRepresentationRegistry.AggregateProjectionAdapters())
        {
            Assert.IsAssignableFrom<IPublishProjection>(projection);
        }
    }

    [Fact]
    public void AggregateProjectionAdapters_ReturnNamedDocumentFirstProjections()
    {
        var projections = PublishRepresentationRegistry.AggregateProjectionAdapters();

        Assert.Equal(
            [
                typeof(RssFeedPublishProjection),
                typeof(AtomFeedPublishProjection),
                typeof(JsonFeedPublishProjection),
                typeof(SitemapPublishProjection),
                typeof(SearchIndexPublishProjection),
                typeof(LlmsTxtPublishProjection),
                typeof(LlmsFullTxtPublishProjection),
                typeof(RobotsTxtPublishProjection),
                typeof(AgentManifestAggregateInventoryProjection)
            ],
            projections.Select(x => x.GetType()).ToArray());
        Assert.DoesNotContain(projections, projection =>
            projection.GetType().Name.Contains("ExistingAggregate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProjectionContract_RemainsInternalUntilExternalProjectionAbiIsDefined()
    {
        Assert.False(typeof(IPublishProjection).IsPublic);
        Assert.False(typeof(PublishProjectionContext).IsPublic);
        Assert.False(typeof(PublishProjectionResult).IsPublic);
    }

    [Fact]
    public void DocumentProjectionContracts_WriteFilesAndReturnOutputs()
    {
        const string relatedNotionId = "aaaaaaaa-1111-4222-8333-bbbbbbbbbbbb";
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit_projection_contract_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        try
        {
            var document = ContentDocument.Create(
                "post-1",
                "Projection Post",
                "projection-post",
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "<p>Hello projection.</p>",
                ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["language"] = "en",
                    ["status"] = "published",
                    ["review_status"] = "reviewed",
                    ["source"] = "notion",
                    ["entities"] = new object[]
                    {
                        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "company", ["name"] = "Bukit" },
                        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "page", ["name"] = relatedNotionId }
                    }
                }));
            var route = new RouteInfo("/projection-post/", "projection-post/index.html", "post.html");
            var graph = CanonicalContentGraphBuilder.BuildFromDocuments(new[] { document });
            var context = new PublishProjectionContext(
                Config: new AppConfig
                {
                    Site = new SiteConfig { Name = "test", Title = "Test", Url = "https://example.com" },
                    Content = TestContent.Markdown()
                },
                OutputDir: outputDir,
                ContentGraph: graph,
                SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["projection-post/index.html"] = new SeoIndexEntry(
                        route,
                        "https://example.com/projection-post/",
                        Robots: null,
                        Indexable: true,
                        DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                        SourceItemId: document.Id,
                        ContentType: "post")
                },
                SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
                {
                    ["projection-post/index.html"] = new SeoModel
                    {
                        Title = "Projection Post",
                        Canonical = "https://example.com/projection-post/"
                    }
                },
                RoutedDocuments: new[] { new RoutedContentDocument(document, route) });

            var jsonResult = new JsonContentDocumentProjection().Project(context);
            var markdownResult = new MarkdownContentDocumentProjection().Project(context);

            Assert.Contains(jsonResult.Outputs, x => x.Kind == "json" && x.Url == "/content/projection-post.json" && x.Exists && x.Indexable);
            Assert.Contains(markdownResult.Outputs, x => x.Kind == "markdown" && x.Url == "/content/projection-post.md" && x.Exists && x.Indexable);
            Assert.True(File.Exists(Path.Combine(outputDir, "content", "projection-post.json")));
            Assert.True(File.Exists(Path.Combine(outputDir, "content", "projection-post.md")));
            Assert.DoesNotContain(relatedNotionId, File.ReadAllText(Path.Combine(outputDir, "content", "projection-post.json")), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(relatedNotionId, File.ReadAllText(Path.Combine(outputDir, "content", "projection-post.md")), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AgentManifestProjection_OnlyDeclaresJsonLdWhenSeoModelContainsJsonLd()
    {
        var record = Document("post", "Post").Record;
        var route = new RouteInfo("/post/", "post/index.html", "post.html");
        var entry = new SeoIndexEntry(route, "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post", "post");

        var withoutJsonLd = DefaultContentProjectionWriter.BuildAgentManifestRepresentationEntries(
            record,
            route.Url,
            entry,
            new SeoModel { Title = "Post", Canonical = "https://example.com/post/" });
        var withJsonLd = DefaultContentProjectionWriter.BuildAgentManifestRepresentationEntries(
            record,
            route.Url,
            entry,
            new SeoModel { Title = "Post", Canonical = "https://example.com/post/", JsonLd = ["{\"@type\":\"Article\"}"] });

        Assert.DoesNotContain(withoutJsonLd, x => x.Kind == "jsonld");
        Assert.Contains(withJsonLd, x => x.Kind == "jsonld" && x.Url == "https://example.com/post/");
    }

    [Fact]
    public void AggregateProjectionAdapters_ReturnPerRouteOutputs()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit_projection_aggregate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        try
        {
            File.WriteAllText(Path.Combine(outputDir, "llms.txt"), "https://example.com/visible/");
            var visibleRoute = new RouteInfo("/visible/", "visible/index.html", "post.html");
            var hiddenRoute = new RouteInfo("/hidden/", "hidden/index.html", "post.html");
            var visible = Document("visible", "Visible");
            var hidden = Document("hidden", "Hidden");
            var context = new PublishProjectionContext(
                Config: new AppConfig
                {
                    Site = new SiteConfig { Name = "test", Title = "Test", Url = "https://example.com" },
                    Content = TestContent.Markdown()
                },
                OutputDir: outputDir,
                ContentGraph: CanonicalContentGraph.Empty,
                SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["visible/index.html"] = new SeoIndexEntry(visibleRoute, "https://example.com/visible/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), visible.Id, "post"),
                    ["hidden/index.html"] = new SeoIndexEntry(hiddenRoute, "https://example.com/hidden/", "noindex", false, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), hidden.Id, "post")
                },
                SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
                RoutedDocuments: new[]
                {
                    new RoutedContentDocument(visible, visibleRoute),
                    new RoutedContentDocument(hidden, hiddenRoute)
                });

            var projection = PublishRepresentationRegistry.AggregateProjectionAdapters()
                .Single(x => x.Representation.Kind == "llms");

            var result = projection.Project(context);

            Assert.Equal(2, result.Outputs.Count);
            Assert.Contains(result.Outputs, x => x.Kind == "llms" && x.Url == "/visible/" && x.Path == "llms.txt" && x.Exists && x.Indexable);
            Assert.Contains(result.Outputs, x => x.Kind == "llms" && x.Url == "/hidden/" && x.Path == "llms.txt" && !x.Exists && !x.Indexable);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AggregateProjectionAdapters_GenerateRegisteredAggregateOutputs()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit_projection_generate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        try
        {
            var route = new RouteInfo("/post/", "post/index.html", "post.html");
            var document = Document("post", "Post");
            var context = new PublishProjectionContext(
                Config: new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "test",
                        Title = "Test",
                        Url = "https://example.com",
                        Collections = new Dictionary<string, CollectionConfig>
                        {
                            ["post"] = new() { Permalink = "/post/{slug}/", Output = new CollectionOutputConfig { Rss = true } }
                        }
                    },
                    Content = TestContent.Markdown()
                },
                OutputDir: outputDir,
                ContentGraph: CanonicalContentGraph.Empty,
                SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post/index.html"] = new SeoIndexEntry(route, "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), document.Id, "post")
                },
                SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
                BodyStore: new InlineBodyStore("<p>Post body</p>"),
                BaseUrl: "/",
                RoutedDocuments: new[] { new RoutedContentDocument(document, route) });

            var projection = PublishRepresentationRegistry.AggregateProjectionAdapters()
                .Single(x => x.Representation.Kind == "feed");

            var result = projection.Project(context);

            Assert.True(File.Exists(Path.Combine(outputDir, "rss.xml")));
            Assert.Contains(result.Outputs, x => x.Kind == "feed" && x.Url == "/post/" && x.Exists);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void SitemapProjection_IncludesListRouteGraphEntriesAndRespectsCollectionExclusions()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit_projection_list_sitemap_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        try
        {
            var graph = ListRouteGraph.Create(new[]
            {
                ListRoute("collection:post:1", ListRouteKind.CollectionList, "/blog/", "blog/index.html", "post"),
                ListRoute("collection:post:2", ListRouteKind.CollectionPage, "/blog/page/2/", "blog/page/2/index.html", "post"),
                ListRoute("collection:company:1", ListRouteKind.CollectionList, "/companies/", "companies/index.html", "company"),
                ListRoute("filter:company:country:Malaysia:2", ListRouteKind.FilteredListPage, "/companies/malaysia/page/2/", "companies/malaysia/page/2/index.html", "company")
            });
            foreach (var route in graph.Routes)
            {
                WriteHtml(outputDir, route.OutputPath);
            }

            var context = new PublishProjectionContext(
                Config: new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "test",
                        Title = "Test",
                        Url = "https://example.com",
                        Collections = new Dictionary<string, CollectionConfig>
                        {
                            ["post"] = new()
                            {
                                Permalink = "/blog/{slug}/",
                                ListRoute = "/blog/",
                                ListTemplate = "list.html",
                                Output = new CollectionOutputConfig { Sitemap = true }
                            },
                            ["company"] = new()
                            {
                                Permalink = "/companies/{slug}/",
                                ListRoute = "/companies/",
                                ListTemplate = "list.html",
                                Output = new CollectionOutputConfig { Sitemap = false }
                            }
                        }
                    },
                    Content = TestContent.Markdown()
                },
                OutputDir: outputDir,
                ContentGraph: CanonicalContentGraph.Empty,
                SeoIndex: graph.Routes.ToDictionary(
                    route => BuildPathUtils.NormalizeRelPath(route.OutputPath),
                    route => new SeoIndexEntry(route.ToRouteInfo(), $"https://example.com{route.Url}", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), null, "list", IsDerived: true),
                    StringComparer.OrdinalIgnoreCase),
                SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
                RoutedDocuments: Array.Empty<RoutedContentDocument>(),
                ListRouteGraph: graph);

            new SitemapPublishProjection().Project(context);

            var sitemap = File.ReadAllText(Path.Combine(outputDir, "sitemap.xml"));
            Assert.Contains("<loc>https://example.com/blog/</loc>", sitemap, StringComparison.Ordinal);
            Assert.Contains("<loc>https://example.com/blog/page/2/</loc>", sitemap, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/companies/", sitemap, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/companies/malaysia/page/2/", sitemap, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void FeedProjections_WritePerCollectionFeedsToCollectionFeedPath()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit_projection_collection_feed_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        try
        {
            var route = new RouteInfo("/post/", "post/index.html", "post.html");
            var document = Document("post", "Post");
            var context = new PublishProjectionContext(
                Config: new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "test",
                        Title = "Test",
                        Url = "https://example.com",
                        Feed = new FeedConfig { Formats = ["rss", "atom", "json"] },
                        Collections = new Dictionary<string, CollectionConfig>
                        {
                            ["post"] = new()
                            {
                                Permalink = "/post/{slug}/",
                                Output = new CollectionOutputConfig
                                {
                                    Rss = true,
                                    FeedPath = "feeds/posts",
                                    FeedTitle = "Post Feed"
                                }
                            }
                        }
                    },
                    Content = TestContent.Markdown()
                },
                OutputDir: outputDir,
                ContentGraph: CanonicalContentGraph.Empty,
                SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post/index.html"] = new SeoIndexEntry(route, "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), document.Id, "post")
                },
                SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
                BodyStore: new InlineBodyStore("<p>Post body</p>"),
                BaseUrl: "/",
                RoutedDocuments: new[] { new RoutedContentDocument(document, route) });

            foreach (var projection in PublishRepresentationRegistry.AggregateProjectionAdapters()
                         .Where(x => x.Representation.Kind is "feed" or "atom" or "jsonfeed"))
            {
                projection.Project(context);
            }

            Assert.True(File.Exists(Path.Combine(outputDir, "feeds", "posts", "rss.xml")));
            Assert.True(File.Exists(Path.Combine(outputDir, "feeds", "posts", "atom.xml")));
            Assert.True(File.Exists(Path.Combine(outputDir, "feeds", "posts", "feed.json")));
            Assert.Contains("<title>Post Feed</title>", File.ReadAllText(Path.Combine(outputDir, "feeds", "posts", "rss.xml")), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void SearchProjection_GeneratesSearchUiPartial()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit_projection_search_ui_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        try
        {
            var route = new RouteInfo("/post/", "post/index.html", "post.html");
            var document = Document("post", "Post");
            var context = new PublishProjectionContext(
                Config: new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "test",
                        Title = "Test",
                        Url = "https://example.com",
                        Search = new SearchDetailConfig { Ui = "default", PlaceholderText = "Find content" }
                    },
                    Content = TestContent.Markdown()
                },
                OutputDir: outputDir,
                ContentGraph: CanonicalContentGraph.Empty,
                SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post/index.html"] = new SeoIndexEntry(route, "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), document.Id, "post")
                },
                SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
                BodyStore: new InlineBodyStore("<p>Post body</p>"),
                BaseUrl: "/",
                RoutedDocuments: new[] { new RoutedContentDocument(document, route) });

            var projection = PublishRepresentationRegistry.AggregateProjectionAdapters()
                .Single(x => x.Representation.Kind == "search");

            projection.Project(context);

            var searchUi = File.ReadAllText(Path.Combine(outputDir, "bukit-search.html"));
            Assert.Contains("Find content", searchUi, StringComparison.Ordinal);
            Assert.Contains("search.json", searchUi, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildRepresentationKinds_UsesRegistryDocumentKinds()
    {
        var entry = new SeoIndexEntry(
            new RouteInfo("/post/", "post/index.html", "post.html"),
            "https://example.com/post/",
            Robots: null,
            Indexable: true,
            DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
            SourceItemId: "post-1",
            ContentType: "post");
        var model = new SeoModel
        {
            Title = "Post",
            Canonical = "https://example.com/post/",
            JsonLd = ["{\"@type\":\"Article\"}"]
        };

        var kinds = PublishDocumentBuilder.BuildRepresentationKinds(entry, model);

        var documentKinds = PublishRepresentationRegistry.DocumentKinds(includeJsonLd: true);
        Assert.Equal(documentKinds, kinds.Take(documentKinds.Count).ToArray());
        Assert.Contains("search", kinds);
        Assert.Contains("sitemap", kinds);
    }

    private static ContentDocument Document(string id, string title)
        => ContentDocument.Create(
            id,
            title,
            id,
            DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
            "<p>Body</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["language"] = "en",
                ["status"] = "published",
                ["type"] = "post",
                ["collection"] = "post"
            }));

    private static ListRoutePlan ListRoute(string id, ListRouteKind kind, string url, string outputPath, string collection)
        => new()
        {
            RouteId = id,
            Kind = kind,
            Url = url,
            OutputPath = outputPath,
            Template = "list.html",
            Collection = collection,
            PageNumber = url.Contains("/page/", StringComparison.OrdinalIgnoreCase) ? 2 : 1,
            PageSize = 10,
            TotalItems = 20,
            CanonicalUrl = url
        };

    private static void WriteHtml(string outputDir, string outputPath)
    {
        var path = Path.Combine(outputDir, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<!doctype html><html><head></head><body></body></html>");
    }

    private sealed class InlineBodyStore : IContentBodyStore
    {
        private readonly string _html;

        public InlineBodyStore(string html)
        {
            _html = html;
        }

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(_html));
    }
}
