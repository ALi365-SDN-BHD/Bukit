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

    [Fact]
    public void Generate_PostType_ProducesBlogRouteAndPostTemplate()
    {
        var item = Item("my-post", new Dictionary<string, object> { ["type"] = "post" });
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/blog/my-post/", route.Url);
        Assert.Equal("blog/my-post/index.html", route.OutputPath);
        Assert.Equal("pages/post.html", route.Template);
    }

    [Fact]
    public void Generate_PageType_ProducesPagesRouteAndPageTemplate()
    {
        var item = Item("about", new Dictionary<string, object> { ["type"] = "page" });
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/pages/about/", route.Url);
        Assert.Equal("pages/about/index.html", route.OutputPath);
        Assert.Equal("pages/page.html", route.Template);
    }

    [Fact]
    public void Generate_NoType_DefaultsToPage()
    {
        var item = Item("default");
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/pages/default/", route.Url);
        Assert.Contains("default", route.OutputPath);
        Assert.Equal("pages/page.html", route.Template);
    }

    [Fact]
    public void Generate_UnknownType_DefaultsToPage()
    {
        var item = Item("custom", new Dictionary<string, object> { ["type"] = "custom" });
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/pages/custom/", route.Url);
        Assert.Equal("pages/custom/index.html", route.OutputPath);
        Assert.Equal("pages/page.html", route.Template);
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
        var item = Item("slug-with-dash");
        var route = RouteGenerator.Generate(item);

        Assert.Equal("pages/slug-with-dash/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_OutputPathEncoding_Slug_OnDefaultPostPath()
    {
        var item = Item("My Post Title!", new Dictionary<string, object> { ["type"] = "post" });
        var route = RouteGenerator.Generate(item, "slug");

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
        var item = Item("about", meta);

        var route = RouteGenerator.Generate(item);

        Assert.Equal("/pages/about/", route.Url);
        Assert.Equal("custom/about/index.html", route.OutputPath.Replace('\\', '/'));
        Assert.Equal("pages/page.html", route.Template);
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
        var item = Item("fallback", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/only-url/", route.Url);
        Assert.Equal("only-url/index.html", route.OutputPath);
        Assert.Equal("pages/page.html", route.Template);
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
        var item = Item("fallback", meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/only-url/", route.Url);
        Assert.Equal("out/index.html", route.OutputPath);
        Assert.Equal("pages/page.html", route.Template);
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
            ["type"] = "post"
        });

        var route = RouteGenerator.Generate(item);

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
    public void Generate_EmptySlug_ProducesRoute()
    {
        var item = Item("");
        var route = RouteGenerator.Generate(item);

        Assert.StartsWith("/pages/", route.Url);
        Assert.EndsWith("/", route.Url);
        Assert.Contains("index.html", route.OutputPath);
        Assert.Equal("pages/page.html", route.Template);
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
        Assert.Equal("pages/post.html", route.Template);
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
        Assert.Equal("pages/page.html", route.Template);
    }

    [Fact]
    public void Generate_PermalinkPattern_NoMatchFallsToDefault()
    {
        var item = Item("test-slug", new Dictionary<string, object> { ["type"] = "post" });
        var permalinks = new Dictionary<string, string> { ["page"] = "/p/{slug}/" };
        var route = RouteGenerator.Generate(item, "none", permalinks);

        Assert.Equal("/blog/test-slug/", route.Url);
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
    public void Generate_CollectionsRule_TypeAsFallbackCollectionKey()
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
        Assert.Equal("articles/hello/index.html", route.OutputPath);
        Assert.Equal("pages/article.html", route.Template);
    }
}
