using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RssGeneratorTests
{
    private static ContentItem Item(
        string slug = "my-slug",
        string type = "post",
        string? summary = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? publishAt = null) =>
        new(
            Id: slug,
            Title: "Title " + slug,
            Slug: slug,
            PublishAt: publishAt ?? new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: null,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: BuildFields(type, summary, tags));

    private static IReadOnlyDictionary<string, ContentField> BuildFields(string type, string? summary, IReadOnlyList<string>? tags)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", type),
            ["collection"] = new("text", type)
        };
        if (summary is not null) fields["summary"] = new("text", summary);
        if (tags is not null) fields["tags"] = new("list", tags);
        return fields;
    }

    private sealed class InMemoryBodyStore : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ContentBody($"<p>Body for {item.Slug}</p>"));
        }
    }

    private static readonly IReadOnlyDictionary<string, CollectionConfig> RssCollections =
        new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new() { Permalink = "/blog/{slug}/", Template = "pages/post.html", Output = new() { Rss = true } }
        };

    [Fact]
    public void Generate_BasicRss_HasCorrectStructure()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var items = new List<(ContentItem Item, RouteInfo Route)>
        {
            (Item("post-1", "post", "Summary one", publishAt: new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
             new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };

        RssGenerator.Generate(
            outDir,
            "https://example.com",
            "/",
            "My Site",
            RssCollections,
            items,
            new InMemoryBodyStore(),
            contentGraph: GraphFor(items[0].Item, summary: "Summary one", tags: []));

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", rss, StringComparison.Ordinal);
        Assert.Contains("<rss version=\"2.0\"", rss, StringComparison.Ordinal);
        Assert.Contains("<title>My Site</title>", rss, StringComparison.Ordinal);
        Assert.Contains("<link>https://example.com/</link>", rss, StringComparison.Ordinal);
        Assert.Contains("<generator>bukit</generator>", rss, StringComparison.Ordinal);
        Assert.Contains("<title>Title post-1</title>", rss, StringComparison.Ordinal);
        Assert.Contains("<link>https://example.com/blog/post-1/</link>", rss, StringComparison.Ordinal);
        Assert.Contains("<guid>https://example.com/blog/post-1/</guid>", rss, StringComparison.Ordinal);
        Assert.Contains("<description>Summary one</description>", rss, StringComparison.Ordinal);
        Assert.Contains("<atom:link", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_UsesSiteDescriptionForChannelDescription()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        RssGenerator.Generate(
            outDir,
            "https://example.com",
            "/",
            "My Site",
            null,
            Array.Empty<(ContentItem Item, RouteInfo Route)>(),
            new InMemoryBodyStore(),
            siteDescription: "Useful site summary");

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<description>Useful site summary</description>", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RespectsMaxItems()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var items = new List<(ContentItem Item, RouteInfo Route)>();
        for (var i = 1; i <= 10; i++)
        {
            items.Add((Item($"post-{i}", "post",
                         publishAt: new DateTimeOffset(2024, 6, i, 0, 0, 0, TimeSpan.Zero)),
                       new RouteInfo($"/blog/post-{i}/", $"blog/post-{i}/index.html", "pages/post.html")));
        }

        RssGenerator.Generate(outDir, "https://example.com", "/", "Site", RssCollections, items, new InMemoryBodyStore(), maxItems: 3);

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<title>Title post-10</title>", rss, StringComparison.Ordinal);
        Assert.Contains("<title>Title post-9</title>", rss, StringComparison.Ordinal);
        Assert.Contains("<title>Title post-8</title>", rss, StringComparison.Ordinal);
        Assert.DoesNotContain("<title>Title post-1</title>", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RssEnabledCollections_FiltersByCollection()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var items = new List<(ContentItem Item, RouteInfo Route)>
        {
            (Item("post-1", "post", publishAt: new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
             new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
            (Item("page-1", "page", publishAt: new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero)),
             new RouteInfo("/pages/page-1/", "pages/page-1/index.html", "pages/page.html")),
        };

        var collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new() { Permalink = "/blog/{slug}/", Template = "pages/post.html", Output = new() { Rss = true } },
            ["page"] = new() { Permalink = "/pages/{slug}/", Template = "pages/page.html", Output = new() { Rss = false } },
        };

        RssGenerator.Generate(outDir, "https://example.com", "/", "Site", collections, items, new InMemoryBodyStore());

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<title>Title post-1</title>", rss, StringComparison.Ordinal);
        Assert.DoesNotContain("<title>Title page-1</title>", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMerged_DeduplicatesByUrl()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var posts = new List<RssGenerator.Post>
        {
            new("Post A", "https://example.com/blog/a/", new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero), null, null, null),
            new("Post A Duplicate", "https://example.com/blog/a/", new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero), null, null, null),
            new("Post B", "https://example.com/blog/b/", new DateTimeOffset(2024, 6, 3, 0, 0, 0, TimeSpan.Zero), null, null, null),
        };

        RssGenerator.GenerateMerged(outDir, "https://example.com", "/", "Site", posts);

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<title>Post B</title>", rss, StringComparison.Ordinal);
        Assert.Contains("<title>Post A Duplicate</title>", rss, StringComparison.Ordinal);
        Assert.DoesNotContain("<title>Post A</title>", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMerged_UsesSiteDescriptionForChannelDescription()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        RssGenerator.GenerateMerged(
            outDir,
            "https://example.com",
            "/",
            "Site",
            Array.Empty<RssGenerator.Post>(),
            siteDescription: "Merged feed summary");

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<description>Merged feed summary</description>", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CategoriesInRss()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var items = new List<(ContentItem Item, RouteInfo Route)>
        {
            (Item("post-1", "post", "Summary", tags: new[] { "tech", "dotnet" }),
             new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };

        RssGenerator.Generate(
            outDir,
            "https://example.com",
            "/",
            "Site",
            RssCollections,
            items,
            new InMemoryBodyStore(),
            contentGraph: GraphFor(items[0].Item, summary: "Summary", tags: ["tech", "dotnet"]));

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<category>tech</category>", rss, StringComparison.Ordinal);
        Assert.Contains("<category>dotnet</category>", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPost_PrefersCanonicalContentMetadata()
    {
        var item = Item("post-1", "post", "Meta summary", tags: new[] { "tech" });
        var record = new ContentRecord(
            new ContentIdentity("post-1", "post-1", "post-1", "post", "published"),
            new ContentPresentation("Canonical Title", "Canonical summary", "<p>Body</p>", "en", []),
            new ContentClassification("post", "post", ["guides"], ["canonical-tag"]),
            new ContentOwnership("Ali", null, null, null),
            new ContentLifecycle(item.PublishAt, null, null, null),
            new ProvenanceRecord("notion", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [new EntityRecord("company", "Bukit")],
            [],
            []);

        var post = RssGenerator.ToPost(item, "https://example.com/blog/post-1/", new InMemoryBodyStore(), record);

        Assert.Equal("Canonical Title", post.Title);
        Assert.Equal("Canonical summary", post.Description);
        Assert.Equal("Ali", post.Author);
        Assert.Equal("en", post.Language);
        Assert.Equal("approved", post.ReviewStatus);
        Assert.Contains("canonical-tag", post.Categories!);
        Assert.Contains("guides", post.Categories!);
        Assert.Contains("Bukit", post.Entities!);
    }

    [Fact]
    public void ToPost_ContentDocument_UsesCanonicalDocumentMetadata()
    {
        var record = new ContentRecord(
            new ContentIdentity("doc-1", "doc-1", "doc-1", "guide", "published"),
            new ContentPresentation("Document Title", "Document summary", "<p>Document body</p>", "en", []),
            new ContentClassification("guide", "docs", ["guides"], ["canonical-tag"]),
            new ContentOwnership("Ali", null, null, null),
            new ContentLifecycle(new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero), null, null, null),
            new ProvenanceRecord("notion", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [new EntityRecord("company", "Bukit")],
            [],
            []);
        var document = new ContentDocument(
            record,
            new ContentBodyRef("<p>Document body</p>", null, null, null),
            new ContentRoutePolicy(null, null, null, null, "docs"),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<ContentDiagnostic>());

        var post = RssGenerator.ToPost(document, "https://example.com/docs/doc-1/");

        Assert.Equal("Document Title", post.Title);
        Assert.Equal("Document summary", post.Description);
        Assert.Equal("<p>Document body</p>", post.ContentHtml);
        Assert.Equal("Ali", post.Author);
        Assert.Equal("en", post.Language);
        Assert.Equal("notion", post.Source);
        Assert.Equal("approved", post.ReviewStatus);
        Assert.Contains("canonical-tag", post.Categories!);
        Assert.Contains("guides", post.Categories!);
        Assert.Contains("Bukit", post.Entities!);
    }

    [Fact]
    public void GenerateJsonFeed_ShouldIncludeCanonicalSummaryAndSource_WhenSourceHasNoEntities()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);
        var posts = new List<RssGenerator.Post>
        {
            new(
                "Canonical Post",
                "https://example.com/blog/post-1/",
                new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
                "Canonical feed summary",
                new[] { "tag" },
                "<p>Body</p>",
                Source: "notion")
        };

        JsonFeedGenerator.Generate(outDir, "https://example.com", "/", "Site", posts, "feed.json");

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(outDir, "feed.json")));
        var item = doc.RootElement.GetProperty("items")[0];
        Assert.Equal("Canonical feed summary", item.GetProperty("summary").GetString());
        Assert.Equal("notion", item.GetProperty("_bukit").GetProperty("source").GetString());
    }

    [Fact]
    public void Generate_SubpathBaseUrl()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var items = new List<(ContentItem Item, RouteInfo Route)>
        {
            (Item("post-1", "post", publishAt: new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
             new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };

        RssGenerator.Generate(outDir, "https://example.com", "/my-repo", "Site", RssCollections, items, new InMemoryBodyStore());

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<link>https://example.com/my-repo/blog/post-1/</link>", rss, StringComparison.Ordinal);
        Assert.Contains("<atom:link href=\"https://example.com/my-repo/rss.xml\"", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildAbsoluteUrl_CombinesCorrectly()
    {
        var url = RssGenerator.BuildAbsoluteUrl("https://example.com", "/", "/blog/hello/");
        Assert.Equal("https://example.com/blog/hello/", url);
    }

    [Fact]
    public void BuildAbsoluteUrl_NormalizesTrailingSlashSiteUrl()
    {
        var url = RssGenerator.BuildAbsoluteUrl("https://example.com/", "/docs/", "/blog/hello/");
        Assert.Equal("https://example.com/docs/blog/hello/", url);
    }

    private static CanonicalContentGraph GraphFor(ContentItem item, string? summary, IReadOnlyList<string> tags)
    {
        var record = new ContentRecord(
            new ContentIdentity(item.Id, item.Slug, item.Id, "post", "published"),
            new ContentPresentation(item.Title, summary, item.ContentHtml, "en", []),
            new ContentClassification("post", "post", [], tags),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(item.PublishAt, null, null, null),
            new ProvenanceRecord("markdown", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [],
            [],
            []);

        return new CanonicalContentGraph([record], []);
    }
}
