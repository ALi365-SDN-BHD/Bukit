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
            Content = TestContent.Markdown()
        };
    }

    [Fact]
    public void BuildForContent_WithFullItem_SetsAllProperties()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "post-1",
            title: "Hello World",
            slug: "hello-world",
            publishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>hello</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
        Assert.Equal("Hello World", model.DocumentTitle);
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
        Assert.Equal("Person", model.Article.AuthorType);
        Assert.Equal(new[] { "dotnet", "aspire" }, model.Article.Tags);
        Assert.NotNull(model.JsonLd);
        Assert.NotEmpty(model.JsonLd);
    }

    [Fact]
    public void BuildForContent_SeoTitleOverridesTitle()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "p1",
            title: "Original",
            slug: "original",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["seo_title"] = "SEO Title"
            }));
        var route = new RouteInfo("/pages/original/", "pages/original/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("SEO Title", model.Title);
        Assert.Equal("SEO Title", model.DocumentTitle);
        Assert.Equal("SEO Title", model.Og.Title);
    }

    [Fact]
    public void BuildForContent_CustomPageTitleTemplate_SeparatesDocumentAndSemanticTitles()
    {
        var config = CreateConfig();
        config = config with
        {
            Site = config.Site with
            {
                Seo = config.Site.Seo with
                {
                    PageTitleTemplate = " {PAGETITLE}{separator}{siteTitle} ",
                    TitleSeparator = " | "
                }
            }
        };
        var item = ContentDocument.Create(
            id: "p1",
            title: "Original",
            slug: "original",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["seo_title"] = "SEO   Title"
            }));
        var route = new RouteInfo("/pages/original/", "pages/original/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(
            config,
            "/",
            item,
            route,
            breadcrumb: new BreadcrumbDescriptor(
                [new BreadcrumbItemDescriptor("Original", "https://example.com/pages/original/")]));

        Assert.Equal("SEO   Title", model.Title);
        Assert.Equal("SEO Title | My Site", model.DocumentTitle);
        Assert.Equal("SEO   Title", model.Og.Title);
        Assert.All(model.JsonLd, json => Assert.DoesNotContain("SEO Title | My Site", json, StringComparison.Ordinal));

        using var webPage = model.JsonLd
            .Select(static json => JsonDocument.Parse(json))
            .Single(doc => doc.RootElement.GetProperty("@type").GetString() == "WebPage");
        Assert.Equal("Original", webPage.RootElement.GetProperty("name").GetString());

        using var breadcrumb = model.JsonLd
            .Select(static json => JsonDocument.Parse(json))
            .Single(doc => doc.RootElement.GetProperty("@type").GetString() == "BreadcrumbList");
        var items = breadcrumb.RootElement.GetProperty("itemListElement");
        Assert.Equal("Original", items[items.GetArrayLength() - 1].GetProperty("name").GetString());
    }

    [Fact]
    public void ResolveDocumentTitle_DoesNotInterpretPlaceholderTextInsideReplacementValues()
    {
        var seo = new SeoConfig
        {
            PageTitleTemplate = "{pageTitle}{separator}{siteTitle}",
            TitleSeparator = " | {pageTitle}"
        };

        var title = SeoDocumentTitleResolver.Resolve(
            seo,
            siteTitle: "Site {separator}",
            pageTitle: "Guide to {siteTitle} and {separator}",
            routeUrl: "/guide/");

        Assert.Equal(
            "Guide to {siteTitle} and {separator} | {pageTitle}Site {separator}",
            title);
    }

    [Fact]
    public void BuildForContent_HomeRoute_UsesHomeTitleTemplate()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "home",
            title: "Welcome",
            slug: "home",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
        var route = new RouteInfo("/", "index.html", "pages/index.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("Welcome", model.Title);
        Assert.Equal("My Site", model.DocumentTitle);
    }

    [Fact]
    public void BuildForContent_SeoDescriptionOverridesSummary()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "p1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
        var item = ContentDocument.Create(
            id: "p1",
            title: "Canonical",
            slug: "canonical",
            publishAt: DateTimeOffset.Parse("2026-06-05T10:00:00Z"),
            contentHtml: null,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
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
        Assert.Equal("Person", model.Article.AuthorType);
        Assert.Equal(new[] { "canonical-tag", "canonical-second" }, model.Article.Tags);

        var articleJson = model.JsonLd
            .Select(static json => JsonDocument.Parse(json))
            .First(doc => doc.RootElement.TryGetProperty("@type", out var type) &&
                          type.GetString() == "BlogPosting");
        Assert.Equal("ms-MY", articleJson.RootElement.GetProperty("inLanguage").GetString());
        Assert.Equal("canonical-tag", articleJson.RootElement.GetProperty("keywords")[0].GetString());
    }

    [Fact]
    public void BuildForContent_OrganizationAuthor_UsesCanonicalTypeWithoutStandaloneOrganization()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "organization-author",
            title: "Collective byline",
            slug: "collective-byline",
            publishAt: DateTimeOffset.Parse("2026-07-13T10:00:00Z"),
            contentHtml: "<p>Collective byline</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["author"] = "丝路商讯编辑部",
                ["authorType"] = "organization"
            }));
        var route = new RouteInfo("/collective-byline/", "collective-byline/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("丝路商讯编辑部", model.Article.Author);
        Assert.Equal("Organization", model.Article.AuthorType);
        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        var article = documents.Single(doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "BlogPosting");
        var author = article.RootElement.GetProperty("author");
        Assert.Equal("Organization", author.GetProperty("@type").GetString());
        Assert.Equal("丝路商讯编辑部", author.GetProperty("name").GetString());
        Assert.DoesNotContain(documents, doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "Organization");
    }

    [Fact]
    public void BuildForContent_InvalidExplicitAuthorType_OmitsStructuredAuthor()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "invalid-author-type",
            title: "Invalid author type",
            slug: "invalid-author-type",
            publishAt: DateTimeOffset.Parse("2026-07-13T10:00:00Z"),
            contentHtml: "<p>Invalid author type</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["author"] = "Editorial Desk",
                ["authorType"] = "Company"
            }));
        var route = new RouteInfo("/invalid-author-type/", "invalid-author-type/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("Editorial Desk", model.Article.Author);
        Assert.Null(model.Article.AuthorType);
        var article = model.JsonLd
            .Select(static json => JsonDocument.Parse(json))
            .Single(doc => doc.RootElement.TryGetProperty("@type", out var type) &&
                           type.GetString() == "BlogPosting");
        Assert.False(article.RootElement.TryGetProperty("author", out _));
    }

    [Fact]
    public void BuildForContent_LegacySeoFieldsFallbackToStandardSeo()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "p1",
            title: "Original",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
        var item = ContentDocument.Create(
            id: "p1",
            title: "About",
            slug: "about",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
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
        var item = ContentDocument.Create(
            id: "p1",
            title: "Noindex",
            slug: "noindex",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
        var item = ContentDocument.Create(
            id: "p1",
            title: "Translated",
            slug: "translated",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
        Assert.Equal("Blog", model.DocumentTitle);
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
        var item = ContentDocument.Create(
            id: "p1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["i18nKey"] = "page.about" }));
        var route = new RouteInfo("/pages/about/", "pages/about/index.html", "pages/page.html");

        var key = SeoModelBuilder.BuildAlternateKey(item, route);

        Assert.Equal("i18n:page.about", key);
    }

    [Fact]
    public void BuildAlternateKey_WithoutI18nKey_UsesRoute()
    {
        var item = ContentDocument.Create(
            id: "p1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));
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
        var item = ContentDocument.Create(
            id: "p1",
            title: "No Image",
            slug: "no-image",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
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
        var item = ContentDocument.Create(
            id: "p1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
        var item = ContentDocument.Create(
            id: "p1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["cover"] = "/covers/main.jpg"
            }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("https://example.com/covers/main.jpg", model.Og.Image);
    }

    [Fact]
    public void BuildForContent_JsonLdContainsWebSiteWithoutUndeclaredSearchAction()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "p1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains("WebSite", model.JsonLd[0]);
        Assert.DoesNotContain("SearchAction", model.JsonLd[0]);
    }

    [Fact]
    public void BuildForContent_DeclaredSearchAction_UsesResolvedDescriptor()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "p1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");
        var searchAction = new SearchActionDescriptor(
            "https://example.com/search/?q={search_term_string}",
            "required name=search_term_string");

        var model = SeoModelBuilder.BuildForContent(
            config,
            "/",
            item,
            route,
            searchAction: searchAction);

        Assert.Contains("\"@type\":\"SearchAction\"", model.JsonLd[0], StringComparison.Ordinal);
        Assert.Contains("https://example.com/search/?q={search_term_string}", model.JsonLd[0], StringComparison.Ordinal);
    }

    [Fact]
    public void BuildForContent_JsonLdContainsWebPage()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "p1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));
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
        var item = ContentDocument.Create(
            id: "post-1",
            title: "My Post",
            slug: "my-post",
            publishAt: new DateTimeOffset(2025, 3, 10, 8, 0, 0, TimeSpan.Zero),
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["schema_type"] = "BlogPosting",
                ["seo_title"] = "Search Result Title",
                ["author"] = "Bob",
                ["tags"] = "tech,code"
            }));
        var route = new RouteInfo("/blog/my-post/", "blog/my-post/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        using var blogPosting = model.JsonLd
            .Select(static json => JsonDocument.Parse(json))
            .Single(doc => doc.RootElement.GetProperty("@type").GetString() == "BlogPosting");
        Assert.Equal("My Post", blogPosting.RootElement.GetProperty("headline").GetString());
        Assert.Equal("Bob", blogPosting.RootElement.GetProperty("author").GetProperty("name").GetString());
        Assert.Equal("tech", blogPosting.RootElement.GetProperty("keywords")[0].GetString());
        Assert.Equal("code", blogPosting.RootElement.GetProperty("keywords")[1].GetString());
    }

    [Fact]
    public void BuildForContent_PostTypeWithNewsCollectionEmitsBlogPostingJsonLd()
    {
        var item = ContentDocument.Create(
            "news-post",
            "News Post",
            "news-post",
            DateTimeOffset.UnixEpoch,
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "news"
            }));
        var route = new RouteInfo("/news/news-post/", "news/news-post/index.html", "news.html");

        var model = SeoModelBuilder.BuildForContent(CreateConfig(), "/", item, route);

        Assert.Equal("BlogPosting", model.SchemaType);
        Assert.Contains(model.JsonLd, json => json.Contains("BlogPosting", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildForContent_ArticleTypeWithNewsCollectionEmitsBlogPostingJsonLd()
    {
        var item = ContentDocument.Create(
            "news-article",
            "News Article",
            "news-article",
            DateTimeOffset.UnixEpoch,
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "article",
                ["collection"] = "news"
            }));
        var route = new RouteInfo("/news/news-article/", "news/news-article/index.html", "news.html");

        var model = SeoModelBuilder.BuildForContent(CreateConfig(), "/", item, route);

        Assert.Equal("BlogPosting", model.SchemaType);
        Assert.Equal("article", model.Og.Type);
        Assert.Contains(model.JsonLd, json => json.Contains("BlogPosting", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildForContent_PageTypeWithPostCollectionDoesNotEmitBlogPostingJsonLd()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "blog-archive-2026",
            title: "Archive: 2026",
            slug: "archive-2026",
            publishAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
        var item = ContentDocument.Create(
            id: "p1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["canonical"] = "https://other.com/custom"
            }));
        var route = new RouteInfo("/pages/test/", "pages/test/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Equal("https://other.com/custom", model.Canonical);
    }
}
