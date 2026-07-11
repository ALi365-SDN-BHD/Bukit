using Bukit.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RouteGeneratorTests
{
    private static ContentDocument Item(
        string slug = "my-slug",
        IReadOnlyDictionary<string, object>? fieldValues = null) =>
        ContentDocument.Create(
            id: "id-1",
            title: "Title",
            slug: slug,
            publishAt: DateTimeOffset.MinValue,
            contentHtml: "",
            fields: ContentFieldReader.ToFieldMap(fieldValues ?? new Dictionary<string, object>()));

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
        var item = ContentDocument.Create(
            id: "id-2",
            title: "Title",
            slug: "field-post",
            publishAt: DateTimeOffset.MinValue,
            contentHtml: "",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["collection"] = new("text", "post")
            });

        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("/blog/field-post/", route.Url);
        Assert.Equal("blog/field-post/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_NoRouteRule_Throws()
    {
        var item = Item("default");

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));
        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_EmptyCollection_Throws()
    {
        var item = Item("empty-collection", new Dictionary<string, object>
        {
            ["type"] = "article"
        });
        item = item with
        {
            Record = item.Record with
            {
                Classification = item.Record.Classification with { Collection = string.Empty }
            }
        };
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            [string.Empty] = new("/invalid/{slug}/", "pages/article.html")
        };

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, collections: collections));

        Assert.Equal(DiagnosticCode.ContentCollectionMissing, ex.Code);
        Assert.Contains("collection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_FullRouteOverrideWithoutCollection_ThrowsBeforeOverrideResolution()
    {
        var item = Item("override", new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/custom/override/",
                ["template"] = "custom/page.html"
            }
        });

        var exception = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));

        Assert.Equal(DiagnosticCode.ContentCollectionMissing, exception.Code);
    }

    [Fact]
    public void Generate_TypePermalinkWithoutCollection_ThrowsBeforePermalinkResolution()
    {
        var item = Item("article", new Dictionary<string, object>
        {
            ["type"] = "article"
        });
        var permalinks = new Dictionary<string, string>
        {
            ["article"] = "/articles/{slug}/"
        };

        var exception = Assert.Throws<ConfigException>(() =>
            RouteGenerator.Generate(item, permalinks: permalinks));

        Assert.Equal(DiagnosticCode.ContentCollectionMissing, exception.Code);
    }

    [Fact]
    public void Generate_MissingCollectionUsesDocumentSourceKey()
    {
        var item = Item("article", new Dictionary<string, object> { ["type"] = "article" }) with
        {
            Source = new ContentSourceInfo("notion", SourceKey: "editorial")
        };

        var exception = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));

        Assert.Contains("source \"editorial\"", exception.Message, StringComparison.Ordinal);
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
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/custom/foo/",
                ["template"] = "custom/template.html"
            }
        };
        var item = Item("ignored", fieldValues);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/custom/foo/", route.Url);
        Assert.Equal("custom/foo/index.html", route.OutputPath);
        Assert.Equal("custom/template.html", route.Template);
    }

    [Fact]
    public void Generate_RouteOverrideIndividualKeys_OverridesAll()
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["url"] = "/standalone/bar/",
            ["template"] = "standalone/page.html"
        };
        var item = Item("ignored", fieldValues);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/standalone/bar/", route.Url);
        Assert.Equal("standalone/bar/index.html", route.OutputPath);
        Assert.Equal("standalone/page.html", route.Template);
    }

    [Fact]
    public void Generate_UrlNormalization_AddsLeadingSlash()
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "no-leading/",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", fieldValues);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/no-leading/", route.Url);
    }

    [Fact]
    public void Generate_UrlNormalization_AddsTrailingSlash()
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/no-trailing",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", fieldValues);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/no-trailing/", route.Url);
    }

    [Fact]
    public void Generate_UrlNormalization_TrimsWhitespace()
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "  /trimmed/path/  ",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", fieldValues);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/trimmed/path/", route.Url);
    }

    [Fact]
    public void Generate_OutputPathEncoding_None_PreservesOriginal()
    {
        var item = Item("path with spaces", new Dictionary<string, object> { ["collection"] = "page" });
        var route = RouteGenerator.Generate(item, "none", collections: DefaultCollections);

        Assert.Equal("pages/path with spaces/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_Slug_SlugifiesSegments()
    {
        var item = Item("Hello World Here", new Dictionary<string, object> { ["collection"] = "page" });
        var route = RouteGenerator.Generate(item, "slug", collections: DefaultCollections);

        Assert.Equal("pages/hello-world-here/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_UrlEncode_EncodesSegments()
    {
        var item = Item("hello world", new Dictionary<string, object> { ["collection"] = "page" });
        var route = RouteGenerator.Generate(item, "urlencode", collections: DefaultCollections);

        Assert.Equal("pages/hello%20world/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_Sanitize_ReplacesSpacesRemovesInvalidChars()
    {
        var item = Item("hello world", new Dictionary<string, object> { ["collection"] = "page" });
        var route = RouteGenerator.Generate(item, "sanitize", collections: DefaultCollections);

        Assert.Equal("pages/hello-world/index.html", route.OutputPath);
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
        var item = Item("Hello", new Dictionary<string, object> { ["collection"] = "page" });
        var route = RouteGenerator.Generate(item, "SLUG", collections: DefaultCollections);

        Assert.Equal("pages/hello/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_NestedRouteOutputPath_ThrowsRemovedField()
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["outputPath"] = "custom/about/index.html"
            }
        };
        fieldValues["collection"] = "page";
        var item = Item("about", fieldValues);

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, collections: DefaultCollections));

        Assert.Equal(DiagnosticCode.RouteOutputPathRejected, ex.Code);
        Assert.Contains("route.outputPath is removed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_FullRouteOverride_DangerousUrl_Throws()
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "https://evil.com",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", fieldValues);

        Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));
    }

    [Fact]
    public void Generate_FullRouteOverride_DangerousOutputPath_Throws()
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/safe/",
                ["outputPath"] = "../evil/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", fieldValues);

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));
        Assert.Equal(DiagnosticCode.RouteOutputPathRejected, ex.Code);
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
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["template"] = "  pages/trimmed.html  "
            }
        };
        var item = Item("x", fieldValues);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("pages/trimmed.html", route.Template);
    }

    [Fact]
    public void Generate_RouteOverride_IncompleteUrlOnly_DerivesOutputPath()
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/only-url/",
                ["template"] = ""
            }
        };
        fieldValues["collection"] = "page";
        var item = Item("fallback", fieldValues);
        var route = RouteGenerator.Generate(item, collections: DefaultCollections);

        Assert.Equal("/only-url/", route.Url);
        Assert.Equal("only-url/index.html", route.OutputPath);
        Assert.Equal(string.Empty, route.Template);
    }

    [Fact]
    public void Generate_RouteOverride_IncompleteUrlAndOutputPath_DerivesTemplate()
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/only-url/",
                ["outputPath"] = "out/index.html"
            }
        };
        fieldValues["collection"] = "page";
        var item = Item("fallback", fieldValues);

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, collections: DefaultCollections));
        Assert.Equal(DiagnosticCode.RouteOutputPathRejected, ex.Code);
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
        var item = Item("hello---world", new Dictionary<string, object> { ["collection"] = "page" });
        var route = RouteGenerator.Generate(item, "sanitize", collections: DefaultCollections);

        Assert.Equal("pages/hello-world/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_Sanitize_RemovesInvalidWindowsChars()
    {
        var item = Item("hello<>:world", new Dictionary<string, object> { ["collection"] = "page" });
        var route = RouteGenerator.Generate(item, "sanitize", collections: DefaultCollections);

        Assert.Equal("pages/helloworld/index.html", route.OutputPath);
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
        var item = ContentDocument.Create(
            id: "id-1", title: "My Post", slug: "my-post",
            publishAt: new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "post" }));

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
        var item = ContentDocument.Create(
            id: "id-1", title: "T", slug: "hello",
            publishAt: new DateTimeOffset(2024, 12, 5, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "post" }));

        var permalinks = new Dictionary<string, string> { ["post"] = "/{year}/{month}/{day}/{slug}/" };
        var route = RouteGenerator.Generate(item, "none", permalinks);

        Assert.Equal("/2024/12/05/hello/", route.Url);
    }

    [Fact]
    public void Generate_PermalinkPattern_PageType()
    {
        var item = ContentDocument.Create(
            id: "id-1", title: "T", slug: "about",
            publishAt: DateTimeOffset.MinValue,
            contentHtml: "",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" }));

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
        var fieldValues = new Dictionary<string, object>
        {
            ["type"] = "post",
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/custom/",
                ["template"] = "pages/custom.html"
            }
        };
        var item = Item("test", fieldValues);
        var permalinks = new Dictionary<string, string> { ["post"] = "/{year}/{slug}/" };
        var route = RouteGenerator.Generate(item, "none", permalinks);

        Assert.Equal("/custom/", route.Url);
        Assert.Equal("pages/custom.html", route.Template);
    }

    [Fact]
    public void ExpandPermalinkPattern_AllPlaceholders()
    {
        var item = ContentDocument.Create(
            id: "id-1", title: "T", slug: "my-slug",
            publishAt: new DateTimeOffset(2025, 1, 9, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "post" }));

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
    public void Generate_CollectionsRule_TypeOnly_UsesCanonicalCollection()
    {
        var item = Item("hello", new Dictionary<string, object>
        {
            ["type"] = "article"
        });

        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["article"] = new("/articles/{slug}/", "pages/article.html")
        };

        var route = RouteGenerator.Generate(item, collections: collections);

        Assert.Equal("/articles/hello/", route.Url);
        Assert.Equal("pages/article.html", route.Template);
    }
}
