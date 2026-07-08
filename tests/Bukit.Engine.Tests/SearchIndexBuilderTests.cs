using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SearchIndexBuilderTests
{
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
    public void BuildDocumentMap_EmptyInput_ReturnsEmptyDictionary()
    {
        var result = SearchIndexBuilder.BuildDocumentMap([]);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildDocumentMap_SingleDocument_MapsByOutputPath()
    {
        var document = ContentDocument.Create(
            "post-1",
            "Test Post",
            "test-post",
            DateTimeOffset.UtcNow,
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));
        var route = new RouteInfo("/blog/test-post/", "blog/test-post/index.html", "pages/post.html");
        var input = new[] { new RoutedContentDocument(document, route) };

        var result = SearchIndexBuilder.BuildDocumentMap(input);

        Assert.Single(result);
        Assert.True(result.ContainsKey("blog/test-post/index.html"));
        Assert.Equal(document, result["blog/test-post/index.html"]);
    }

    [Fact]
    public void BuildDocumentMap_MultipleDocuments_MapsByNormalizedOutputPath()
    {
        var document1 = ContentDocument.Create("a", "Alpha", "alpha", DateTimeOffset.UtcNow, null, ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));
        var document2 = ContentDocument.Create("b", "Beta", "beta", DateTimeOffset.UtcNow, null, ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));
        var document3 = ContentDocument.Create("c", "Gamma", "gamma", DateTimeOffset.UtcNow, null, ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));

        var route1 = new RouteInfo("/blog/alpha/", "blog/alpha/index.html", "pages/post.html");
        var route2 = new RouteInfo("/pages/beta/", "pages/beta/index.html", "pages/page.html");
        var route3 = new RouteInfo("/en/gamma/", "en/gamma/index.html", "pages/post.html");

        var input = new[]
        {
            new RoutedContentDocument(document1, route1),
            new RoutedContentDocument(document2, route2),
            new RoutedContentDocument(document3, route3)
        };

        var result = SearchIndexBuilder.BuildDocumentMap(input);

        Assert.Equal(3, result.Count);
        Assert.Equal(document1, result["blog/alpha/index.html"]);
        Assert.Equal(document2, result["pages/beta/index.html"]);
        Assert.Equal(document3, result["en/gamma/index.html"]);
    }

    [Fact]
    public void BuildDocumentMap_OutputPathWithBackslashes_NormalizesToForwardSlashes()
    {
        var document = ContentDocument.Create("x", "X", "x", DateTimeOffset.UtcNow, null, ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));
        var route = new RouteInfo("/pages/x/", "pages\\x\\index.html", "pages/page.html");

        var result = SearchIndexBuilder.BuildDocumentMap([new RoutedContentDocument(document, route)]);

        Assert.True(result.ContainsKey("pages/x/index.html"));
    }

    [Fact]
    public void WriteSearchItem_EmitsCanonicalContentMetadata()
    {
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
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
        });
        var document = ContentDocumentNormalizer.ToDocument(new RawContentDocument(
            Id: "search-1",
            Title: "Search Post",
            Slug: "search-post",
            PublishAt: DateTimeOffset.Parse("2026-06-05T12:00:00Z"),
            Body: new RawBody(InlineHtml: "<p>Body</p>"),
            Properties: RawContentValue.FromFields(fields),
            CustomFields: fields));
        var route = new RouteInfo("/search-post/", "search-post/index.html", "pages/post.html");

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            SearchIndexBuilder.WriteSearchItem(writer, document, route, "/", new DictionaryContentBodyStore(new Dictionary<string, ContentBody>
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
        var fields = ContentFieldReader.WithValues(new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "guide"),
            ["summary"] = new("text", "Canonical summary"),
            ["language"] = new("text", "ms-MY"),
            ["source"] = new("text", "notion"),
            ["tags"] = new("list", new object[] { "bukit", "canonical" }),
            ["categories"] = new("list", new object[] { "docs" })
        }, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = "legacy-source"
        });
        var document = ContentDocumentNormalizer.ToDocument(new RawContentDocument(
            Id: "search-2",
            Title: "Structured Post",
            Slug: "structured-post",
            PublishAt: DateTimeOffset.Parse("2026-06-05T12:00:00Z"),
            Body: new RawBody(InlineHtml: "<p>Structured body</p>"),
            Properties: RawContentValue.FromFields(fields),
            CustomFields: fields));
        var route = new RouteInfo("/structured-post/", "structured-post/index.html", "pages/post.html");

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            SearchIndexBuilder.WriteSearchItem(writer, document, route, "/", new DictionaryContentBodyStore(new Dictionary<string, ContentBody>
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
    public void GenerateSingleSearchIndex_WithGraphOnlyListRoute_IncludesListRoute()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-search-list-route-" + Guid.NewGuid().ToString("N"));
        try
        {
            var route = CreateGraphOnlyListRoute();
            var graph = ListRouteGraph.Create(new[] { route });
            var routeInfo = route.ToRouteInfo();
            var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [route.OutputPath] = new(routeInfo, "https://example.com/companies/malaysia/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), null, "list", IsDerived: true)
            };
            var seoModels = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
            {
                [route.OutputPath] = new()
                {
                    Title = "Malaysia Companies",
                    Description = "Companies operating in Malaysia",
                    Canonical = "https://example.com/companies/malaysia/"
                }
            };

            SearchIndexBuilder.GenerateSingleSearchIndex(
                tempDir,
                "/",
                includeDerived: false,
                emitSnippet: true,
                routed: Array.Empty<RoutedContentDocument>(),
                derivedRouted: Array.Empty<RoutedContentDocument>(),
                seoIndex,
                NullContentBodyStore.Instance,
                graph,
                seoModels);

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempDir, "search.json")));
            var item = Assert.Single(doc.RootElement.EnumerateArray());
            Assert.Equal("filter:companies:country:malaysia:1", item.GetProperty("id").GetString());
            Assert.Equal("Malaysia Companies", item.GetProperty("title").GetString());
            Assert.Equal("/companies/malaysia/", item.GetProperty("url").GetString());
            Assert.Equal("filter", item.GetProperty("type").GetString());
            Assert.Contains("Acme Malaysia", item.GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.Equal("Companies operating in Malaysia", item.GetProperty("snippet").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void GenerateMergedSearchIndex_WithGraphOnlyListRoute_IncludesListRoute()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-search-merged-list-route-" + Guid.NewGuid().ToString("N"));
        try
        {
            var route = CreateGraphOnlyListRoute();
            var routeInfo = route.ToRouteInfo();
            var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [route.OutputPath] = new(routeInfo, "https://example.com/en/companies/malaysia/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), null, "list", IsDerived: true)
            };
            var seoModels = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
            {
                [route.OutputPath] = new()
                {
                    Title = "Malaysia Companies",
                    Description = "Companies operating in Malaysia",
                    Canonical = "https://example.com/en/companies/malaysia/"
                }
            };
            var result = new BuildVariantResult(
                Language: "en",
                OutputDir: tempDir,
                BaseUrl: "/en",
                SearchSnippetsEnabled: true,
                BodyStore: NullContentBodyStore.Instance,
                DerivedRoutes: Array.Empty<(RouteInfo, DateTimeOffset)>(),
                SeoIndex: seoIndex,
                SeoModels: seoModels,
                PluginExecutions: Array.Empty<PluginExecutionInfo>(),
                RenderedCount: 0,
                SkippedCount: 0,
                RenderReasons: new Dictionary<string, int>(),
                StageMetrics: BuildStageMetrics.Empty,
                RoutedDocuments: Array.Empty<RoutedContentDocument>(),
                ListRouteGraph: ListRouteGraph.Create(new[] { route }));

            SearchIndexBuilder.GenerateMergedSearchIndex(tempDir, new[] { result }, includeDerived: false);

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempDir, "search.json")));
            var item = Assert.Single(doc.RootElement.EnumerateArray());
            Assert.Equal("Malaysia Companies", item.GetProperty("title").GetString());
            Assert.Equal("/en/companies/malaysia/", item.GetProperty("url").GetString());
            Assert.Contains("Acme Malaysia", item.GetProperty("content").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static ListRoutePlan CreateGraphOnlyListRoute()
        => new()
        {
            RouteId = "filter:companies:country:malaysia:1",
            Kind = ListRouteKind.FilteredListPage,
            Url = "/companies/malaysia/",
            OutputPath = "companies/malaysia/index.html",
            Template = "pages/company-list.html",
            Collection = "companies",
            PageNumber = 1,
            PageSize = 10,
            TotalItems = 1,
            Items = new[]
            {
                new ListRouteItem
                {
                    Id = "company-1",
                    Title = "Acme Malaysia",
                    Url = "/companies/acme-malaysia/",
                    Summary = "Malaysia logistics company"
                }
            },
            CanonicalUrl = "/companies/malaysia/",
            FilterContext = new ListRouteFilterContext
            {
                Field = "country",
                Value = "Malaysia"
            }
        };
}
