using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoIndexBuilderTests
{
    private static AppConfig CreateConfig(bool seoEnabled = true)
    {
        return new AppConfig
        {
            Content = new ContentConfig { Provider = "markdown" },
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test Site",
                Url = "https://example.com",
                Language = "zh-CN",
                Seo = new SeoConfig
                {
                    Enabled = seoEnabled,
                    Schema = new SeoSchemaConfig
                    {
                        SearchAction = true,
                        WebPage = true,
                        CollectionPage = false
                    }
                }
            }
        };
    }

    [Fact]
    public void Build_SeoDisabled_ReturnsEmpty()
    {
        var config = CreateConfig(seoEnabled: false);
        var routed = new (ContentItem, RouteInfo)[]
        {
            (new ContentItem(
                Id: "p1",
                Title: "Page",
                Slug: "page",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: null,
                Meta: new Dictionary<string, object> { ["type"] = "page" }),
             new RouteInfo("/pages/page/", "pages/page/index.html", "pages/page.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed, Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        Assert.Empty(result.Entries);
        Assert.Empty(result.Models);
    }

    [Fact]
    public void Build_WithRoutedItems_CreatesEntriesAndModels()
    {
        var config = CreateConfig();
        var publishAt = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var routed = new (ContentItem, RouteInfo)[]
        {
            (new ContentItem(
                Id: "post-1",
                Title: "First Post",
                Slug: "first-post",
                PublishAt: publishAt,
                ContentHtml: null,
                Meta: new Dictionary<string, object>
                {
                    ["type"] = "post",
                    ["collection"] = "post",
                    ["summary"] = "First summary"
                }),
             new RouteInfo("/blog/first-post/", "blog/first-post/index.html", "pages/post.html")),
            (new ContentItem(
                Id: "page-1",
                Title: "About",
                Slug: "about",
                PublishAt: publishAt,
                ContentHtml: null,
                Meta: new Dictionary<string, object>
                {
                    ["type"] = "page",
                    ["summary"] = "About us"
                }),
             new RouteInfo("/pages/about/", "pages/about/index.html", "pages/page.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed, Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(2, result.Models.Count);

        Assert.True(result.Entries.ContainsKey("blog/first-post/index.html"));
        var postEntry = result.Entries["blog/first-post/index.html"];
        Assert.True(postEntry.Indexable);
        Assert.Equal("https://example.com/blog/first-post/", postEntry.Canonical);
        Assert.Equal("post-1", postEntry.SourceItemId);
        Assert.Equal("post", postEntry.ContentType);

        Assert.True(result.Entries.ContainsKey("pages/about/index.html"));
        var pageEntry = result.Entries["pages/about/index.html"];
        Assert.True(pageEntry.Indexable);
        Assert.Null(pageEntry.ContentType);

        Assert.True(result.Models.ContainsKey("blog/first-post/index.html"));
        Assert.True(result.Models.ContainsKey("pages/about/index.html"));
    }

    [Fact]
    public void Build_WithListRoutes_CreatesListEntries()
    {
        var config = CreateConfig();
        var routed = new (ContentItem, RouteInfo)[]
        {
            (new ContentItem(
                Id: "post-1",
                Title: "Post",
                Slug: "post",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: null,
                Meta: new Dictionary<string, object> { ["type"] = "post", ["collection"] = "post" }),
             new RouteInfo("/blog/post/", "blog/post/index.html", "pages/post.html"))
        };
        var listRoutes = new[]
        {
            new RouteInfo("/", "index.html", "pages/index.html"),
            new RouteInfo("/blog/", "blog/index.html", "pages/list.html")
        };

        var result = SeoIndexBuilder.Build(config, "/", routed, listRoutes, new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        Assert.True(result.Entries.ContainsKey("index.html"));
        Assert.True(result.Entries.ContainsKey("blog/index.html"));
        Assert.True(result.Entries.ContainsKey("blog/post/index.html"));

        var homeEntry = result.Entries["index.html"];
        Assert.Equal("list", homeEntry.ContentType);
        Assert.Null(homeEntry.SourceItemId);

        var blogEntry = result.Entries["blog/index.html"];
        Assert.Equal("list", blogEntry.ContentType);
    }

    [Fact]
    public void Build_EntryHasLastModified()
    {
        var config = CreateConfig();
        var publishAt = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var routed = new (ContentItem, RouteInfo)[]
        {
            (new ContentItem(
                Id: "post-1",
                Title: "Post",
                Slug: "post",
                PublishAt: publishAt,
                ContentHtml: null,
                Meta: new Dictionary<string, object> { ["type"] = "post", ["collection"] = "post" }),
             new RouteInfo("/blog/post/", "blog/post/index.html", "pages/post.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed, Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        var entry = result.Entries["blog/post/index.html"];
        Assert.True(entry.LastModified > DateTimeOffset.MinValue);
    }

    [Fact]
    public void Build_NonIndexableContent()
    {
        var config = CreateConfig();
        var routed = new (ContentItem, RouteInfo)[]
        {
            (new ContentItem(
                Id: "p1",
                Title: "Hidden",
                Slug: "hidden",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: null,
                Meta: new Dictionary<string, object>
                {
                    ["type"] = "page",
                    ["robots"] = "noindex"
                }),
             new RouteInfo("/pages/hidden/", "pages/hidden/index.html", "pages/page.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed, Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        var entry = result.Entries["pages/hidden/index.html"];
        Assert.False(entry.Indexable);
        Assert.Equal("noindex", entry.Robots);
    }

    [Fact]
    public void Build_WithAlternates_PassesToModels()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Translated",
            Slug: "translated",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>
            {
                ["type"] = "page",
                ["i18n_key"] = "page.translated"
            });
        var route = new RouteInfo("/pages/translated/", "pages/translated/index.html", "pages/page.html");
        var routed = new (ContentItem, RouteInfo)[] { (item, route) };
        var alternates = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.OrdinalIgnoreCase)
        {
            ["i18n:page.translated"] = new[]
            {
                new SeoAlternateModel("en", "https://example.com/pages/translated/"),
                new SeoAlternateModel("ja", "https://example.com/ja/pages/translated/")
            }
        };

        var result = SeoIndexBuilder.Build(config, "/", routed, Array.Empty<RouteInfo>(), alternates);

        var model = result.Models["pages/translated/index.html"];
        Assert.Equal(2, model.Alternates.Count);
    }

    [Fact]
    public void Build_BaseUrlIsPrependedToCanonical()
    {
        var config = CreateConfig();
        var routed = new (ContentItem, RouteInfo)[]
        {
            (new ContentItem(
                Id: "p1",
                Title: "Page",
                Slug: "page",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: null,
                Meta: new Dictionary<string, object> { ["type"] = "page" }),
             new RouteInfo("/pages/page/", "pages/page/index.html", "pages/page.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/zh", routed, Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        var entry = result.Entries["pages/page/index.html"];
        Assert.Equal("https://example.com/zh/pages/page/", entry.Canonical);
    }

    [Fact]
    public void Build_ListRoutesWithoutRouted_FieldsNull()
    {
        var config = CreateConfig();
        var listRoutes = new[]
        {
            new RouteInfo("/blog/", "blog/index.html", "pages/list.html")
        };

        var result = SeoIndexBuilder.Build(config, "/", Array.Empty<(ContentItem, RouteInfo)>(), listRoutes, new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        Assert.True(result.Entries.ContainsKey("blog/index.html"));
        Assert.Equal("list", result.Entries["blog/index.html"].ContentType);
    }
}
