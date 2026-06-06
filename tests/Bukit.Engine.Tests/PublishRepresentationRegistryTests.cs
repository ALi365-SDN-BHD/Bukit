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
    public void DocumentProjectionContracts_WriteFilesAndReturnOutputs()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit_projection_contract_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        try
        {
            var item = new ContentItem(
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
                    ["source"] = "editorial"
                }));
            var route = new RouteInfo("/projection-post/", "projection-post/index.html", "post.html");
            var graph = new CanonicalContentGraph([CanonicalContentGraphBuilder.ToRecord(item)], Array.Empty<EntityRecord>());
            var context = new PublishProjectionContext(
                new AppConfig
                {
                    Site = new SiteConfig { Name = "test", Title = "Test", Url = "https://example.com" },
                    Content = new ContentConfig { Provider = "markdown" }
                },
                outputDir,
                graph,
                [(item, route)],
                Array.Empty<(ContentItem Item, RouteInfo Route)>(),
                new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["projection-post/index.html"] = new SeoIndexEntry(
                        route,
                        "https://example.com/projection-post/",
                        Robots: null,
                        Indexable: true,
                        DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                        SourceItemId: item.Id,
                        ContentType: "post")
                },
                new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
                {
                    ["projection-post/index.html"] = new SeoModel
                    {
                        Title = "Projection Post",
                        Canonical = "https://example.com/projection-post/"
                    }
                });

            var jsonResult = new JsonContentDocumentProjection().Project(context);
            var markdownResult = new MarkdownContentDocumentProjection().Project(context);

            Assert.Contains(jsonResult.Outputs, x => x.Kind == "json" && x.Url == "/content/projection-post.json" && x.Exists && x.Indexable);
            Assert.Contains(markdownResult.Outputs, x => x.Kind == "markdown" && x.Url == "/content/projection-post.md" && x.Exists && x.Indexable);
            Assert.True(File.Exists(Path.Combine(outputDir, "content", "projection-post.json")));
            Assert.True(File.Exists(Path.Combine(outputDir, "content", "projection-post.md")));
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
        var record = CanonicalContentGraphBuilder.ToRecord(Item("post", "Post"));
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
            var visible = Item("visible", "Visible");
            var hidden = Item("hidden", "Hidden");
            var context = new PublishProjectionContext(
                new AppConfig
                {
                    Site = new SiteConfig { Name = "test", Title = "Test", Url = "https://example.com" },
                    Content = new ContentConfig { Provider = "markdown" }
                },
                outputDir,
                CanonicalContentGraph.Empty,
                [(visible, visibleRoute), (hidden, hiddenRoute)],
                Array.Empty<(ContentItem Item, RouteInfo Route)>(),
                new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["visible/index.html"] = new SeoIndexEntry(visibleRoute, "https://example.com/visible/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), visible.Id, "post"),
                    ["hidden/index.html"] = new SeoIndexEntry(hiddenRoute, "https://example.com/hidden/", "noindex", false, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), hidden.Id, "post")
                },
                new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase));

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
            var item = Item("post", "Post");
            var context = new PublishProjectionContext(
                new AppConfig
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
                    Content = new ContentConfig { Provider = "markdown" }
                },
                outputDir,
                CanonicalContentGraph.Empty,
                [(item, route)],
                Array.Empty<(ContentItem Item, RouteInfo Route)>(),
                new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post/index.html"] = new SeoIndexEntry(route, "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), item.Id, "post")
                },
                new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
                BodyStore: new InlineBodyStore("<p>Post body</p>"),
                BaseUrl: "/");

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
    public void SearchProjection_GeneratesSearchUiPartial()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit_projection_search_ui_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        try
        {
            var route = new RouteInfo("/post/", "post/index.html", "post.html");
            var item = Item("post", "Post");
            var context = new PublishProjectionContext(
                new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "test",
                        Title = "Test",
                        Url = "https://example.com",
                        Search = new SearchDetailConfig { Ui = "default", PlaceholderText = "Find content" }
                    },
                    Content = new ContentConfig { Provider = "markdown" }
                },
                outputDir,
                CanonicalContentGraph.Empty,
                [(item, route)],
                Array.Empty<(ContentItem Item, RouteInfo Route)>(),
                new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post/index.html"] = new SeoIndexEntry(route, "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), item.Id, "post")
                },
                new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
                BodyStore: new InlineBodyStore("<p>Post body</p>"),
                BaseUrl: "/");

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

    private static ContentItem Item(string id, string title)
        => new(
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

    private sealed class InlineBodyStore : IContentBodyStore
    {
        private readonly string _html;

        public InlineBodyStore(string html)
        {
            _html = html;
        }

        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(_html));
    }
}
