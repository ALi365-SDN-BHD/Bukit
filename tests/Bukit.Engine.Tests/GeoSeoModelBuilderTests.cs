using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
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
            Content = TestContent.Markdown()
        };
    }

    [Fact]
    public void BuildForContent_WithGeoFaq_GeneratesFaqPageJsonLd()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "faq-1",
            title: "FAQ Guide",
            slug: "faq-guide",
            publishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>faq content</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["seo_title"] = "FAQ Search Title",
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
            }));
        var route = new RouteInfo("/faq-guide/", "faq-guide/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.NotEmpty(model.JsonLd);
        Assert.Contains(model.JsonLd, j => j.Contains("FAQPage", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("What is Bukit?", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("acceptedAnswer", StringComparison.Ordinal));
        using var faqPage = model.JsonLd
            .Select(static json => JsonDocument.Parse(json))
            .Single(doc => doc.RootElement.GetProperty("@type").GetString() == "FAQPage");
        Assert.Equal("FAQ Guide", faqPage.RootElement.GetProperty("headline").GetString());
        Assert.NotNull(model.FaqItems);
        Assert.Equal(2, model.FaqItems.Count);
    }

    [Fact]
    public void BuildForContent_WithGeoHowTo_GeneratesHowToJsonLd()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "howto-1",
            title: "How to Setup",
            slug: "how-to-setup",
            publishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>guide</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["seo_title"] = "HowTo Search Title",
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
            }));
        var route = new RouteInfo("/how-to-setup/", "how-to-setup/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("HowTo", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("HowToStep", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("position", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("https://example.com/img1.png", StringComparison.Ordinal));
        using var howTo = model.JsonLd
            .Select(static json => JsonDocument.Parse(json))
            .Single(doc => doc.RootElement.GetProperty("@type").GetString() == "HowTo");
        Assert.Equal("How to Setup", howTo.RootElement.GetProperty("name").GetString());
        Assert.NotNull(model.HowToSteps);
        Assert.Equal(2, model.HowToSteps.Count);
    }

    [Fact]
    public void BuildForContent_WithGeoAuthor_GeneratesPersonJsonLd()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "post-1",
            title: "Post with Author",
            slug: "post-author",
            publishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>post</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
            }));
        var route = new RouteInfo("/post-author/", "post-author/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        var article = documents.Single(doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "BlogPosting");
        Assert.Equal("Person", article.RootElement.GetProperty("author").GetProperty("@type").GetString());
        Assert.Contains(documents, doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "Person" &&
            doc.RootElement.GetProperty("name").GetString() == "Alice");
        Assert.Contains(model.JsonLd, j => j.Contains("https://github.com/alice", StringComparison.Ordinal));
        Assert.NotNull(model.GeoAuthor);
        Assert.Equal("Alice", model.GeoAuthor.Name);
        Assert.Equal("Alice", model.Article.Author);
        Assert.Equal("Person", model.Article.AuthorType);
    }

    [Fact]
    public void BuildForContent_CanonicalOrganizationAuthor_MergesMatchingGeoEnrichment()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "organization-author",
            title: "Organization author",
            slug: "organization-author",
            publishAt: new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>post</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["author"] = "丝路商讯编辑部",
                ["authorType"] = "Organization",
                ["geo"] = new Dictionary<string, object>
                {
                    ["author"] = new Dictionary<string, object>
                    {
                        ["name"] = "丝路商讯编辑部",
                        ["url"] = "https://example.com/editorial/",
                        ["same_as"] = new List<object> { "https://example.com/about/" }
                    }
                }
            }));
        var route = new RouteInfo("/organization-author/", "organization-author/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        var article = documents.Single(doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "BlogPosting");
        var author = article.RootElement.GetProperty("author");
        Assert.Equal("Organization", author.GetProperty("@type").GetString());
        Assert.Equal("https://example.com/editorial/", author.GetProperty("url").GetString());
        Assert.Equal("https://example.com/about/", author.GetProperty("sameAs")[0].GetString());
        Assert.DoesNotContain(documents, doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "Person" &&
            doc.RootElement.GetProperty("name").GetString() == "丝路商讯编辑部");
    }

    [Fact]
    public void BuildForContent_InvalidCanonicalAuthorType_DoesNotLeakMatchingGeoPerson()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "invalid-organization-author",
            title: "Invalid organization author",
            slug: "invalid-organization-author",
            publishAt: new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>post</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["author"] = "Editorial Desk",
                ["authorType"] = "Company",
                ["geo"] = new Dictionary<string, object>
                {
                    ["author"] = new Dictionary<string, object>
                    {
                        ["name"] = "Editorial Desk",
                        ["url"] = "https://example.com/editorial/"
                    }
                }
            }));
        var route = new RouteInfo(
            "/invalid-organization-author/",
            "invalid-organization-author/index.html",
            "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        var article = documents.Single(doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "BlogPosting");
        Assert.False(article.RootElement.TryGetProperty("author", out _));
        Assert.DoesNotContain(documents, doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "Person" &&
            doc.RootElement.GetProperty("name").GetString() == "Editorial Desk");
    }

    [Fact]
    public void BuildForContent_CanonicalAuthor_DoesNotAdoptConflictingGeoIdentity()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "conflicting-author",
            title: "Conflicting author",
            slug: "conflicting-author",
            publishAt: new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>post</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["author"] = "丝路商讯编辑部",
                ["authorType"] = "Organization",
                ["geo"] = new Dictionary<string, object>
                {
                    ["author"] = new Dictionary<string, object>
                    {
                        ["name"] = "Alice",
                        ["url"] = "https://alice.dev"
                    }
                }
            }));
        var route = new RouteInfo("/conflicting-author/", "conflicting-author/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        var article = documents.Single(doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "BlogPosting");
        var author = article.RootElement.GetProperty("author");
        Assert.Equal("Organization", author.GetProperty("@type").GetString());
        Assert.Equal("丝路商讯编辑部", author.GetProperty("name").GetString());
        Assert.False(author.TryGetProperty("url", out _));
        Assert.Contains(documents, doc =>
            doc.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "Person" &&
            doc.RootElement.GetProperty("name").GetString() == "Alice");
    }

    [Fact]
    public void BuildForContent_WithGeoAuthorOnNonPost_GeneratesPersonJsonLd()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "page-1",
            title: "About Us",
            slug: "about-us",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>about</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
            }));
        var route = new RouteInfo("/about-us/", "about-us/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("Person", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("Bob", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildForContent_WithGeoCitations_GeneratesCitationsJsonLd()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "page-1",
            title: "Research",
            slug: "research",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>research</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
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
                        },
                    }
                }
            }));
        var route = new RouteInfo("/research/", "research/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("\"mentions\"", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("Schema.org Docs", StringComparison.Ordinal));
        Assert.NotNull(model.Citations);
        Assert.Single(model.Citations);
    }

    [Fact]
    public void BuildForContent_WithGeoCitationRelation_ParsesRelationAndDefaultsToCitation()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "page-2",
            title: "Provenance",
            slug: "provenance",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>provenance</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["geo"] = new Dictionary<string, object>
                {
                    ["citations"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["title"] = "Primary report",
                            ["url"] = "https://source.example/report",
                            ["relation"] = "based-on"
                        },
                        new Dictionary<string, object>
                        {
                            ["title"] = "Supporting doc",
                            ["url"] = "https://source.example/support"
                        },
                    }
                }
            }));
        var route = new RouteInfo("/provenance/", "provenance/index.html", "pages/page.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.NotNull(model.Citations);
        Assert.Equal(2, model.Citations.Count);
        Assert.Equal("based-on", model.Citations[0].Relation);
        Assert.Equal("citation", model.Citations[1].Relation);
    }

    [Theory]
    [InlineData("Article")]
    [InlineData("BlogPosting")]
    [InlineData("NewsArticle")]
    public void BuildForContent_ArticleFamily_EmitsMainEntityOfPageAndProvenance(string schemaType)
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: $"trust-{schemaType.ToLowerInvariant()}",
            title: "Trust Graph",
            slug: $"trust-{schemaType.ToLowerInvariant()}",
            publishAt: new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>trust graph</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["schema_type"] = schemaType,
                ["geo"] = new Dictionary<string, object>
                {
                    ["citations"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["title"] = "Supporting doc",
                            ["url"] = "https://source.example/support"
                        },
                        new Dictionary<string, object>
                        {
                            ["title"] = "Primary report",
                            ["url"] = "https://source.example/report",
                            ["relation"] = "based-on"
                        },
                    }
                }
            }));
        var route = new RouteInfo("/news/item/", "news/item/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        var articleJson = model.JsonLd.Single(j => j.Contains($"\"@type\":\"{schemaType}\"", StringComparison.Ordinal));
        using var articleDoc = JsonDocument.Parse(articleJson);
        var article = articleDoc.RootElement;

        var mainEntity = article.GetProperty("mainEntityOfPage");
        Assert.Equal("WebPage", mainEntity.GetProperty("@type").GetString());
        Assert.Equal(model.Canonical, mainEntity.GetProperty("@id").GetString());

        var citations = article.GetProperty("citation").EnumerateArray().ToArray();
        Assert.Equal(2, citations.Length);
        Assert.Contains(citations, c => c.GetProperty("name").GetString() == "Supporting doc");
        Assert.Contains(citations, c => c.GetProperty("name").GetString() == "Primary report");

        var basedOn = article.GetProperty("isBasedOn").EnumerateArray().ToArray();
        var single = Assert.Single(basedOn);
        Assert.Equal("Primary report", single.GetProperty("name").GetString());

        var mentionsJson = model.JsonLd.Single(j => j.Contains("\"mentions\"", StringComparison.Ordinal));
        using var mentionsDoc = JsonDocument.Parse(mentionsJson);
        var mentions = mentionsDoc.RootElement.GetProperty("mentions").EnumerateArray().ToArray();
        Assert.Equal(2, mentions.Length);
        Assert.Contains(mentions, m => m.GetProperty("name").GetString() == "Supporting doc");
        Assert.Contains(mentions, m => m.GetProperty("name").GetString() == "Primary report");
    }

    [Theory]
    [InlineData("Article")]
    [InlineData("BlogPosting")]
    [InlineData("NewsArticle")]
    public void BuildForContent_ArticleFamilyWithoutCitations_StillEmitsMainEntityOfPage(string schemaType)
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: $"trust-empty-{schemaType.ToLowerInvariant()}",
            title: "Trust Graph Empty",
            slug: $"trust-empty-{schemaType.ToLowerInvariant()}",
            publishAt: new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>trust graph</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["schema_type"] = schemaType
            }));
        var route = new RouteInfo("/news/empty/", "news/empty/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        var articleJson = model.JsonLd.Single(j => j.Contains($"\"@type\":\"{schemaType}\"", StringComparison.Ordinal));
        using var articleDoc = JsonDocument.Parse(articleJson);
        var article = articleDoc.RootElement;

        Assert.Equal(model.Canonical, article.GetProperty("mainEntityOfPage").GetProperty("@id").GetString());
        Assert.False(article.TryGetProperty("citation", out _));
        Assert.False(article.TryGetProperty("isBasedOn", out _));
    }

    [Fact]
    public void BuildForContent_WithGeoSameAs_AddsToArticleJsonLd()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "post-1",
            title: "SameAs Post",
            slug: "sameas-post",
            publishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>post</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["geo"] = new Dictionary<string, object>
                {
                    ["schema_type"] = "BlogPosting",
                    ["same_as"] = new List<object> { "https://github.com/repo" }
                }
            }));
        var route = new RouteInfo("/sameas-post/", "sameas-post/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("sameAs", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("https://github.com/repo", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildForContent_WithGeoSpeakable_GeneratesSpeakableJsonLd()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "post-1",
            title: "Speakable Post",
            slug: "speakable-post",
            publishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>post</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["geo"] = new Dictionary<string, object>
                {
                    ["speakable_xpath"] = "/html/body/article"
                }
            }));
        var route = new RouteInfo("/speakable-post/", "speakable-post/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("SpeakableSpecification", StringComparison.Ordinal));
        Assert.Equal("/html/body/article", model.SpeakableXPath);
    }

    [Fact]
    public void BuildForContent_WithGeoAboutAndDateReviewed_AddsToArticleJsonLd()
    {
        var config = CreateGeoConfig();
        var item = ContentDocument.Create(
            id: "post-1",
            title: "Reviewed Post",
            slug: "reviewed-post",
            publishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>post</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["geo"] = new Dictionary<string, object>
                {
                    ["schema_type"] = "BlogPosting",
                    ["about"] = "Static site generators",
                    ["date_reviewed"] = "2026-03-01T00:00:00+00:00"
                }
            }));
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
        var item = ContentDocument.Create(
            id: "post-1",
            title: "Language Post",
            slug: "lang-post",
            publishAt: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>post</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["schema_type"] = "BlogPosting"
            }));
        var route = new RouteInfo("/lang-post/", "lang-post/index.html", "pages/post.html");

        var model = SeoModelBuilder.BuildForContent(config, "/", item, route);

        Assert.Contains(model.JsonLd, j => j.Contains("inLanguage", StringComparison.Ordinal));
        Assert.Contains(model.JsonLd, j => j.Contains("zh-CN", StringComparison.Ordinal));
    }
}
