using Bukit.Config;
using Bukit.Content;
using Bukit.Content.Markdown;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Engine.Output;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RssGeneratorTests
{
    private static ContentDocument Document(
        string slug = "my-slug",
        string type = "post",
        string? summary = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? publishAt = null) =>
        ContentDocument.Create(
            slug,
            "Title " + slug,
            slug,
            publishAt ?? new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            ContentFieldReader.ToFieldMap(BuildMeta(type, summary, tags)));

    private static IReadOnlyDictionary<string, object> BuildMeta(string type, string? summary, IReadOnlyList<string>? tags)
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["type"] = type,
            ["collection"] = type
        };
        if (summary is not null) fieldValues["summary"] = summary;
        if (tags is not null) fieldValues["tags"] = tags;
        return fieldValues;
    }

    private sealed class InMemoryBodyStore : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
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

        var documents = new List<RoutedContentDocument>
        {
            new(
                Document("post-1", "post", "Summary one", publishAt: new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
                new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };

        RssGenerator.Generate(outDir, "https://example.com", "/", "My Site", RssCollections, documents, new InMemoryBodyStore());

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
            Array.Empty<RoutedContentDocument>(),
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

        var documents = new List<RoutedContentDocument>();
        for (var i = 1; i <= 10; i++)
        {
            documents.Add(new RoutedContentDocument(
                Document($"post-{i}", "post",
                    publishAt: new DateTimeOffset(2024, 6, i, 0, 0, 0, TimeSpan.Zero)),
                new RouteInfo($"/blog/post-{i}/", $"blog/post-{i}/index.html", "pages/post.html")));
        }

        RssGenerator.Generate(outDir, "https://example.com", "/", "Site", RssCollections, documents, new InMemoryBodyStore(), maxItems: 3);

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

        var documents = new List<RoutedContentDocument>
        {
            new(
                Document("post-1", "post", publishAt: new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
                new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
            new(
                Document("page-1", "page", publishAt: new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero)),
                new RouteInfo("/pages/page-1/", "pages/page-1/index.html", "pages/page.html")),
        };

        var collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new() { Permalink = "/blog/{slug}/", Template = "pages/post.html", Output = new() { Rss = true } },
            ["page"] = new() { Permalink = "/pages/{slug}/", Template = "pages/page.html", Output = new() { Rss = false } },
        };

        RssGenerator.Generate(outDir, "https://example.com", "/", "Site", collections, documents, new InMemoryBodyStore());

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<title>Title post-1</title>", rss, StringComparison.Ordinal);
        Assert.DoesNotContain("<title>Title page-1</title>", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_DistinctTypeAndCollectionUsesCollectionAndExcludesCollectionlessDocument()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);
        var news = DocumentWithClassification("news-1", "article", "news");
        var articleCollection = DocumentWithClassification("article-1", "page", "article");
        var module = DocumentWithClassification("module-1", "module", string.Empty);
        var routed = new[]
        {
            new RoutedContentDocument(news, new RouteInfo("/news/news-1/", "news/news-1/index.html", "news.html")),
            new RoutedContentDocument(articleCollection, new RouteInfo("/articles/article-1/", "articles/article-1/index.html", "article.html")),
            new RoutedContentDocument(module, new RouteInfo("/modules/module-1/", "modules/module-1/index.html", "module.html"))
        };
        var collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["news"] = new() { Permalink = "/news/{slug}/", Output = new() { Rss = false } },
            ["article"] = new() { Permalink = "/articles/{slug}/", Output = new() { Rss = true } },
            ["module"] = new() { Permalink = "/modules/{slug}/", Output = new() { Rss = true } }
        };

        RssGenerator.Generate(outDir, "https://example.com", "/", "Site", collections, routed, new InMemoryBodyStore());

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.DoesNotContain("Title news-1", rss, StringComparison.Ordinal);
        Assert.Contains("Title article-1", rss, StringComparison.Ordinal);
        Assert.DoesNotContain("Title module-1", rss, StringComparison.Ordinal);
    }

    private static ContentDocument DocumentWithClassification(string slug, string type, string collection)
        => ContentDocument.Create(
            slug,
            "Title " + slug,
            slug,
            new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = type,
                ["collection"] = collection
            }));

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
    public void FeedGenerators_UseSameStableWindowAndPositiveDefaultLimit()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);
        var boundary = new DateTimeOffset(2024, 6, 3, 0, 0, 0, TimeSpan.Zero);
        var posts = new List<RssGenerator.Post>
        {
            new("B", "https://example.com/b/", boundary, null, null, null),
            new("A older duplicate", "https://example.com/a/", boundary.AddDays(-1), null, null, null),
            new("C", "https://example.com/c/", boundary.AddDays(-2), null, null, null),
            new("A", "https://example.com/a/", boundary, null, null, null)
        };

        RssGenerator.GenerateMerged(outDir, "https://example.com", "/", "Site", posts, maxItems: 2);
        AtomFeedGenerator.Generate(outDir, "https://example.com", "/", "Site", posts, "atom.xml", maxItems: 2);
        JsonFeedGenerator.Generate(outDir, "https://example.com", "/", "Site", posts, "feed.json", maxItems: 2);

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        var atom = File.ReadAllText(Path.Combine(outDir, "atom.xml"));
        var json = File.ReadAllText(Path.Combine(outDir, "feed.json"));
        foreach (var output in new[] { rss, atom, json })
        {
            Assert.Contains("https://example.com/a/", output, StringComparison.Ordinal);
            Assert.Contains("https://example.com/b/", output, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/c/", output, StringComparison.Ordinal);
            Assert.DoesNotContain("A older duplicate", output, StringComparison.Ordinal);
            Assert.True(
                output.IndexOf("https://example.com/a/", StringComparison.Ordinal) <
                output.IndexOf("https://example.com/b/", StringComparison.Ordinal));
        }

        RssGenerator.GenerateMerged(outDir, "https://example.com", "/", "Site", posts, maxItems: 0);
        Assert.Contains("https://example.com/c/", File.ReadAllText(Path.Combine(outDir, "rss.xml")), StringComparison.Ordinal);
    }

    [Fact]
    public void AtomFeed_EmptyInput_IsByteStableAndUsesUnixEpoch()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var firstDir = Path.Combine(root, "first");
        var secondDir = Path.Combine(root, "second");
        Directory.CreateDirectory(firstDir);
        Directory.CreateDirectory(secondDir);

        AtomFeedGenerator.Generate(firstDir, "https://example.com", "/", "Site", [], "atom.xml");
        AtomFeedGenerator.Generate(secondDir, "https://example.com", "/", "Site", [], "atom.xml");

        var first = File.ReadAllBytes(Path.Combine(firstDir, "atom.xml"));
        var second = File.ReadAllBytes(Path.Combine(secondDir, "atom.xml"));
        Assert.Equal(first, second);
        Assert.Contains("<updated>1970-01-01T00:00:00Z</updated>", System.Text.Encoding.UTF8.GetString(first), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarkdownFileTimestamp_DoesNotChangeCanonicalDocumentRouteHashOrFeed()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var contentDir = Path.Combine(root, "content");
        Directory.CreateDirectory(contentDir);
        var markdownPath = Path.Combine(contentDir, "stable.md");
        await File.WriteAllTextAsync(markdownPath, """
        ---
        title: Stable
        type: article
        collection: news
        ---
        Body
        """);

        File.SetLastWriteTimeUtc(markdownPath, new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        var first = await CaptureMarkdownProjectionAsync(contentDir, Path.Combine(root, "feed-first"));

        File.SetLastWriteTimeUtc(markdownPath, new DateTime(2026, 7, 8, 9, 10, 11, DateTimeKind.Utc));
        var second = await CaptureMarkdownProjectionAsync(contentDir, Path.Combine(root, "feed-second"));

        Assert.Equal(first.CanonicalRecord, second.CanonicalRecord);
        Assert.Equal(first.RouteHash, second.RouteHash);
        Assert.Equal(first.Feed, second.Feed);
    }

    private static async Task<(string CanonicalRecord, string RouteHash, byte[] Feed)> CaptureMarkdownProjectionAsync(
        string contentDir,
        string outputDir)
    {
        var raw = Assert.Single((await new MarkdownFolderProvider(new MarkdownFolderProviderOptions(contentDir)).LoadRawAsync()).Documents);
        var document = ContentDocumentNormalizer.ToDocument(raw);
        var route = RouteGenerator.Generate(
            document,
            collections: new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["news"] = new("/{year}/{month}/{day}/{slug}/", "pages/article.html")
            });
        Directory.CreateDirectory(outputDir);
        RssGenerator.Generate(
            outputDir,
            "https://example.com",
            "/",
            "Site",
            new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["news"] = new() { Permalink = "/{year}/{month}/{day}/{slug}/", Output = new() { Rss = true } }
            },
            [new RoutedContentDocument(document, route)],
            new InMemoryBodyStore());

        return (
            JsonSerializer.Serialize(document.Record),
            IncrementalBuildEngine.ComputeRouteHash(route),
            File.ReadAllBytes(Path.Combine(outputDir, "rss.xml")));
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

        var documents = new List<RoutedContentDocument>
        {
            new(
                Document("post-1", "post", "Summary", tags: new[] { "tech", "dotnet" }),
                new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };

        RssGenerator.Generate(outDir, "https://example.com", "/", "Site", RssCollections, documents, new InMemoryBodyStore());

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<category>tech</category>", rss, StringComparison.Ordinal);
        Assert.Contains("<category>dotnet</category>", rss, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPost_PrefersCanonicalContentMetadata()
    {
        var sourceDocument = Document("post-1", "post", "Meta summary", tags: new[] { "tech" });
        var record = new ContentRecord(
            new ContentIdentity("post-1", "post-1", "post-1", "post", "published"),
            new ContentPresentation("Canonical Title", "Canonical summary", "<p>Body</p>", "en", []),
            new ContentClassification("post", "post", ["guides"], ["canonical-tag"]),
            new ContentOwnership("Ali", null, null, null),
            new ContentLifecycle(sourceDocument.PublishAt, null, null, null),
            new ProvenanceRecord("notion", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [new EntityRecord("company", "Bukit")],
            [],
            []);
        var document = new ContentDocument(
            record,
            new ContentBodyRef(sourceDocument.Body.Html, sourceDocument.Body.BodyKey),
            ContentRoutePolicy.FromFields(sourceDocument.CustomFields),
            ContentPublishPolicy.FromFields(sourceDocument.CustomFields),
            sourceDocument.CustomFields);

        var post = RssGenerator.ToPost(document, "https://example.com/blog/post-1/", new InMemoryBodyStore());

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
    public void GenerateJsonFeed_ShouldIncludeCanonicalSummaryWithoutProviderSource()
    {
        const string relatedNotionId = "aaaaaaaa-1111-4222-8333-bbbbbbbbbbbb";
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
                Source: "notion",
                Entities: ["Bukit", relatedNotionId])
        };

        JsonFeedGenerator.Generate(outDir, "https://example.com", "/", "Site", posts, "feed.json");

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(outDir, "feed.json")));
        var item = doc.RootElement.GetProperty("items")[0];
        Assert.Equal("Canonical feed summary", item.GetProperty("summary").GetString());
        var extension = item.GetProperty("_bukit");
        Assert.Equal("Bukit", Assert.Single(extension.GetProperty("entities").EnumerateArray()).GetString());
        Assert.DoesNotContain(relatedNotionId, File.ReadAllText(Path.Combine(outDir, "feed.json")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateJsonFeed_RejectsTraversalBeforeWritingOutsideOutputRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-json-feed-safety-" + Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        var escapedPath = Path.Combine(root, "escaped-feed", "feed.json");

        try
        {
            Directory.CreateDirectory(outDir);

            Assert.Throws<OutputPathSecurityException>(() =>
                JsonFeedGenerator.Generate(
                    outDir,
                    "https://example.com",
                    "/",
                    "Site",
                    Array.Empty<RssGenerator.Post>(),
                    "../escaped-feed/feed.json"));

            Assert.False(File.Exists(escapedPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Generate_SubpathBaseUrl()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var documents = new List<RoutedContentDocument>
        {
            new(
                Document("post-1", "post", publishAt: new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
                new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
        };

        RssGenerator.Generate(outDir, "https://example.com", "/my-repo", "Site", RssCollections, documents, new InMemoryBodyStore());

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
}
