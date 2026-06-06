using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SearchIndexBuilderTests
{
    private static ContentItem TestItem(
        string Id,
        string Title,
        string Slug,
        DateTimeOffset PublishAt,
        string? ContentHtml,
        IReadOnlyDictionary<string, object>? Meta = null,
        IReadOnlyDictionary<string, ContentField>? Fields = null,
        string? BodyKey = null)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (Meta is not null)
        {
            foreach (var (key, value) in Meta)
            {
                fields[key] = new ContentField("test", value);
            }
        }

        if (Fields is not null)
        {
            foreach (var (key, value) in Fields)
            {
                fields[key] = value;
            }
        }

        return new ContentItem(
            Id,
            Title,
            Slug,
            PublishAt,
            ContentHtml,
            fields,
            BodyKey);
    }

    private static Dictionary<string, ContentField> ToFields(IReadOnlyDictionary<string, object>? values)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (values is null)
        {
            return fields;
        }

        foreach (var (key, value) in values)
        {
            fields[key] = new ContentField("test", value);
        }

        return fields;
    }

    [Fact]
    public void StripHtmlToText_BasicHtml_StripsTagsAndJoinsWithSpace()
    {
        var result = SearchIndexBuilder.StripHtmlToText("<p>Hello</p><div>World</div>");

        Assert.Equal("Hello  World", result);
    }

    [Fact]
    public void StripHtmlToText_HtmlEntities_AreDecoded()
    {
        var result = SearchIndexBuilder.StripHtmlToText("&amp; &lt; &gt; &quot;");

        Assert.Equal("& < > \"", result);
    }

    [Fact]
    public void StripHtmlToText_ScriptTags_AreStripped()
    {
        var result = SearchIndexBuilder.StripHtmlToText("<script>alert('xss')</script>hello");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void StripHtmlToText_ScriptTagsWithAttributes_AreStripped()
    {
        var result = SearchIndexBuilder.StripHtmlToText("<script type=\"text/javascript\">var x=1;</script>world");

        Assert.Equal("world", result);
    }

    [Fact]
    public void StripHtmlToText_StyleTags_AreStripped()
    {
        var result = SearchIndexBuilder.StripHtmlToText("<style>body{color:red}</style>text");

        Assert.Equal("text", result);
    }

    [Fact]
    public void StripHtmlToText_StyleTagsWithAttributes_AreStripped()
    {
        var result = SearchIndexBuilder.StripHtmlToText("<style scoped>h1{font-size:2em}</style>content");

        Assert.Equal("content", result);
    }

    [Fact]
    public void StripHtmlToText_HtmlComments_AreStripped()
    {
        var result = SearchIndexBuilder.StripHtmlToText("<!-- comment -->hello");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void StripHtmlToText_MultilineComment_IsStripped()
    {
        var result = SearchIndexBuilder.StripHtmlToText("<!-- multi\nline\ncomment -->after");

        Assert.Equal("after", result);
    }

    [Fact]
    public void StripHtmlToText_MixedContent_StripsScriptStyleCommentsAndDecodesEntities()
    {
        var result = SearchIndexBuilder.StripHtmlToText(
            "<script>code</script><p>text &amp; more</p><!-- comment --><style>css</style>end");

        Assert.Equal("text & more   end", result);
    }

    [Fact]
    public void StripHtmlToText_EmptyString_ReturnsEmpty()
    {
        var result = SearchIndexBuilder.StripHtmlToText("");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void StripHtmlToText_Null_ReturnsEmpty()
    {
        var result = SearchIndexBuilder.StripHtmlToText(null!);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void StripHtmlToText_WhitespaceOnly_ReturnsEmpty()
    {
        var result = SearchIndexBuilder.StripHtmlToText("   \t  \n  ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void StripHtmlToText_SelfClosingTags_AreStripped()
    {
        var result = SearchIndexBuilder.StripHtmlToText("Hello<br/>World<img src=\"x.jpg\"/>end");

        Assert.Equal("Hello World end", result);
    }

    [Fact]
    public void StripHtmlToText_PlainText_ReturnsUnchanged()
    {
        var result = SearchIndexBuilder.StripHtmlToText("just plain text");

        Assert.Equal("just plain text", result);
    }

    [Fact]
    public void NormalizeSearchUrl_BaseUrlIsSlash_ReturnsUrlWithLeadingSlash()
    {
        var result = SearchIndexBuilder.NormalizeSearchUrl("/", "/page/");

        Assert.Equal("/page/", result);
    }

    [Fact]
    public void NormalizeSearchUrl_BaseUrlIsNull_ReturnsUrlWithLeadingSlash()
    {
        var result = SearchIndexBuilder.NormalizeSearchUrl(null!, "/page/");

        Assert.Equal("/page/", result);
    }

    [Fact]
    public void NormalizeSearchUrl_BaseUrlIsEmpty_ReturnsUrlWithLeadingSlash()
    {
        var result = SearchIndexBuilder.NormalizeSearchUrl("", "/page/");

        Assert.Equal("/page/", result);
    }

    [Fact]
    public void NormalizeSearchUrl_BaseUrlWithPath_PrependsBaseToUrl()
    {
        var result = SearchIndexBuilder.NormalizeSearchUrl("/en", "/page/");

        Assert.Equal("/en/page/", result);
    }

    [Fact]
    public void NormalizeSearchUrl_BaseUrlWithTrailingSlash_TrimsTrailingSlash()
    {
        var result = SearchIndexBuilder.NormalizeSearchUrl("/en/", "/page/");

        Assert.Equal("/en/page/", result);
    }

    [Fact]
    public void NormalizeSearchUrl_UrlWithoutLeadingSlash_AddsLeadingSlash()
    {
        var result = SearchIndexBuilder.NormalizeSearchUrl("/en", "page/");

        Assert.Equal("/en/page/", result);
    }

    [Fact]
    public void NormalizeSearchUrl_BaseUrlWithoutLeadingSlash_AddsLeadingSlash()
    {
        var result = SearchIndexBuilder.NormalizeSearchUrl("en", "/page/");

        Assert.Equal("/en/page/", result);
    }

    [Fact]
    public void BuildItemMap_EmptyInput_ReturnsEmptyDictionary()
    {
        var result = SearchIndexBuilder.BuildItemMap([]);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildItemMap_SingleItem_MapsByOutputPath()
    {
        var item = TestItem(
            Id: "post-1",
            Title: "Test Post",
            Slug: "test-post",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null);
        var route = new RouteInfo("/blog/test-post/", "blog/test-post/index.html", "pages/post.html");
        var input = new[] { (item, route) };

        var result = SearchIndexBuilder.BuildItemMap(input);

        Assert.Single(result);
        Assert.True(result.ContainsKey("blog/test-post/index.html"));
        Assert.Equal(item, result["blog/test-post/index.html"]);
    }

    [Fact]
    public void BuildItemMap_MultipleItems_MapsByNormalizedOutputPath()
    {
        var item1 = TestItem(
            Id: "a",
            Title: "Alpha",
            Slug: "alpha",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null);
        var item2 = TestItem(
            Id: "b",
            Title: "Beta",
            Slug: "beta",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null);
        var item3 = TestItem(
            Id: "c",
            Title: "Gamma",
            Slug: "gamma",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null);

        var route1 = new RouteInfo("/blog/alpha/", "blog/alpha/index.html", "pages/post.html");
        var route2 = new RouteInfo("/pages/beta/", "pages/beta/index.html", "pages/page.html");
        var route3 = new RouteInfo("/en/gamma/", "en/gamma/index.html", "pages/post.html");

        var input = new[] { (item1, route1), (item2, route2), (item3, route3) };

        var result = SearchIndexBuilder.BuildItemMap(input);

        Assert.Equal(3, result.Count);
        Assert.Equal(item1, result["blog/alpha/index.html"]);
        Assert.Equal(item2, result["pages/beta/index.html"]);
        Assert.Equal(item3, result["en/gamma/index.html"]);
    }

    [Fact]
    public void BuildItemMap_OutputPathWithBackslashes_NormalizesToForwardSlashes()
    {
        var item = TestItem(
            Id: "x",
            Title: "X",
            Slug: "x",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null);
        var route = new RouteInfo("/pages/x/", "pages\\x\\index.html", "pages/page.html");

        var result = SearchIndexBuilder.BuildItemMap([(item, route)]);

        Assert.True(result.ContainsKey("pages/x/index.html"));
    }

    [Fact]
    public void WriteSearchItem_EmitsCanonicalContentMetadata()
    {
        var item = TestItem(
            Id: "search-1",
            Title: "Search Post",
            Slug: "search-post",
            PublishAt: DateTimeOffset.Parse("2026-06-05T12:00:00Z"),
            ContentHtml: "<p>Body</p>",
            Fields: ToFields(new Dictionary<string, object>
            {
                ["type"] = "post",
                ["summary"] = "Search summary",
                ["language"] = "en",
                ["source"] = "notion",
                ["review_status"] = "approved",
                ["tags"] = new[] { "bukit" },
                ["entities"] = new object[]
                {
                    new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = "company",
                        ["name"] = "Bukit"
                    }
                }
            }));
        var route = new RouteInfo("/search-post/", "search-post/index.html", "pages/post.html");

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            SearchIndexBuilder.WriteSearchItem(writer, item, route, "/", new DictionaryContentBodyStore(new Dictionary<string, ContentBody>
            {
                ["search-1"] = new("<p>Body</p>")
            }), emitSnippet: true);
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.Equal("approved", doc.RootElement.GetProperty("reviewStatus").GetString());
        Assert.Equal("notion", doc.RootElement.GetProperty("source").GetString());
        Assert.Equal("post", doc.RootElement.GetProperty("contentType").GetString());
        Assert.Equal("Bukit", doc.RootElement.GetProperty("entities")[0].GetString());
    }

    [Fact]
    public void WriteSearchItem_PrefersCanonicalSummaryClassificationAndLanguage()
    {
        var item = TestItem(
            Id: "search-2",
            Title: "Structured Post",
            Slug: "structured-post",
            PublishAt: DateTimeOffset.Parse("2026-06-05T12:00:00Z"),
            ContentHtml: "<p>Structured body</p>",
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "guide"),
                ["summary"] = new("text", "Canonical summary"),
                ["language"] = new("text", "ms-MY"),
                ["source"] = new("text", "notion"),
                ["tags"] = new("list", new object[] { "bukit", "canonical" }),
                ["categories"] = new("list", new object[] { "docs" })
            });
        var route = new RouteInfo("/structured-post/", "structured-post/index.html", "pages/post.html");

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            SearchIndexBuilder.WriteSearchItem(writer, item, route, "/", new DictionaryContentBodyStore(new Dictionary<string, ContentBody>
            {
                ["search-2"] = new("<p>Structured body</p>")
            }), emitSnippet: true);
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.Equal("Canonical summary", doc.RootElement.GetProperty("summary").GetString());
        Assert.Equal("Canonical summary", doc.RootElement.GetProperty("snippet").GetString());
        Assert.Equal("guide", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("ms-MY", doc.RootElement.GetProperty("language").GetString());
        Assert.Equal("notion", doc.RootElement.GetProperty("sourceKey").GetString());
        Assert.Equal("bukit", doc.RootElement.GetProperty("tags")[0].GetString());
        Assert.Equal("docs", doc.RootElement.GetProperty("categories")[0].GetString());
    }

    [Fact]
    public void WriteSearchItem_ContentDocument_EmitsCanonicalDocumentMetadata()
    {
        var record = new ContentRecord(
            Identity: new ContentIdentity("doc-search", "doc-search", "doc-search", "guide", "published"),
            Presentation: new ContentPresentation("Document Search", "Document summary", "<p>Document body</p>", "en", Array.Empty<string>()),
            Classification: new ContentClassification("guide", "docs", new[] { "docs" }, new[] { "canonical" }),
            Ownership: new ContentOwnership("Ali", null, null, null),
            Lifecycle: new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T12:00:00Z"), null, null, null),
            Provenance: new ProvenanceRecord("notion", null, Array.Empty<string>(), Array.Empty<string>(), null),
            Trust: new TrustMetadata(null, "approved", Array.Empty<string>()),
            Entities: new[] { new EntityRecord("company", "Bukit", null, "company:bukit") },
            Relations: Array.Empty<ContentRelation>(),
            Media: Array.Empty<MediaAsset>());
        var document = new ContentDocument(
            record,
            new ContentBodyRef("<p>Document body</p>", null, null, null),
            new ContentRoutePolicy(null, null, null, null, "docs"),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<ContentDiagnostic>());
        var route = new RouteInfo("/doc-search/", "doc-search/index.html", "pages/post.html");

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            SearchIndexBuilder.WriteSearchItem(writer, document, route, "/", emitSnippet: true);
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.Equal("doc-search", doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("Document summary", doc.RootElement.GetProperty("summary").GetString());
        Assert.Equal("Document body", doc.RootElement.GetProperty("content").GetString());
        Assert.Equal("approved", doc.RootElement.GetProperty("reviewStatus").GetString());
        Assert.Equal("notion", doc.RootElement.GetProperty("sourceKey").GetString());
        Assert.Equal("Bukit", doc.RootElement.GetProperty("entities")[0].GetString());
    }

    [Fact]
    public void GenerateSingleSearchIndex_ContentDocuments_WritesIndexableDocumentsOnly()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-search-docs-" + Guid.NewGuid().ToString("N"));
        var included = SearchDocument("included", "Included", excludeFromSearch: false);
        var excluded = SearchDocument("excluded", "Excluded", excludeFromSearch: true);
        var includedRoute = new RouteInfo("/included/", "included/index.html", "pages/post.html");
        var excludedRoute = new RouteInfo("/excluded/", "excluded/index.html", "pages/post.html");
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["included/index.html"] = new(includedRoute, "/included/", null, true, DateTimeOffset.UnixEpoch, "included", "post"),
            ["excluded/index.html"] = new(excludedRoute, "/excluded/", null, true, DateTimeOffset.UnixEpoch, "excluded", "post")
        };

        SearchIndexBuilder.GenerateSingleSearchIndex(
            outputDir,
            "/",
            emitSnippet: false,
            routed: new[] { (included, includedRoute), (excluded, excludedRoute) },
            seoIndex: seoIndex);

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDir, "search.json")));
        var item = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("included", item.GetProperty("id").GetString());
    }

    private static ContentDocument SearchDocument(string id, string title, bool excludeFromSearch)
    {
        var record = new ContentRecord(
            Identity: new ContentIdentity(id, id, id, "post", "published"),
            Presentation: new ContentPresentation(title, null, $"<p>{title}</p>", "en", Array.Empty<string>()),
            Classification: new ContentClassification("post", "post", Array.Empty<string>(), Array.Empty<string>()),
            Ownership: new ContentOwnership(null, null, null, null),
            Lifecycle: new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
            Provenance: new ProvenanceRecord("markdown", null, Array.Empty<string>(), Array.Empty<string>(), null),
            Trust: new TrustMetadata(null, "approved", Array.Empty<string>()),
            Entities: Array.Empty<EntityRecord>(),
            Relations: Array.Empty<ContentRelation>(),
            Media: Array.Empty<MediaAsset>());

        return new ContentDocument(
            record,
            new ContentBodyRef($"<p>{title}</p>", null, null, null),
            new ContentRoutePolicy(null, null, null, null, "post"),
            new ContentPublishPolicy(false, false, false, false, excludeFromSearch, false, false),
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<ContentDiagnostic>());
    }
}
