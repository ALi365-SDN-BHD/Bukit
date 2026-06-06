using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RouteGeneratorCoverageTests
{
    private static ContentItem Item(
        string slug = "my-slug",
        string title = "Title",
        IReadOnlyDictionary<string, object>? meta = null) =>
        new(
            Id: "id-1",
            Title: title,
            Slug: slug,
            PublishAt: DateTimeOffset.MinValue,
            ContentHtml: "",
            Fields: ContentFieldReader.ToFieldMap(meta ?? new Dictionary<string, object>()));

    private static ContentItem ItemWithDate(
        string slug,
        string title,
        int year, int month, int day,
        IReadOnlyDictionary<string, object>? meta = null) =>
        new(
            Id: "id-1",
            Title: title,
            Slug: slug,
            PublishAt: new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: "",
            Fields: ContentFieldReader.ToFieldMap(meta ?? new Dictionary<string, object>()));

    // ── Slugify tests (via Generate with "slug" encoding) ────────────────

    [Fact]
    public void Generate_SlugEncoding_NormalText_SlugifiesToLowerDashSeparated()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/Hello World Test/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "slug");

        Assert.Equal("path/hello-world-test/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_SlugEncoding_OnlySpecialChars_SegmentBecomesPage()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "base/!@#$%^&/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "slug");

        Assert.Contains("page", route.OutputPath);
    }

    [Fact]
    public void Generate_SlugEncoding_LeadingTrailingDashes_Trimmed()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/  leading trailing  /index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "slug");

        Assert.Equal("path/leading-trailing/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_SlugEncoding_UnderscoresBecomeDashes()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/my_file_name/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "slug");

        Assert.Equal("path/my-file-name/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_SlugEncoding_EmptyOrWhitespaceSegment_BecomesPage()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "   /index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "slug");

        Assert.Equal("index.html", route.OutputPath);
    }

    // ── SlugifySegment tests (via Generate with "slug" encoding) ─────────

    [Fact]
    public void Generate_SlugEncoding_LeadingDotPreserved()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "base/.hidden/config.json",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "slug");

        Assert.Equal("base/.hidden/config.json", route.OutputPath);
    }

    [Fact]
    public void Generate_SlugEncoding_DotFileExtension()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "data/My Report.pdf",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "slug");

        Assert.Equal("data/my-report.pdf", route.OutputPath);
    }

    // ── SanitizeSegment tests (via Generate with "sanitize" encoding) ────

    [Fact]
    public void Generate_SanitizeEncoding_AllWhitespaceSegment_BecomesPage()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "    /index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "sanitize");

        Assert.Equal("index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_SanitizeEncoding_ControlCharsStripped()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/hel\u0001lo\u0002world/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "sanitize");

        Assert.Equal("path/helloworld/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_SanitizeEncoding_TrailingDotsAndSpaces_Trimmed()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/test...  /index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "sanitize");

        Assert.Equal("path/test...-/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_SanitizeEncoding_WindowsInvalidCharsRemoved()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/a<b>c:d\"e|f?g*h/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "sanitize");

        Assert.Equal("path/abcdefgh/index.html", route.OutputPath);
    }

    // ── CompressDashes tests (via Generate with "sanitize" encoding) ──────

    [Fact]
    public void Generate_SanitizeEncoding_MultipleSpacesAndDashes_Compressed()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/a---b  c/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "sanitize");

        Assert.Equal("path/a-b-c/index.html", route.OutputPath);
    }

    // ── NormalizeUrl tests (via Generate and ExpandPermalinkPattern) ──────

    [Fact]
    public void Generate_NormalizeUrl_AlreadyNormalized_Unchanged()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/already/normalized/",
                ["outputPath"] = "already/normalized/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/already/normalized/", route.Url);
    }

    [Fact]
    public void Generate_NormalizeUrl_MissingLeadingSlash_Added()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "no-slash/path/",
                ["outputPath"] = "no-slash/path/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item);

        Assert.Equal("/no-slash/path/", route.Url);
    }

    // ── NormalizeOutputPath tests (via Generate) ──────────────────────────

    [Fact]
    public void Generate_NormalizeOutputPath_LeadingSlashStripped()
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
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item);

        Assert.DoesNotContain("\\", route.OutputPath);
        Assert.Equal("leading/slash/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_NormalizeOutputPath_BackslashesNormalized()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "dir\\sub\\file.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item);

        Assert.DoesNotContain("\\", route.OutputPath);
        Assert.Equal("dir/sub/file.html", route.OutputPath);
    }

    // ── ExpandPermalinkPattern {title} tests ──────────────────────────────

    [Fact]
    public void ExpandPermalinkPattern_TitlePlaceholder_SlugifiesTitle()
    {
        var item = ItemWithDate("my-slug", "Hello Beautiful World", 2025, 6, 15);
        var result = RouteGenerator.ExpandPermalinkPattern("/{title}/", item);

        Assert.Equal("/hello-beautiful-world/", result);
    }

    [Fact]
    public void ExpandPermalinkPattern_TitlePlaceholder_EmptyTitle_EmptySlug()
    {
        var item = ItemWithDate("my-slug", "", 2025, 6, 15);
        var result = RouteGenerator.ExpandPermalinkPattern("/{title}/", item);

        Assert.Equal("//", result);
    }

    [Fact]
    public void ExpandPermalinkPattern_TitleWithSpecialChars_Slugifies()
    {
        var item = ItemWithDate("my-slug", "Hello! @World #2024", 2025, 6, 15);
        var result = RouteGenerator.ExpandPermalinkPattern("/{title}/", item);

        Assert.Equal("/hello-world-2024/", result);
    }

    // ── ExpandPermalinkPattern {type} tests ───────────────────────────────

    [Fact]
    public void ExpandPermalinkPattern_TypePlaceholder_MissingTypeExpandsEmpty()
    {
        var item = ItemWithDate("my-slug", "T", 2025, 6, 15, meta: new Dictionary<string, object>());
        var result = RouteGenerator.ExpandPermalinkPattern("/{type}/{slug}/", item);

        Assert.Equal("//my-slug/", result);
    }

    [Fact]
    public void ExpandPermalinkPattern_TypePlaceholder_ExplicitType()
    {
        var item = ItemWithDate("my-slug", "T", 2025, 6, 15,
            meta: new Dictionary<string, object> { ["type"] = "article" });
        var result = RouteGenerator.ExpandPermalinkPattern("/{type}/{slug}/", item);

        Assert.Equal("/article/my-slug/", result);
    }

    [Fact]
    public void ExpandPermalinkPattern_TypePlaceholder_NonStringType_UsesToString()
    {
        var item = ItemWithDate("my-slug", "T", 2025, 6, 15,
            meta: new Dictionary<string, object> { ["type"] = 42 });
        var result = RouteGenerator.ExpandPermalinkPattern("/{type}/{slug}/", item);

        Assert.Equal("/42/my-slug/", result);
    }

    // ── ExpandPermalinkPattern all placeholders ───────────────────────────

    [Fact]
    public void ExpandPermalinkPattern_AllPlaceholders_ReplacesCorrectly()
    {
        var item = ItemWithDate("hello-world", "My Great Post", 2024, 3, 7,
            meta: new Dictionary<string, object> { ["type"] = "post" });
        var result = RouteGenerator.ExpandPermalinkPattern("/{type}/{year}/{month}/{day}/{slug}/{title}/", item);

        Assert.Equal("/post/2024/03/07/hello-world/my-great-post/", result);
    }

    // ── UrlEncodeSegment with unicode (via Generate with "urlencode") ─────

    [Fact]
    public void Generate_UrlEncode_UnicodeCharacters_Encoded()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/中文文件/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "urlencode");

        Assert.Equal("path/" + Uri.EscapeDataString("中文文件") + "/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_UrlEncode_SpacesEncoded()
    {
        var meta = new Dictionary<string, object>
        {
            ["route"] = new Dictionary<string, object>
            {
                ["url"] = "/u/",
                ["outputPath"] = "path/hello world here/index.html",
                ["template"] = "t.html"
            }
        };
        var item = Item("x", meta: meta);
        var route = RouteGenerator.Generate(item, "urlencode");

        Assert.Equal("path/hello%20world%20here/index.html", route.OutputPath);
    }

    // ── Explicit collection tests ────────────────────────────────────────

    [Fact]
    public void Generate_GetCollection_NoCollectionField_NoLongerMatchesType()
    {
        var item = Item("hello", meta: new Dictionary<string, object> { ["type"] = "article" });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["article"] = new("/articles/{slug}/", "pages/article.html")
        };
        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, collections: collections));

        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_GetCollection_ExplicitCollection_WinsOverType()
    {
        var item = Item("hello", meta: new Dictionary<string, object>
        {
            ["collection"] = "blog-posts",
            ["type"] = "article"
        });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["blog-posts"] = new("/blog/{slug}/", "pages/blog.html"),
            ["article"] = new("/articles/{slug}/", "pages/article.html")
        };
        var route = RouteGenerator.Generate(item, collections: collections);

        Assert.Equal("/blog/hello/", route.Url);
        Assert.Equal("pages/blog.html", route.Template);
    }

    [Fact]
    public void Generate_GetCollection_EmptyCollectionField_NoLongerMatchesType()
    {
        var item = Item("hello", meta: new Dictionary<string, object>
        {
            ["collection"] = "",
            ["type"] = "guide"
        });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["guide"] = new("/guides/{slug}/", "pages/guide.html")
        };
        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, collections: collections));

        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_GetCollection_WhitespaceCollectionField_NoLongerMatchesType()
    {
        var item = Item("hello", meta: new Dictionary<string, object>
        {
            ["collection"] = "   ",
            ["type"] = "doc"
        });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["doc"] = new("/docs/{slug}/", "pages/doc.html")
        };
        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item, collections: collections));

        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── GetType with non-string meta value tests ──────────────────────────

    [Fact]
    public void Generate_GetType_IntMetaValueWithoutRule_Throws()
    {
        var item = Item("hello", meta: new Dictionary<string, object> { ["type"] = 99 });

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));
        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_GetType_NullMetaTypeWithoutRule_Throws()
    {
        var meta = new Dictionary<string, object> { ["type"] = (object?)null! };
        var item = Item("hello", meta: meta);

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(item));
        Assert.Contains("No route rule matches", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── ExpandPermalinkPattern without type meta ──────────────────────────

    [Fact]
    public void ExpandPermalinkPattern_NoTypeMeta_ExpandsEmpty()
    {
        var item = ItemWithDate("slug", "T", 2025, 1, 1);
        var result = RouteGenerator.ExpandPermalinkPattern("/{type}/{slug}/", item);

        Assert.Equal("//slug/", result);
    }

    [Fact]
    public void ExpandPermalinkPattern_NullTypeValue_ExpandsEmpty()
    {
        var meta = new Dictionary<string, object> { ["type"] = (object?)null! };
        var item = ItemWithDate("slug", "T", 2025, 1, 1, meta: meta);
        var result = RouteGenerator.ExpandPermalinkPattern("/{type}/{slug}/", item);

        Assert.Equal("//slug/", result);
    }
}
