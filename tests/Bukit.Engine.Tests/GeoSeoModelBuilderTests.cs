using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class GeoSeoModelBuilderTests
{
    private static AppConfig CreateGeoConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "GeoTest",
                Title = "Geo Test Site",
                Url = "https://example.com",
                Language = "zh-CN",
                Seo = new SeoConfig
                {
                    Enabled = true,
                    Schema = new SeoSchemaConfig
                    {
                        SearchAction = false,
                        WebPage = false,
                        CollectionPage = false
                    }
                }
            },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() }
        };
    }

    [Fact]
    public void BuildForContent_WithGeoFaq_GeneratesFaqPageJsonLd()
    {
        var config = CreateGeoConfig();
        var item = new ContentItem(
            Id: "faq-1",
            Title: "FAQ Guide",
            Slug: "faq-guide",
            PublishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>faq content</p>",
            Meta: new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["geo"] = new Dictionary<string, object>
                {
                    ["schema_type"] = "FAQPage",
                    ["faq"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["question"] = "What is Bukit?",
                            ["answer"] = "A static site generator."
                        },
                        new Dictionary<string, object>
                        {
                            ["question"] = "How to install?",
                            ["answer"] = "Run dotnet tool install."
                        }
                    }
                }
            });
        var route = new RouteInfo("/faq-guide/", "faq-guide/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.NotEmpty(model.JsonLd);
        Assert.Contains(model.JsonLd, j => j.Contains("FAQPage", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("What is Bukit?", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("acceptedAnswer", StringComparison.Ordinal));
        Assert.NotNull(model.FaqItems);
        Assert.Equal(2, model.FaqItems.Count);
    }

    [Fact]
    public void BuildForContent_WithGeoHowTo_GeneratesHowToJsonLd()
    {
        var config = CreateGeoConfig();
        var item = new ContentItem(
            Id: "howto-1",
            Title: "How to Setup",
            Slug: "how-to-setup",
            PublishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>guide</p>",
            Meta: new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["geo"] = new Dictionary<string, object>
                {
                    ["schema_type"] = "HowTo",
                    ["steps"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["name"] = "Install",
                            ["text"] = "Run the installer.",
                            ["image"] = "https://example.com/img1.png",
                            ["url"] = "https://example.com/install"
                        },
                        new Dictionary<string, object>
                        {
                            ["name"] = "Configure",
                            ["text"] = "Edit site.yaml."
                        }
                    }
                }
            });
        var route = new RouteInfo("/how-to-setup/", "how-to-setup/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("HowTo", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("HowToStep", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("position", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("https://example.com/img1.png", StringComparison.Ordinal));
        Assert.NotNull(model.HowToSteps);
        Assert.Equal(2, model.HowToSteps.Count);
    }

    [Fact]
    public void BuildForContent_WithGeoAuthor_GeneratesPersonJsonLd()
    {
        var config = CreateGeoConfig();
        var item = new ContentItem(
            Id: "post-1",
            Title: "Post with Author",
            Slug: "post-author",
            PublishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>post</p>",
            Meta: new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["geo"] = new Dictionary<string, object>
                {
                    ["author"] = new Dictionary<string, object>
                    {
                        ["name"] = "Alice",
                        ["url"] = "https://alice.dev",
                        ["same_as"] = new List<object> { "https://github.com/alice", "https://twitter.com/alice" }
                    }
                }
            });
        var route = new RouteInfo("/post-author/", "post-author/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("Person", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("Alice", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("https://github.com/alice", StringComparison.Ordinal));
        Assert.NotNull(model.GeoAuthor);
        Assert.Equal("Alice", model.GeoAuthor.Name);
    }

    [Fact]
    public void BuildForContent_WithGeoAuthorOnNonPost_GeneratesPersonJsonLd()
    {
        var config = CreateGeoConfig();
        var item = new ContentItem(
            Id: "page-1",
            Title: "About Us",
            Slug: "about-us",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>about</p>",
            Meta: new Dictionary<string, object>
            {
                ["type"] = "page",
                ["geo"] = new Dictionary<string, object>
                {
                    ["author"] = new Dictionary<string, object>
                    {
                        ["name"] = "Bob",
                        ["url"] = "https://bob.dev"
                    }
                }
            });
        var route = new RouteInfo("/about-us/", "about-us/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("Person", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("Bob", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildForContent_WithGeoCitations_GeneratesCitationsJsonLd()
    {
        var config = CreateGeoConfig();
        var item = new ContentItem(
            Id: "page-1",
            Title: "Research",
            Slug: "research",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>research</p>",
            Meta: new Dictionary<string, object>
            {
                ["type"] = "page",
                ["geo"] = new Dictionary<string, object>
                {
                    ["citations"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["title"] = "Schema.org Docs",
                            ["url"] = "https://schema.org/HowTo"
                        }
                    }
                }
            });
        var route = new RouteInfo("/research/", "research/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("\"mentions\"", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("Schema.org Docs", StringComparison.Ordinal));
        Assert.NotNull(model.Citations);
        Assert.Single(model.Citations);
    }

    [Fact]
    public void BuildForContent_WithGeoSameAs_AddsToArticleJsonLd()
    {
        var config = CreateGeoConfig();
        var item = new ContentItem(
            Id: "post-1",
            Title: "SameAs Post",
            Slug: "sameas-post",
            PublishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>post</p>",
            Meta: new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["geo"] = new Dictionary<string, object>
                {
                    ["same_as"] = new List<object> { "https://github.com/repo" }
                }
            });
        var route = new RouteInfo("/sameas-post/", "sameas-post/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("sameAs", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("https://github.com/repo", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildForContent_WithGeoSpeakable_GeneratesSpeakableJsonLd()
    {
        var config = CreateGeoConfig();
        var item = new ContentItem(
            Id: "post-1",
            Title: "Speakable Post",
            Slug: "speakable-post",
            PublishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>post</p>",
            Meta: new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["geo"] = new Dictionary<string, object>
                {
                    ["speakable_xpath"] = "/html/body/article"
                }
            });
        var route = new RouteInfo("/speakable-post/", "speakable-post/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("SpeakableSpecification", StringComparison.Ordinal));
        Assert.Equal("/html/body/article", model.SpeakableXPath);
    }

    [Fact]
    public void BuildForContent_WithGeoAboutAndDateReviewed_AddsToArticleJsonLd()
    {
        var config = CreateGeoConfig();
        var item = new ContentItem(
            Id: "post-1",
            Title: "Reviewed Post",
            Slug: "reviewed-post",
            PublishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>post</p>",
            Meta: new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["geo"] = new Dictionary<string, object>
                {
                    ["about"] = "Static site generators",
                    ["date_reviewed"] = "2026-03-01T00:00:00+00:00"
                }
            });
        var route = new RouteInfo("/reviewed-post/", "reviewed-post/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("Static site generators", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("2026-03-01", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("dateReviewed", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildForContent_ArticleJsonLd_IncludesInLanguage()
    {
        var config = CreateGeoConfig();
        var item = new ContentItem(
            Id: "post-1",
            Title: "Language Post",
            Slug: "lang-post",
            PublishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>post</p>",
            Meta: new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post"
            });
        var route = new RouteInfo("/lang-post/", "lang-post/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("inLanguage", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("zh-CN", StringComparison.Ordinal));
    }
}
