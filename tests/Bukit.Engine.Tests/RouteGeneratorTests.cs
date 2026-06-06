using Bukit.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RouteGeneratorTests
{
    private static ContentItem Item(
        string slug = "my-slug",
        IReadOnlyDictionary<string, object>? meta = null) =>
        new(
            Id: "id-1",
            Title: "Title",
            Slug: slug,
            PublishAt: DateTimeOffset.MinValue,
            ContentHtml: "",
            Meta: meta ?? new Dictionary<string, object>());

    private static ContentDocument Document(string id, string slug, string type, string collection)
    {
        var record = new ContentRecord(
            Identity: new ContentIdentity(id, slug, id, type, "published"),
            Presentation: new ContentPresentation("Hello", null, "<p>Hello</p>", "en", Array.Empty<string>()),
            Classification: new ContentClassification(type, collection, Array.Empty<string>(), Array.Empty<string>()),
            Ownership: new ContentOwnership(null, null, null, null),
            Lifecycle: new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
            Provenance: new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            Trust: new TrustMetadata(null, "unreviewed", Array.Empty<string>()),
            Entities: Array.Empty<EntityRecord>(),
            Relations: Array.Empty<ContentRelation>(),
            Media: Array.Empty<MediaAsset>());

        return new ContentDocument(
            record,
            new ContentBodyRef("<p>Hello</p>", null, null, null),
            new ContentRoutePolicy(null, null, null, null, collection),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<ContentDiagnostic>());
    }

    private static readonly IReadOnlyDictionary<string, RouteGenerator.CollectionRouteRule> DefaultCollections =
        new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new("/blog/{slug}/", string.Empty),
            ["page"] = new("/pages/{slug}/", string.Empty)
        };

    [Fact]
    public void Generate_PostType_ProducesBlogRouteAndPostTemplate()
    {
        var item = Item("my-post", new Dictionary<string, object> { ["type"] = "post", ["collection"] = "post" });
        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("/blog/my-post/", route.Url);
        Assert.Equal("blog/my-post/index.html", route.OutputPath);
        Assert.Equal(string.Empty, route.Template);
    }

    [Fact]
    public void Generate_PageType_ProducesPagesRouteAndPageTemplate()
    {
        var item = Item("about", new Dictionary<string, object> { ["type"] = "page", ["collection"] = "page" });
        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("/pages/about/", route.Url);
        Assert.Equal("pages/about/index.html", route.OutputPath);
        Assert.Equal(string.Empty, route.Template);
    }

    [Fact]
    public void Generate_FieldOnlyTypeAndCollection_ProducesCollectionRoute()
    {
        var item = new ContentItem(
            Id: "id-2",
            Title: "Title",
            Slug: "field-post",
            PublishAt: DateTimeOffset.MinValue,
            ContentHtml: "",
            Meta: new Dictionary<string, object>(),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["collection"] = new("text", "post")
            });

        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("/blog/field-post/", route.Url);
        Assert.Equal("blog/field-post/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_ContentDocument_UsesTypedRoutePolicyAndClassification()
    {
        var document = new ContentDocument(
            Record: new ContentRecord(
                Identity: new ContentIdentity("post-1", "hello", "post-1", "post", "published"),
                Presentation: new ContentPresentation("Hello", null, "<p>Hello</p>", "en", Array.Empty<string>()),
                Classification: new ContentClassification("post", "post", Array.Empty<string>(), Array.Empty<string>()),
                Ownership: new ContentOwnership(null, null, null, null),
                Lifecycle: new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
                Provenance: new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
                Trust: new TrustMetadata(null, "unreviewed", Array.Empty<string>()),
                Entities: Array.Empty<EntityRecord>(),
                Relations: Array.Empty<ContentRelation>(),
                Media: Array.Empty<MediaAsset>()),
            Body: new ContentBodyRef("<p>Hello</p>", null, null, null),
            Route: new ContentRoutePolicy(null, null, null, null, "post"),
            Publish: new ContentPublishPolicy(false, false, false, false, false, false, false),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Diagnostics: Array.Empty<ContentDiagnostic>());

        var route = RouteGenerator.Generate(document, collections: DefaultCollections);

        Assert.Equal("/blog/hello/", route.Url);
        Assert.Equal("blog/hello/index.html", route.OutputPath);
    }

    [Fact]
    public void GenerateWithSource_ContentDocument_ReportsCollectionSource()
    {
        var document = Document("post-1", "hello", "post", "post");

        var result = RouteGenerator.GenerateWithSource(document, collections: DefaultCollections);

        Assert.Equal("/blog/hello/", result.Route.Url);
        Assert.Equal(RouteGenerator.RouteSource.Collection, result.Source);
    }

    [Fact]
    public void Generate_NoRouteRule_Throws()
    {
        var item = Item("default");

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));
        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_UnknownTypeWithoutRule_Throws()
    {
        var item = Item("custom", new Dictionary<string, object> { ["type"] = "custom" });

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));
        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_RouteOverrideDict_OverridesAll()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/custom/foo/",
                ["outputPath"] = "custom/foo/index.html",
                ["template"] = "custom/template.html"
            }
        };
        var item = Item("ignored", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/custom/foo/", route.Url);
        Assert.Equal("custom/foo/index.html", route.OutputPath);
        Assert.Equal("custom/template.html", route.Template);
    }

    [Fact]
    public void Generate_RouteOverrideIndividualKeys_OverridesAll()
    {
        var meta = new Dictionary<string, object>
        {
            ["url"] = "/standalone/bar/",
            ["outputPath"] = "standalone/bar/index.html",
            ["template"] = "standalone/page.html"
        };
        var item = Item("ignored", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/standalone/bar/", route.Url);
        Assert.Equal("standalone/bar/index.html", route.OutputPath);
        Assert.Equal("standalone/page.html", route.Template);
    }

    [Fact]
    public void Generate_UrlNormalization_AddsLeadingSlash()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "no-leading/",
                ["outputPath"] = "out/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/no-leading/", route.Url);
    }

    [Fact]
    public void Generate_UrlNormalization_AddsTrailingSlash()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/no-trailing",
                ["outputPath"] = "out/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/no-trailing/", route.Url);
    }

    [Fact]
    public void Generate_UrlNormalization_TrimsWhitespace()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "  /trimmed/path/  ",
                ["outputPath"] = "out/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/trimmed/path/", route.Url);
    }

    [Fact]
    public void Generate_OutputPathEncoding_None_PreservesOriginal()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path with spaces/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item, "none");

        Assert.Equal("path with spaces/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_Slug_SlugifiesSegments()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/Hello World Here/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item, "slug");

        Assert.Equal("path/hello-world-here/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_UrlEncode_EncodesSegments()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/hello world/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item, "urlencode");

        Assert.Equal("path/hello%20world/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_Sanitize_ReplacesSpacesRemovesInvalidChars()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/hello world/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item, "sanitize");

        Assert.Equal("path/hello-world/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_DefaultNone_WhenOmitted()
    {
        var item = Item("slug-with-dash", new Dictionary<string, object> { ["collection"] = "page" });
        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("pages/slug-with-dash/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_Slug_OnDefaultPostPath()
    {
        var item = Item("My Post Title!", new Dictionary<string, object> { ["type"] = "post", ["collection"] = "post" });
        var route = RouteGenerator.Generate(item, "slug", collections: DefaultCollections);

        Assert.Equal("/blog/My Post Title!/", route.Url);
        Assert.Equal("blog/my-post-title/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_CaseInsensitive()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/Hello/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item, "SLUG");

        Assert.Equal("path/hello/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPath_NormalizesBackslashes()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "dir\\sub\\index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("dir/sub/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPath_TrimsLeadingSlashes()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "/leading/slash/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("leading/slash/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_PartialRouteOverride_AppliesOutputPathOnly()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["outputPath"] = "custom/about/index.html"
            }
        };
        meta["collection"] = "page";
        var item = Item("about", meta);

        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("/pages/about/", route.Url);
        Assert.Equal("custom/about/index.html", route.OutputPath.Replace('\\', '/'));
        Assert.Equal(string.Empty, route.Template);
    }

    [Fact]
    public void Generate_FullRouteOverride_DangerousUrl_Throws()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "https://evil.com",
                ["outputPath"] = "safe/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);

        Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));
    }

    [Fact]
    public void Generate_FullRouteOverride_DangerousOutputPath_Throws()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/safe/",
                ["outputPath"] = "../evil/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);

        Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("CON")]
    [InlineData("AUX")]
    public void Generate_EncodedOutputPathIsValidatedAgain(string slug)
    {
        var item = Item(slug, new Dictionary<string, object> { ["type"] = "post" });

        Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, "none"));
    }

    [Fact]
    public void Generate_Template_Trimmed()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "out/index.html",
                ["template"] = "  pages/trimmed.html  "
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("pages/trimmed.html", route.Template);
    }

    [Fact]
    public void Generate_RouteOverride_IncompleteUrlOnly_DerivesOutputPath()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/only-url/",
                ["outputPath"] = "",
                ["template"] = ""
            }
        };
        meta["collection"] = "page";
        var item = Item("fallback", meta);
        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("/only-url/", route.Url);
        Assert.Equal("only-url/index.html", route.OutputPath);
        Assert.Equal(string.Empty, route.Template);
    }

    [Fact]
    public void Generate_RouteOverride_IncompleteUrlAndOutputPath_DerivesTemplate()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/only-url/",
                ["outputPath"] = "out/index.html"
            }
        };
        meta["collection"] = "page";
        var item = Item("fallback", meta);
        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("/only-url/", route.Url);
        Assert.Equal("out/index.html", route.OutputPath);
        Assert.Equal(string.Empty, route.Template);
    }

    [Fact]
    public void Generate_PartialRouteOverride_UrlOnly_DerivesOutputPathAndKeepsCollectionTemplate()
    {
        var item = Item("hello", new Dictionary<string, object>
        {
            ["collection"] = "article",
            ["url"] = "/custom/hello/"
        });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["article"] = new("/articles/{slug}/", "pages/article.html")
        };

        var route = RouteGenerator.Generate(item, collections: collections);

        Assert.Equal("/custom/hello/", route.Url);
        Assert.Equal("custom/hello/index.html", route.OutputPath);
        Assert.Equal("pages/article.html", route.Template);
    }

    [Fact]
    public void Generate_PartialRouteOverride_UrlAndTemplate_DerivesOutputPathAndUsesTemplate()
    {
        var item = Item("hello", new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/custom/hello/",
                ["template"] = "pages/custom.html"
            },
            ["collection"] = "post"
        });

        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("/custom/hello/", route.Url);
        Assert.Equal("custom/hello/index.html", route.OutputPath);
        Assert.Equal("pages/custom.html", route.Template);
    }

    [Fact]
    public void Generate_Sanitize_CompressesMultipleDashes()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/hello---world/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item, "sanitize");

        Assert.Equal("path/hello-world/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_Sanitize_RemovesInvalidWindowsChars()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/hello<>:world/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item, "sanitize");

        Assert.Equal("path/helloworld/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_Slug_PreservesFileExtension()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/My.Document.pdf",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta);
        var route = RouteGenerator.Generate(item, "slug");

        Assert.Equal("path/mydocument.pdf", route.OutputPath);
    }

    [Fact]
    public void Generate_EmptySlug_WithCollectionRuleThrows()
    {
        var item = Item("", new Dictionary<string, object> { ["collection"] = "page" });

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, collections: DefaultCollections));
        Assert.Contains("route.outputPath", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Permalink pattern tests ──────────────────────────────────────────

    [Fact]
    public void Generate_PermalinkPattern_DateSlug()
    {
        var item = new ContentItem(
            Id: "id-1", Title: "My Post", Slug: "my-post",
            PublishAt: new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: "",
            Meta: new Dictionary<string, object> { ["type"] = "post" });

        var permalinks = new Dictionary<string, string> { ["post"] = "/{year}/{month}/{slug}/" };
        var route = RouteGenerator.Generate(item, "none", permalinks);

        Assert.Equal("/2025/03/my-post/", route.Url);
        Assert.Contains("my-post", route.OutputPath);
        Assert.Contains("index.html", route.OutputPath);
        Assert.Equal(string.Empty, route.Template);
    }

    [Fact]
    public void Generate_PermalinkPattern_YearMonthDaySlug()
    {
        var item = new ContentItem(
            Id: "id-1", Title: "T", Slug: "hello",
            PublishAt: new DateTimeOffset(2024, 12, 5, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: "",
            Meta: new Dictionary<string, object> { ["type"] = "post" });

        var permalinks = new Dictionary<string, string> { ["post"] = "/{year}/{month}/{day}/{slug}/" };
        var route = RouteGenerator.Generate(item, "none", permalinks);

        Assert.Equal("/2024/12/05/hello/", route.Url);
    }

    [Fact]
    public void Generate_PermalinkPattern_PageType()
    {
        var item = new ContentItem(
            Id: "id-1", Title: "T", Slug: "about",
            PublishAt: DateTimeOffset.MinValue,
            ContentHtml: "",
            Meta: new Dictionary<string, object> { ["type"] = "page" });

        var permalinks = new Dictionary<string, string> { ["page"] = "/docs/{slug}/" };
        var route = RouteGenerator.Generate(item, "none", permalinks);

        Assert.Equal("/docs/about/", route.Url);
        Assert.Equal(string.Empty, route.Template);
    }

    [Fact]
    public void Generate_PermalinkPattern_NoMatchThrows()
    {
        var item = Item("test-slug", new Dictionary<string, object> { ["type"] = "post" });
        var permalinks = new Dictionary<string, string> { ["page"] = "/p/{slug}/" };
        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, "none", permalinks));
        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_PermalinkPattern_OverrideStillTakesPrecedence()
    {
        var meta = new Dictionary<string, object>
        {
            ["type"] = "post",
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/custom/",
                ["outputPath"] = "custom/index.html",
                ["template"] = "pages/custom.html"
            }
        };
        var item = Item("test", meta);
        var permalinks = new Dictionary<string, string> { ["post"] = "/{year}/{slug}/" };
        var route = RouteGenerator.Generate(item, "none", permalinks);

        Assert.Equal("/custom/", route.Url);
        Assert.Equal("pages/custom.html", route.Template);
    }

    [Fact]
    public void ExpandPermalinkPattern_AllPlaceholders()
    {
        var item = new ContentItem(
            Id: "id-1", Title: "T", Slug: "my-slug",
            PublishAt: new DateTimeOffset(2025, 1, 9, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: "",
            Meta: new Dictionary<string, object> { ["type"] = "post" });

        var result = RouteGenerator.ExpandPermalinkPattern("/{type}/{year}/{month}/{day}/{slug}/", item);

        Assert.Equal("/post/2025/01/09/my-slug/", result);
    }

    [Fact]
    public void Generate_CollectionsRule_UsesCollectionPermalinkAndTemplate()
    {
        var item = Item("hello", new Dictionary<string, object>
        {
            ["collection"] = "article",
            ["type"] = "post"
        });

        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["article"] = new("/{year}/{slug}/", "pages/article.html")
        };

        var route = RouteGenerator.Generate(item, collections: collections);

        Assert.Equal("/0001/hello/", route.Url);
        Assert.Equal("0001/hello/index.html", route.OutputPath);
        Assert.Equal("pages/article.html", route.Template);
    }

    [Fact]
    public void Generate_CollectionsRule_TypeOnly_NoLongerMatchesCollectionRule()
    {
        var item = Item("hello", new Dictionary<string, object>
        {
            ["type"] = "article"
        });

        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["article"] = new("/articles/{slug}/", "pages/article.html")
        };

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, collections: collections));
        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
