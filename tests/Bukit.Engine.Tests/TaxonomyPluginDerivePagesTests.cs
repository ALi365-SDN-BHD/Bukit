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

public sealed class TaxonomyPluginDerivePagesTests
{
    private static BuildContext CreateContext(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        TaxonomyConfig? taxonomyConfig = null,
        string outputMode = "pages",
        string outputPathEncoding = "none",
        CanonicalContentGraph? contentGraph = null)
    {
        var layoutsDir = CreateTaxonomyLayoutsDir();
        var graph = contentGraph ?? CanonicalContentGraph.Empty;
        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test", OutputPathEncoding = outputPathEncoding },
                Content = new ContentConfig { Provider = "markdown" },
                Taxonomy = taxonomyConfig ?? new TaxonomyConfig
                {
                    OutputMode = outputMode,
                    IndexEnabled = true
                }
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = layoutsDir,
            Routed = routed,
            RoutedDocuments = routed.Select(x => (Document: ToDocument(x.Item, graph), x.Route)).ToList(),
            ContentGraph = graph,
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }

    private static string ResolveTemplateKind(string kind)
        => kind.Trim().ToLowerInvariant() switch
        {
            "taxonomy_index" => "pages/taxonomy-index.html",
            "taxonomy_term" => "pages/taxonomy-term.html",
            _ => throw new ConfigException($"Unexpected template kind: {kind}")
        };

    private static string CreateTaxonomyLayoutsDir()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-taxonomy-tests-" + Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-index.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-term.html"), "{{ page.content }}");
        return layoutsDir;
    }

    private static (ContentItem Item, RouteInfo Route) CreateItem(
        string id,
        string title,
        DateTimeOffset publishAt,
        string[]? tags = null,
        string[]? categories = null,
        bool? pinned = null)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (tags is { Length: > 0 })
        {
            fields["tags"] = new ContentField("list", tags);
        }

        if (categories is { Length: > 0 })
        {
            fields["categories"] = new ContentField("list", categories);
        }

        if (pinned.HasValue)
        {
            fields["pinned"] = new ContentField("bool", pinned.Value);
        }

        var item = new ContentItem(
            Id: id,
            Title: title,
            Slug: id,
            PublishAt: publishAt,
            ContentHtml: $"<p>{title}</p>",
            Fields: fields);
        var route = new RouteInfo($"/blog/{id}/", $"blog/{id}/index.html", "pages/post.html");
        return (item, route);
    }

    private static ContentDocument ToDocument(ContentItem item, CanonicalContentGraph? graph = null)
    {
        var graphRecord = graph?.Records.FirstOrDefault(x => x.Identity.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
        var fields = item.Fields is null
            ? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ContentField>(item.Fields, StringComparer.OrdinalIgnoreCase);
        var tags = GetStringList(fields, "tags");
        var categories = GetStringList(fields, "categories");

        var record = graphRecord ?? new ContentRecord(
            new ContentIdentity(item.Id, item.Slug, item.Id, GetString(fields, "type") ?? "post", "published"),
            new ContentPresentation(item.Title, GetString(fields, "summary"), item.ContentHtml, "en", []),
            new ContentClassification(GetString(fields, "type") ?? "post", GetString(fields, "collection") ?? "post", categories, tags),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(item.PublishAt, null, null, null),
            new ProvenanceRecord(GetString(fields, "sourceKey"), null, [], [], null),
            new TrustMetadata(null, "published", []),
            [],
            [],
            []);

        return new ContentDocument(
            record,
            new ContentBodyRef(item.ContentHtml, null, null, null),
            new ContentRoutePolicy(null, null, null, null, record.Classification.Collection),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            fields,
            []);
    }

    private static string? GetString(IReadOnlyDictionary<string, ContentField> fields, string key)
    {
        if (!fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        var value = field.Value.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<string> GetStringList(
        IReadOnlyDictionary<string, ContentField> fields,
        string key)
    {
        object? raw = null;
        if (fields.TryGetValue(key, out var field))
        {
            raw = field.Value;
        }

        return raw switch
        {
            string text => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            IEnumerable<string> list => list.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
            IEnumerable<object> list => list.Select(x => x?.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
            _ => []
        };
    }

    [Fact]
    public void DerivePages_GeneratesTermPagesForTags()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha", "beta" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/beta/");
    }

    [Fact]
    public void DerivePages_GeneratesTermPagesForCategories()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: new[] { "News", "Tech" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/categories/news/");
        Assert.Contains(derived, x => x.Route.Url == "/categories/tech/");
    }

    [Fact]
    public void DerivePages_GeneratesIndexPages()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
    }

    [Fact]
    public void DerivePages_OutputPathEncodingSanitize_AppliesToTermOutputPath()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "hello world" }),
        };
        var ctx = CreateContext(routed, outputPathEncoding: "sanitize");

        var derived = new TaxonomyPlugin().DerivePages(ctx);

        var term = Assert.Single(derived, x => x.Route.Url == "/tags/hello-world/");
        Assert.Equal("tags/hello-world/index.html", term.Route.OutputPath);
    }

    [Fact]
    public void DerivePages_PinnedItemsSortedFirst()
    {
        var unpinned = CreateItem("p1", "Not Pinned", new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "topic" }, pinned: false);
        var pinned = CreateItem("p2", "Pinned", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "topic" }, pinned: true);
        var routed = new List<(ContentItem Item, RouteInfo Route)> { unpinned, pinned };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        var termPage = Assert.Single(derived, x => x.Route.Url == "/tags/topic/");
        Assert.NotNull(termPage.Item.Fields);
        Assert.True(termPage.Item.Fields!.ContainsKey("items"));
        var items = Assert.IsType<List<object>>(termPage.Item.Fields["items"].Value);
        Assert.Equal(2, items.Count);
        var first = Assert.IsType<Dictionary<string, object>>(items[0]);
        Assert.Equal("Pinned", first["title"]);
    }

    [Fact]
    public void DerivePages_MultipleTaxonomyKinds()
    {
        var config = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = true,
            Kinds = new List<TaxonomyKindConfig>
            {
                new() { Key = "series", Kind = "series", Title = "Series", SingularTitlePrefix = "Series" },
                new() { Key = "authors", Kind = "authors", Title = "Authors", SingularTitlePrefix = "Author" }
            }
        };
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var fields = routed[0].Item.Fields as Dictionary<string, ContentField>;
        fields!["series"] = new ContentField("list", new[] { "My Series" });
        fields["authors"] = new ContentField("list", new[] { "Alice" });
        var ctx = CreateContext(routed, taxonomyConfig: config);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/series/");
        Assert.Contains(derived, x => x.Route.Url == "/series/my-series/");
        Assert.Contains(derived, x => x.Route.Url == "/authors/");
        Assert.Contains(derived, x => x.Route.Url == "/authors/alice/");
        Assert.DoesNotContain(derived, x => x.Route.Url.StartsWith("/tags/"));
    }

    [Fact]
    public void DerivePages_EmptyItems_ReturnsEmpty()
    {
        var ctx = CreateContext(new List<(ContentItem Item, RouteInfo Route)>());

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_ItemsWithoutTaxonomy_ReturnsEmpty()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_ItemsWithCategoriesOnly_NoTagsPages()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), categories: new[] { "News" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.DoesNotContain(derived, x => x.Route.Url.StartsWith("/tags/"));
        Assert.Contains(derived, x => x.Route.Url == "/categories/");
        Assert.Contains(derived, x => x.Route.Url == "/categories/news/");
    }

    [Fact]
    public void DerivePages_UsesStructuredTaxonomyAndSummary()
    {
        var publishAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var item = new ContentItem(
            Id: "p1",
            Title: "Post 1",
            Slug: "p1",
            PublishAt: publishAt,
            ContentHtml: "<p>Post 1</p>",
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["tags"] = new("list", new object[] { "alpha" }),
                ["summary"] = new("text", "Canonical taxonomy summary")
            });
        var route = new RouteInfo("/blog/p1/", "blog/p1/index.html", "pages/post.html");
        var ctx = CreateContext(new List<(ContentItem Item, RouteInfo Route)> { (item, route) });

        var derived = new TaxonomyPlugin().DerivePages(ctx);

        var termPage = Assert.Single(derived, x => x.Route.Url == "/tags/alpha/");
        var items = Assert.IsType<List<object>>(termPage.Item.Fields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(items[0]);
        Assert.Equal("Canonical taxonomy summary", first["summary"]);
    }

    [Fact]
    public void DerivePages_ShouldUseCanonicalGraphTaxonomy_WhenItemHasNoTaxonomyFields()
    {
        var publishAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var item = new ContentItem(
            Id: "p1",
            Title: "Post 1",
            Slug: "p1",
            PublishAt: publishAt,
            ContentHtml: "<p>Post 1</p>",
            Fields: null);
        var route = new RouteInfo("/blog/p1/", "blog/p1/index.html", "pages/post.html");
        var graph = new CanonicalContentGraph(
        [
            new ContentRecord(
                new ContentIdentity("p1", "p1", "p1", "post", "published"),
                new ContentPresentation("Post 1", "Canonical taxonomy summary", "<p>Post 1</p>", "en", []),
                new ContentClassification("post", "post", ["Canonical Category"], ["Canonical Tag"]),
                new ContentOwnership(null, null, null, null),
                new ContentLifecycle(publishAt, null, null, null),
                new ProvenanceRecord("markdown", null, [], [], null),
                new TrustMetadata(null, "published", []),
                [],
                [],
                [])
        ], []);
        var ctx = CreateContext(new List<(ContentItem Item, RouteInfo Route)> { (item, route) }, contentGraph: graph);

        var derived = new TaxonomyPlugin().DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/canonical-tag/");
        var categoryPage = Assert.Single(derived, x => x.Route.Url == "/categories/canonical-category/");
        var items = Assert.IsType<List<object>>(categoryPage.Item.Fields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(items[0]);
        Assert.Equal("Canonical taxonomy summary", first["summary"]);
    }

    [Fact]
    public void DerivePages_IndexEnabledFalse_NoIndexPages()
    {
        var taxonomyConfig = new TaxonomyConfig
        {
            OutputMode = "pages",
            IndexEnabled = false
        };
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = CreateContext(routed, taxonomyConfig: taxonomyConfig);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.DoesNotContain(derived, x => x.Route.Url == "/tags/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
    }

    [Fact]
    public void DerivePages_TermPageTitleContainsPrefix()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "AlphaTag" }),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        var termPage = Assert.Single(derived, x => x.Route.Url == "/tags/alphatag/");
        Assert.Equal("Tag: AlphaTag", termPage.Item.Title);
    }

    [Fact]
    public void DerivePages_CommaSeparatedTags_AreParsedCorrectly()
    {
        var item = new ContentItem(
            Id: "p1",
            Title: "Post 1",
            Slug: "post-1",
            PublishAt: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>content</p>",
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["tags"] = new ContentField("text", "alpha, beta, gamma")
            });
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            (item, new RouteInfo("/blog/p1/", "blog/p1/index.html", "pages/post.html")),
        };
        var ctx = CreateContext(routed);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/beta/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/gamma/");
    }

    [Fact]
    public void DerivePages_WithBaseUrl_PrefixIsApplied()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" },
                Taxonomy = new TaxonomyConfig { OutputMode = "pages", IndexEnabled = true }
            },
            RootDir = "/test",
            OutputDir = "/test/out",
            BaseUrl = "/my-site",
            LayoutsDir = CreateTaxonomyLayoutsDir(),
            Routed = routed,
            RoutedDocuments = routed.Select(x => (Document: ToDocument(x.Item), x.Route)).ToList(),
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/tags/");
        Assert.Contains(derived, x => x.Route.Url == "/tags/alpha/");
    }

    [Fact]
    public void DerivePages_OutputModeData_ReturnsEmpty()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>
        {
            CreateItem("p1", "Post 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), tags: new[] { "alpha" }),
        };
        var ctx = CreateContext(routed, outputMode: "data");

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Empty(derived);
    }
}
