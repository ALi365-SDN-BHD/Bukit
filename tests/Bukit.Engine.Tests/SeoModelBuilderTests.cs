using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoModelBuilderTests
{
    private static AppConfig CreateConfig(string siteUrl = "https://example.com", string siteTitle = "My Site", string? defaultImage = null)
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = siteTitle,
                Title = siteTitle,
                Url = siteUrl,
                Language = "zh-CN",
                Seo = new SeoConfig
                {
                    Enabled = true,
                    DefaultImage = defaultImage,
                    Schema = new SeoSchemaConfig
                    {
                        SearchAction = true,
                        WebPage = true,
                        CollectionPage = false
                    }
                }
            },
            Content = new ContentConfig
            {
                Provider = "markdown",
                Markdown = new MarkdownConfig()
            }
        };
    }

    [Fact]
    public void BuildForContent_WithFullItem_SetsAllProperties()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "post-1",
            Title: "Hello World",
            Slug: "hello-world",
            PublishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>hello</p>",
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["schema_type"] = "BlogPosting",
                ["author"] = "Alice",
                ["tags"] = "dotnet,aspire",
                ["summary"] = "A test post"
            }));
        var route = new RouteInfo("/blog/hello-world/", "blog/hello-world/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/zh", item, route);

        Assert.Equal("Hello World", model.Title);
        Assert.Equal("A test post", model.Description);
        Assert.Equal("https://example.com/zh/blog/hello-world/", model.Canonical);
        Assert.NotNull(model.Og);
        Assert.Equal("Hello World", model.Og.Title);
        Assert.Equal("article", model.Og.Type);
        Assert.Equal("My Site", model.Og.SiteName);
        Assert.Equal("zh-CN", model.Og.Locale);
        Assert.Equal("https://example.com/zh/blog/hello-world/", model.Og.Url);
        Assert.NotNull(model.Twitter);
        Assert.Equal("Hello World", model.Twitter.Title);
        Assert.NotNull(model.Article);
        Assert.NotNull(model.Article.PublishedTime);
        Assert.Equal("Alice", model.Article.Author);
        Assert.Equal(new[] { "dotnet", "aspire" }, model.Article.Tags);
        Assert.NotNull(model.JsonLd);
        Assert.NotEmpty(model.JsonLd);
    }

    [Fact]
    public void BuildForContent_SeoTitleOverridesTitle()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Original",
            Slug: "original",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["seo_title"] = "SEO Title"
            }));
        var route = new RouteInfo("/pages/original/", "pages/original/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("SEO Title", model.Title);
        Assert.Equal("SEO Title", model.Og.Title);
    }

    [Fact]
    public void BuildForContent_SeoDescriptionOverridesSummary()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["seo_desc"] = "Custom desc",
                ["summary"] = "Summary"
            }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("Custom desc", model.Description);
    }

    [Fact]
    public void BuildForContent_PrefersCanonicalFieldsForSummaryTagsAndLanguage()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Canonical",
            Slug: "canonical",
            PublishAt: DateTimeOffset.Parse("2026-06-05T10:00:00Z"),
            ContentHtml: null,
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["summary"] = new("text", "Canonical summary"),
                ["author"] = new("text", "Canonical Author"),
                ["tags"] = new("list", new object[] { "canonical-tag", "canonical-second" }),
                ["language"] = new("text", "ms-MY")
            });
        var route = new RouteInfo("/posts/canonical/", "posts/canonical/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("Canonical summary", model.Description);
        Assert.Equal("Canonical Author", model.Article.Author);
        Assert.Equal(new[] { "canonical-tag", "canonical-second" }, model.Article.Tags);

        var articleJson = model.JsonLd
            .Select(static json => JsonDocument.Parse(json))
            .First(doc => doc.RootElement.TryGetProperty("@type", out var type) &&
                          type.GetString() == "BlogPosting");
        Assert.Equal("ms-MY", articleJson.RootElement.GetProperty("inLanguage").GetString());
        Assert.Equal("canonical-tag", articleJson.RootElement.GetProperty("keywords")[0].GetString());
    }

    [Fact]
    public void BuildForContent_LegacySeoFieldsFallbackToStandardSeo()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Original",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["seotitle"] = "Legacy SEO Title",
                ["seodesc"] = "Legacy SEO Desc",
                ["summary"] = "Summary"
            }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("Legacy SEO Title", model.Title);
        Assert.Equal("Legacy SEO Desc", model.Description);
    }

    [Fact]
    public void BuildForContent_NonPost_UsesWebsiteOgType()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "About",
            Slug: "about",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
        var route = new RouteInfo("/pages/about/", "pages/about/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("website", model.Og.Type);
        Assert.Null(model.Article.PublishedTime);
        Assert.Null(model.Article.Author);
        Assert.Empty(model.Article.Tags);
    }

    [Fact]
    public void BuildForContent_RobotsFromMeta()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Noindex",
            Slug: "noindex",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["robots"] = "noindex"
            }));
        var route = new RouteInfo("/pages/noindex/", "pages/noindex/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("noindex", model.Robots);
    }

    [Fact]
    public void BuildForContent_WithAlternates()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Translated",
            Slug: "translated",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["i18n_key"] = "page.translated"
            }));
        var route = new RouteInfo("/pages/translated/", "pages/translated/index.html", "pages/page.html");
        var alternates = new[]
        {
            new SeoAlternateModel("en", "https://example.com/pages/translated/"),
            new SeoAlternateModel("ja", "https://example.com/ja/pages/translated/")
        };

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route, alternates);

        Assert.Equal(2, model.Alternates.Count);
        Assert.Equal("en", model.Alternates[0].Hreflang);
        Assert.Equal("ja", model.Alternates[1].Hreflang);
    }

    [Fact]
    public void BuildForList_CreatesListSeoModel()
    {
        var config = CreateConfig(defaultImage: "/images/default.jpg");
        var page = new PageInfo
        {
            Title = "Blog",
            Url = "/blog/",
            Content = string.Empty,
            Summary = "All posts"
        };

        var model = SeoModelBuilder.BuildForList(config, "/", page);

        Assert.Equal("Blog", model.Title);
        Assert.Equal("All posts", model.Description);
        Assert.Equal("https://example.com/blog/", model.Canonical);
        Assert.Equal("website", model.Og.Type);
        Assert.NotNull(model.Og.Image);
        Assert.Equal("summary_large_image", model.Twitter.Card);
        Assert.NotNull(model.JsonLd);
    }

    [Fact]
    public void BuildForList_FallsBackToSiteDescription()
    {
        var config = CreateConfig();
        var page = new PageInfo
        {
            Title = "Blog",
            Url = "/blog/",
            Content = string.Empty,
            Summary = null
        };

        var model = SeoModelBuilder.BuildForList(config, "/", page);

        Assert.Equal("Blog", model.Title);
        Assert.Null(model.Description);
    }

    [Fact]
    public void BuildAbsoluteUrl_WithNoSiteUrl_ReturnsPath()
    {
        var result = SeoModelBuilder.BuildAbsoluteUrl(null, "/", "/blog/hello/");

        Assert.Equal("/blog/hello/", result);
    }

    [Fact]
    public void BuildAbsoluteUrl_WithBaseUrl()
    {
        var result = SeoModelBuilder.BuildAbsoluteUrl(null, "/zh/", "/blog/hello/");

        Assert.Equal("/zh/blog/hello/", result);
    }

    [Fact]
    public void BuildAbsoluteUrl_WithSiteUrl()
    {
        var result = SeoModelBuilder.BuildAbsoluteUrl("https://example.com", "/", "/blog/hello/");

        Assert.Equal("https://example.com/blog/hello/", result);
    }

    [Fact]
    public void BuildAbsoluteUrl_WithSiteUrlAndBaseUrl()
    {
        var result = SeoModelBuilder.BuildAbsoluteUrl("https://example.com", "/zh", "/blog/hello/");

        Assert.Equal("https://example.com/zh/blog/hello/", result);
    }

    [Fact]
    public void BuildAlternateKey_WithI18nKey()
    {
        var item = new ContentItem(
            Id: "p1",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["i18nKey"] = "page.about" }));
        var route = new RouteInfo("/pages/about/", "pages/about/index.html", "pages/page.html");

        var key = SeoModelBuilder.BuildAlternateKey(item, route);

        Assert.Equal("i18n:page.about", key);
    }

    [Fact]
    public void BuildAlternateKey_WithoutI18nKey_UsesRoute()
    {
        var item = new ContentItem(
            Id: "p1",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));
        var route = new RouteInfo("/pages/about/", "pages/about/index.html", "pages/page.html");

        var key = SeoModelBuilder.BuildAlternateKey(item, route);

        Assert.Equal("route:/pages/about/", key);
    }

    [Fact]
    public void BuildListAlternateKey_UsesRouteUrl()
    {
        var route = new RouteInfo("/blog/", "blog/index.html", "pages/list.html");

        var key = SeoModelBuilder.BuildListAlternateKey(route);

        Assert.Equal("route:/blog/", key);
    }

    [Fact]
    public void IsIndexable_NullOrEmpty_ReturnsTrue()
    {
        Assert.True(SeoModelBuilder.IsIndexable(null));
        Assert.True(SeoModelBuilder.IsIndexable(""));
        Assert.True(SeoModelBuilder.IsIndexable("   "));
    }

    [Fact]
    public void IsIndexable_Noindex_ReturnsFalse()
    {
        Assert.False(SeoModelBuilder.IsIndexable("noindex"));
        Assert.False(SeoModelBuilder.IsIndexable("NOINDEX"));
    }

    [Fact]
    public void IsIndexable_None_ReturnsFalse()
    {
        Assert.False(SeoModelBuilder.IsIndexable("none"));
        Assert.False(SeoModelBuilder.IsIndexable("NONE"));
    }

    [Fact]
    public void IsIndexable_WithMultipleDirectives()
    {
        Assert.True(SeoModelBuilder.IsIndexable("index,follow"));
        Assert.False(SeoModelBuilder.IsIndexable("noindex,follow"));
        Assert.False(SeoModelBuilder.IsIndexable("follow,none"));
    }

    [Fact]
    public void BuildForContent_WithoutImage_UsesSummaryTwitterCard()
    {
        var config = CreateConfig(defaultImage: null);
        var item = new ContentItem(
            Id: "p1",
            Title: "No Image",
            Slug: "no-image",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
        var route = new RouteInfo("/pages/no-image/", "pages/no-image/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("summary", model.Twitter.Card);
    }

    [Fact]
    public void BuildForList_WithoutImage_UsesSummaryTwitterCard()
    {
        var config = CreateConfig(defaultImage: null);
        var page = new PageInfo
        {
            Title = "Blog",
            Url = "/blog/",
            Content = string.Empty
        };

        var model = SeoModelBuilder.BuildForList(config, "/", page);

        Assert.Equal("summary", model.Twitter.Card);
    }

    [Fact]
    public void BuildForContent_ImageFromOgImageField()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["og_image"] = "/images/custom.jpg"
            }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("https://example.com/images/custom.jpg", model.Og.Image);
        Assert.Equal("summary_large_image", model.Twitter.Card);
    }

    [Fact]
    public void BuildForContent_ImageFromCoverField()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["cover"] = "/covers/main.jpg"
            }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("https://example.com/covers/main.jpg", model.Og.Image);
    }

    [Fact]
    public void BuildForContent_JsonLdContainsWebSite()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains("WebSite", model.JsonLd[0]);
        Assert.Contains("SearchAction", model.JsonLd[0]);
    }

    [Fact]
    public void BuildForContent_JsonLdContainsWebPage()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        var webPageJson = model.JsonLd[1];
        Assert.Contains("WebPage", webPageJson);
        Assert.Contains("Test", webPageJson);
    }

    [Fact]
    public void BuildForContent_PostHasBlogPostingJsonLd()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "post-1",
            Title: "My Post",
            Slug: "my-post",
            PublishAt: new DateTimeOffset(2025, 3, 10, 8, 0, 0, TimeSpan.Zero),
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["schema_type"] = "BlogPosting",
                ["author"] = "Bob",
                ["tags"] = "tech,code"
            }));
        var route = new RouteInfo("/blog/my-post/", "blog/my-post/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        var blogPostingJson = model.JsonLd[model.JsonLd.Count - 1];
        Assert.Contains("BlogPosting", blogPostingJson);
        Assert.Contains("My Post", blogPostingJson);
        Assert.Contains("Bob", blogPostingJson);
        Assert.Contains("tech", blogPostingJson);
        Assert.Contains("code", blogPostingJson);
    }

    [Fact]
    public void BuildForContent_PageTypeWithPostCollectionDoesNotEmitBlogPostingJsonLd()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "blog-archive-2026",
            Title: "Archive: 2026",
            Slug: "archive-2026",
            PublishAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["collection"] = "post"
            }));
        var route = new RouteInfo("/blog/archive/2026/", "blog/archive/2026/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.DoesNotContain(model.JsonLd, json => json.Contains("BlogPosting", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildForContent_CustomCanonicalOverrides()
    {
        var config = CreateConfig();
        var item = new ContentItem(
            Id: "p1",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["canonical"] = "https://other.com/custom"
            }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("https://other.com/custom", model.Canonical);
    }
}
