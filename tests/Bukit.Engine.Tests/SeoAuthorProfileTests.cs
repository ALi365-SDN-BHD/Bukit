using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoAuthorProfileTests
{
    [Fact]
    public void BuildForContent_ResolvedPersonRelation_OverridesLegacyAndUsesCanonicalProfileUrl()
    {
        var document = CreateDocument(
            ("author", new ContentField("text", "Legacy Author")),
            ("authoredby", Profiles(Profile(
                id: "author-1",
                title: "Aisha Tan",
                slug: "aisha-tan",
                type: "Person",
                url: "https://untrusted.example/arbitrary",
                image: "/images/aisha.jpg",
                sameAs: ["https://social.example/aisha"]))));

        var model = Build(document);

        using var article = Article(model.JsonLd);
        var author = article.RootElement.GetProperty("author");
        Assert.Equal("Person", author.GetProperty("@type").GetString());
        Assert.Equal("Aisha Tan", author.GetProperty("name").GetString());
        Assert.Equal("https://example.com/authors/aisha-tan/", author.GetProperty("url").GetString());
        Assert.Equal("https://example.com/images/aisha.jpg", author.GetProperty("image").GetString());
        Assert.Equal("https://social.example/aisha", author.GetProperty("sameAs")[0].GetString());
        Assert.DoesNotContain("untrusted.example", author.GetRawText(), StringComparison.Ordinal);
        Assert.Equal("Aisha Tan", model.Article.Author);
        Assert.Equal("Person", model.Article.AuthorType);
    }

    [Fact]
    public void BuildForContent_ResolvedEditorialRelation_EmitsOrganizationProfile()
    {
        var document = CreateDocument(
            ("authoredBy", Profiles(Profile(
                id: "editorial",
                title: "丝路商讯编辑部",
                slug: "editorial",
                type: "Organization",
                image: "https://cdn.example/editorial.png",
                sameAs: ["https://social.example/editorial"]))));

        var model = Build(document);

        using var article = Article(model.JsonLd);
        var author = article.RootElement.GetProperty("author");
        Assert.Equal("Organization", author.GetProperty("@type").GetString());
        Assert.Equal("丝路商讯编辑部", author.GetProperty("name").GetString());
        Assert.Equal("https://example.com/authors/editorial/", author.GetProperty("url").GetString());
        Assert.Equal("https://cdn.example/editorial.png", author.GetProperty("image").GetString());
        Assert.Equal("https://social.example/editorial", author.GetProperty("sameAs")[0].GetString());
    }

    [Fact]
    public void BuildForContent_MultipleResolvedRelations_EmitsStableAuthorArrayAndKeepsPrimaryAuthor()
    {
        var document = CreateDocument(
            ("authoredby", Profiles(
                Profile("author-2", "Second Author", "second-author", "Person"),
                Profile("editorial", "丝路商讯编辑部", "editorial", "Organization"))));

        var model = Build(document);

        using var article = Article(model.JsonLd);
        var authors = article.RootElement.GetProperty("author");
        Assert.Equal(JsonValueKind.Array, authors.ValueKind);
        Assert.Equal(2, authors.GetArrayLength());
        Assert.Equal("Second Author", authors[0].GetProperty("name").GetString());
        Assert.Equal("Person", authors[0].GetProperty("@type").GetString());
        Assert.Equal("丝路商讯编辑部", authors[1].GetProperty("name").GetString());
        Assert.Equal("Organization", authors[1].GetProperty("@type").GetString());
        Assert.Equal("Second Author", model.Article.Author);
        Assert.Equal("Person", model.Article.AuthorType);
    }

    [Fact]
    public void BuildForContent_RelationProfileWinsOverConflictingGeoAuthorWithoutLeakingGeoIdentity()
    {
        var document = CreateDocument(
            ("authoredby", Profiles(Profile("author-1", "Aisha Tan", "aisha-tan", "Person"))),
            ("geo", new ContentField("map", new Dictionary<string, object>
            {
                ["author"] = new Dictionary<string, object>
                {
                    ["name"] = "Conflicting Geo Author",
                    ["url"] = "https://geo.example/conflict",
                    ["same_as"] = new[] { "https://social.example/conflict" }
                }
            })));

        var model = Build(document);

        using var article = Article(model.JsonLd);
        Assert.Equal("Aisha Tan", article.RootElement.GetProperty("author").GetProperty("name").GetString());
        Assert.DoesNotContain(model.JsonLd, json => json.Contains("Conflicting Geo Author", StringComparison.Ordinal));
        Assert.DoesNotContain(model.JsonLd, json => json.Contains("geo.example", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildForContent_WithoutRelation_KeepsLegacyTextAndMatchingGeoFallback()
    {
        var document = CreateDocument(
            ("author", new ContentField("text", "Legacy Author")),
            ("geo", new ContentField("map", new Dictionary<string, object>
            {
                ["author"] = new Dictionary<string, object>
                {
                    ["name"] = "Legacy Author",
                    ["url"] = "https://legacy.example/profile",
                    ["same_as"] = new[] { "https://social.example/legacy" }
                }
            })));

        var model = Build(document);

        using var article = Article(model.JsonLd);
        var author = article.RootElement.GetProperty("author");
        Assert.Equal("Person", author.GetProperty("@type").GetString());
        Assert.Equal("Legacy Author", author.GetProperty("name").GetString());
        Assert.Equal("https://legacy.example/profile", author.GetProperty("url").GetString());
        Assert.Equal("https://social.example/legacy", author.GetProperty("sameAs")[0].GetString());
        Assert.False(author.TryGetProperty("image", out _));
    }

    [Theory]
    [InlineData(null, "resolved")]
    [InlineData("Company", "type")]
    public void BuildForContent_UnresolvedOrInvalidRelationTarget_BlocksStructuredOutput(
        string? targetType,
        string expectedMessage)
    {
        var target = Profile(
            id: "author-1",
            title: targetType is null ? null : "Invalid Author",
            slug: targetType is null ? null : "invalid-author",
            type: targetType);
        var document = CreateDocument(
            ("author", new ContentField("text", "Legacy Must Not Hide Invalid V2")),
            ("authoredby", Profiles(target)));

        var error = Assert.Throws<InvalidOperationException>(() => Build(document));

        Assert.Contains("authoredBy", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildForContent_AnyUnresolvedRelationTarget_BlocksInsteadOfDroppingAdditionalAuthor()
    {
        var document = CreateDocument(
            ("authoredby", Profiles(
                Profile("author-1", "Resolved Author", "resolved-author", "Person"),
                Profile("author-2", null, null, null))));

        var error = Assert.Throws<InvalidOperationException>(() => Build(document));

        Assert.Contains("author-2", error.Message, StringComparison.Ordinal);
        Assert.Contains("resolved", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildForContent_RelationTargetWithoutId_BlocksAsUnresolved()
    {
        var document = CreateDocument(
            ("authoredby", Profiles(Profile(
                id: "",
                title: "Nameless Identifier",
                slug: "nameless-identifier",
                type: "Person"))));

        var error = Assert.Throws<InvalidOperationException>(() => Build(document));

        Assert.Contains("resolved", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Bukit.Rendering.SeoModel Build(ContentDocument document)
        => SeoModelBuilder.BuildForContent(
            CreateConfig(),
            "/",
            document,
            new RouteInfo("/posts/article/", "posts/article/index.html", "pages/post.html"));

    private static ContentDocument CreateDocument(params (string Key, ContentField Field)[] fields)
    {
        var map = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "post"),
            ["summary"] = new("text", "Article summary")
        };
        foreach (var (key, field) in fields)
        {
            map[key] = field;
        }

        return ContentDocument.Create(
            id: "article-1",
            title: "Profile article",
            slug: "profile-article",
            publishAt: DateTimeOffset.Parse("2026-07-25T00:00:00Z"),
            contentHtml: "<p>Profile article</p>",
            fields: map);
    }

    private static ContentField Profiles(params Dictionary<string, object?>[] profiles)
        => new("list", profiles.ToList());

    private static Dictionary<string, object?> Profile(
        string id,
        string? title,
        string? slug,
        string? type,
        string? url = null,
        string? image = null,
        IReadOnlyList<string>? sameAs = null)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = id,
            ["title"] = title,
            ["slug"] = slug,
            ["type"] = type,
            ["url"] = url,
            ["image"] = image,
            ["sameAs"] = sameAs ?? Array.Empty<string>()
        };

    private static JsonDocument Article(IReadOnlyList<string> jsonLd)
        => jsonLd
            .Select(static json => JsonDocument.Parse(json))
            .Single(document =>
                document.RootElement.TryGetProperty("@type", out var type) &&
                type.GetString() == "BlogPosting");

    private static AppConfig CreateConfig()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "Example",
                Title = "Example",
                Url = "https://example.com",
                Language = "zh-CN",
                Seo = new SeoConfig
                {
                    Enabled = true,
                    Schema = new SeoSchemaConfig
                    {
                        WebPage = true
                    }
                }
            },
            Content = TestContent.Markdown()
        };
}
