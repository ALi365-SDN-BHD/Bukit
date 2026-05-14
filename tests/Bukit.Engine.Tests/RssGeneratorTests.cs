using Bukit.Config;
using Bukit.Content;
using Bukit.Routing;
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
            Meta: BuildMeta(type, summary, tags));

    private static IReadOnlyDictionary<string, object> BuildMeta(string type, string? summary, IReadOnlyList<string>? tags)
    {
        var meta = new Dictionary<string, object> { ["type"] = type };
        if (summary is not null) meta["summary"] = summary;
        if (tags is not null) meta["tags"] = tags;
        return meta;
    }

    private sealed class InMemoryBodyStore : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ContentBody($"<p>Body for {item.Slug}</p>"));
        }
    }

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

        RssGenerator.Generate(outDir, "https://example.com", "/", "My Site", null, items, new InMemoryBodyStore());

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

        RssGenerator.Generate(outDir, "https://example.com", "/", "Site", null, items, new InMemoryBodyStore(), maxItems: 3);

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

        RssGenerator.Generate(outDir, "https://example.com", "/", "Site", null, items, new InMemoryBodyStore());

        var rss = File.ReadAllText(Path.Combine(outDir, "rss.xml"));
        Assert.Contains("<category>tech</category>", rss, StringComparison.Ordinal);
        Assert.Contains("<category>dotnet</category>", rss, StringComparison.Ordinal);
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

        RssGenerator.Generate(outDir, "https://example.com", "/my-repo", "Site", null, items, new InMemoryBodyStore());

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
