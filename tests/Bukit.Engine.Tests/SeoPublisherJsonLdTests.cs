using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoPublisherJsonLdTests
{
    [Theory]
    [InlineData("BlogPosting")]
    [InlineData("Article")]
    public void BuildForContent_ArticlePublisherMatchesNormalizedSiteOrganization(string schemaType)
    {
        var organization = new SeoOrganizationConfig
        {
            Type = "NewsMediaOrganization",
            Name = "丝路商讯",
            Url = "/about/",
            Logo = "/assets/images/social-default.png",
            SameAs =
            [
                "https://www.linkedin.com/company/silushangxun/",
                "https://www.youtube.com/@silushangxun"
            ]
        };
        var model = BuildForContent(CreateConfig("https://silushangxun.com", organization), schemaType);
        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        using var cleanup = new JsonDocumentsCleanup(documents);

        var siteOrganization = documents.Single(document =>
            document.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "NewsMediaOrganization").RootElement;
        var article = documents.Single(document =>
            document.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == schemaType).RootElement;
        var publisher = article.GetProperty("publisher");

        Assert.Equal(siteOrganization.GetProperty("@type").GetString(), publisher.GetProperty("@type").GetString());
        Assert.Equal(siteOrganization.GetProperty("name").GetString(), publisher.GetProperty("name").GetString());
        Assert.Equal("https://silushangxun.com/about/", publisher.GetProperty("url").GetString());
        Assert.Equal("https://silushangxun.com/assets/images/social-default.png", publisher.GetProperty("logo").GetString());
        Assert.Equal(
            siteOrganization.GetProperty("sameAs").EnumerateArray().Select(static value => value.GetString()),
            publisher.GetProperty("sameAs").EnumerateArray().Select(static value => value.GetString()));
    }

    [Fact]
    public void BuildForContent_EmptySameAs_IsOmittedFromOrganizationAndPublisher()
    {
        var organization = new SeoOrganizationConfig
        {
            Name = "Legacy Publisher",
            Url = "https://example.com/about/",
            Logo = "https://example.com/logo.png",
            SameAs = []
        };
        var model = BuildForContent(CreateConfig("https://example.com", organization), "BlogPosting");
        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        using var cleanup = new JsonDocumentsCleanup(documents);

        var siteOrganization = documents.Single(document =>
            document.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "Organization").RootElement;
        var publisher = documents.Single(document =>
                document.RootElement.TryGetProperty("@type", out var type) &&
                type.GetString() == "BlogPosting")
            .RootElement
            .GetProperty("publisher");

        Assert.False(siteOrganization.TryGetProperty("sameAs", out _));
        Assert.False(publisher.TryGetProperty("sameAs", out _));
    }

    [Fact]
    public void BuildForContent_RelativeOrganizationUrlsWithoutSiteUrl_AreOmitted()
    {
        var organization = new SeoOrganizationConfig
        {
            Name = "Publisher Without Origin",
            Url = "/about/",
            Logo = "/logo.png"
        };
        var model = BuildForContent(CreateConfig(null, organization), "BlogPosting");
        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        using var cleanup = new JsonDocumentsCleanup(documents);

        var siteOrganization = documents.Single(document =>
            document.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "Organization").RootElement;
        var publisher = documents.Single(document =>
                document.RootElement.TryGetProperty("@type", out var type) &&
                type.GetString() == "BlogPosting")
            .RootElement
            .GetProperty("publisher");

        Assert.False(siteOrganization.TryGetProperty("url", out _));
        Assert.False(siteOrganization.TryGetProperty("logo", out _));
        Assert.False(publisher.TryGetProperty("url", out _));
        Assert.False(publisher.TryGetProperty("logo", out _));
    }

    [Fact]
    public void BuildForContent_NonHttpOrganizationUrls_AreNotRewrittenOrEmitted()
    {
        var organization = new SeoOrganizationConfig
        {
            Name = "Publisher With Invalid Urls",
            Url = "mailto:publisher@example.com",
            Logo = "ftp://example.com/logo.png"
        };
        var model = BuildForContent(CreateConfig("https://example.com", organization), "BlogPosting");
        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        using var cleanup = new JsonDocumentsCleanup(documents);

        var siteOrganization = documents.Single(document =>
            document.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "Organization").RootElement;
        var publisher = documents.Single(document =>
                document.RootElement.TryGetProperty("@type", out var type) &&
                type.GetString() == "BlogPosting")
            .RootElement
            .GetProperty("publisher");

        Assert.False(siteOrganization.TryGetProperty("url", out _));
        Assert.False(siteOrganization.TryGetProperty("logo", out _));
        Assert.False(publisher.TryGetProperty("url", out _));
        Assert.False(publisher.TryGetProperty("logo", out _));
    }

    [Fact]
    public void BuildForContent_NonArticleSchema_DoesNotEmitPublisher()
    {
        var organization = new SeoOrganizationConfig
        {
            Name = "Example Publisher",
            Url = "https://example.com/about/"
        };
        var model = BuildForContent(CreateConfig("https://example.com", organization), "FAQPage");
        var documents = model.JsonLd.Select(static json => JsonDocument.Parse(json)).ToArray();
        using var cleanup = new JsonDocumentsCleanup(documents);

        var faqPage = documents.Single(document =>
            document.RootElement.TryGetProperty("@type", out var type) &&
            type.GetString() == "FAQPage");

        Assert.False(faqPage.RootElement.TryGetProperty("publisher", out _));
    }

    private static SeoModel BuildForContent(AppConfig config, string schemaType)
    {
        var document = ContentDocument.Create(
            id: $"publisher-{schemaType}",
            title: "Publisher contract",
            slug: $"publisher-{schemaType.ToLowerInvariant()}",
            publishAt: new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>Publisher contract</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["schema_type"] = schemaType
            }));
        var route = new RouteInfo(
            $"/publisher-{schemaType.ToLowerInvariant()}/",
            $"publisher-{schemaType.ToLowerInvariant()}/index.html",
            "pages/post.html");

        return SeoModelBuilder.BuildForContent(config, "/", document, route);
    }

    private static AppConfig CreateConfig(string? siteUrl, SeoOrganizationConfig organization)
        => new()
        {
            Site = new SiteConfig
            {
                Name = "silk-road-news",
                Title = "丝路商讯",
                Url = siteUrl,
                Seo = new SeoConfig { Organization = organization }
            },
            Content = TestContent.Markdown()
        };

    private sealed class JsonDocumentsCleanup(JsonDocument[] documents) : IDisposable
    {
        public void Dispose()
        {
            foreach (var document in documents)
            {
                document.Dispose();
            }
        }
    }
}
