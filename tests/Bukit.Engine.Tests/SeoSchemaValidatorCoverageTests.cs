using Xunit;
using System.Text.Json;

namespace Bukit.Engine.Tests;

/// <summary>
/// Extended tests for SeoSchemaValidator JSON-LD extraction and validation.
/// </summary>
public sealed class SeoSchemaValidatorCoverageTests
{
    // ── IsSupportedArticleAuthorType ────────────────────────────────

    [Theory]
    [InlineData("Person", true)]
    [InlineData("Organization", true)]
    [InlineData("person", false)] // case-sensitive
    [InlineData("organization", false)]
    [InlineData("Thing", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsSupportedArticleAuthorType_VariousInputs(string? type, bool expected)
    {
        var result = SeoSchemaValidator.IsSupportedArticleAuthorType(type);
        Assert.Equal(expected, result);
    }

    // ── IsSupportedProfileAuthorType ────────────────────────────────

    [Theory]
    [InlineData("Person", true)]
    [InlineData("Organization", true)]
    [InlineData("person", true)] // case-insensitive
    [InlineData("ORGANIZATION", true)]
    [InlineData("Thing", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsSupportedProfileAuthorType_VariousInputs(string? type, bool expected)
    {
        var result = SeoSchemaValidator.IsSupportedProfileAuthorType(type);
        Assert.Equal(expected, result);
    }

    // ── ExtractSchemaTypes ──────────────────────────────────────────

    [Fact]
    public void ExtractSchemaTypes_SingleType_ExtractsCorrectly()
    {
        var jsonLd = new List<string>
        {
            """{"@context": "https://schema.org", "@type": "WebSite", "name": "Test"}"""
        };
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Single(types);
        Assert.Contains("WebSite", types);
    }

    [Fact]
    public void ExtractSchemaTypes_MultipleTypes_ExtractsAll()
    {
        var jsonLd = new List<string>
        {
            """{"@type": "WebSite"}""",
            """{"@type": "Organization"}"""
        };
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Equal(2, types.Count);
        Assert.Contains("WebSite", types);
        Assert.Contains("Organization", types);
    }

    [Fact]
    public void ExtractSchemaTypes_ArrayType_ExtractsAll()
    {
        var jsonLd = new List<string>
        {
            """{"@type": ["WebSite", "Organization"]}"""
        };
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Equal(2, types.Count);
        Assert.Contains("WebSite", types);
        Assert.Contains("Organization", types);
    }

    [Fact]
    public void ExtractSchemaTypes_NoType_AddsWarning()
    {
        var jsonLd = new List<string>
        {
            """{"name": "Test"}"""
        };
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Empty(types);
        Assert.Single(issues);
        Assert.Equal("seo.json_ld_type_missing", issues[0].Code);
    }

    [Fact]
    public void ExtractSchemaTypes_InvalidJson_AddsError()
    {
        var jsonLd = new List<string> { "not valid json" };
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Empty(types);
        Assert.Single(issues);
        Assert.Equal("seo.json_ld_invalid", issues[0].Code);
    }

    [Fact]
    public void ExtractSchemaTypes_EmptyList_ReturnsEmpty()
    {
        var jsonLd = new List<string>();
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Empty(types);
        Assert.Empty(issues);
    }

    [Fact]
    public void ExtractSchemaTypes_NestedTypes_ExtractsFromObjects()
    {
        var jsonLd = new List<string>
        {
            """{"@type": "WebPage", "mainEntity": {"@type": "Article"}}"""
        };
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Equal(2, types.Count);
        Assert.Contains("WebPage", types);
        Assert.Contains("Article", types);
    }

    [Fact]
    public void ExtractSchemaTypes_ArrayOfObjects_ExtractsAll()
    {
        var jsonLd = new List<string>
        {
            """[{"@type": "WebSite"}, {"@type": "Organization"}]"""
        };
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Equal(2, types.Count);
    }

    [Fact]
    public void ExtractSchemaTypes_EmptyTypeString_Ignored()
    {
        var jsonLd = new List<string>
        {
            """{"@type": ""}"""
        };
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Empty(types);
    }

    [Fact]
    public void ExtractSchemaTypes_NonStringType_Ignored()
    {
        var jsonLd = new List<string>
        {
            """{"@type": 123}"""
        };
        var issues = new List<SeoAuditIssue>();
        var types = SeoSchemaValidator.ExtractSchemaTypes(jsonLd, "/test/", issues);

        Assert.Empty(types);
    }

    // ── ValidateSchemaObject: WebSite validation ────────────────────

    [Fact]
    public void ValidateSchemaObject_WebSite_MissingName_AddsWarning()
    {
        var json = """{"@context": "https://schema.org", "@type": "WebSite", "url": "https://example.com"}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_website_name_missing");
    }

    [Fact]
    public void ValidateSchemaObject_WebSite_MissingUrl_AddsWarning()
    {
        var json = """{"@context": "https://schema.org", "@type": "WebSite", "name": "Test"}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_website_url_invalid");
    }

    [Fact]
    public void ValidateSchemaObject_WebSite_WithSearchAction_NoWarning()
    {
        var json = """{"@type": "WebSite", "name": "Test", "url": "https://example.com", "potentialAction": {"@type": "SearchAction", "target": "https://example.com/search?q={query}", "query-input": "required name=query"}}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues, searchActionExpected: true);
        Assert.DoesNotContain(issues, i => i.Code.Contains("searchaction"));
    }

    [Fact]
    public void ValidateSchemaObject_WebSite_MissingSearchAction_WhenExpected()
    {
        var json = """{"@context": "https://schema.org", "@type": "WebSite", "name": "Test", "url": "https://example.com"}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues, searchActionExpected: true);
        Assert.Contains(issues, i => i.Code == "seo.schema_website_searchaction_missing");
    }

    [Fact]
    public void ValidateSchemaObject_WebSite_SearchActionMissingTarget()
    {
        var json = """{"@type": "WebSite", "name": "Test", "url": "https://example.com", "potentialAction": {"@type": "SearchAction"}}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_searchaction_target_missing");
    }

    [Fact]
    public void ValidateSchemaObject_WebSite_SearchActionRelativeTarget()
    {
        var json = """{"@type": "WebSite", "name": "Test", "url": "https://example.com", "potentialAction": {"@type": "SearchAction", "target": "/search?q={query}", "query-input": "required name=query"}}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_searchaction_target_not_absolute");
    }

    [Fact]
    public void ValidateSchemaObject_WebSite_SearchActionArrayWithSearchAction()
    {
        var json = """{"@type": "WebSite", "name": "Test", "url": "https://example.com", "potentialAction": [{"@type": "SearchAction", "target": "https://example.com/search?q={query}", "query-input": "required name=query"}]}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.DoesNotContain(issues, i => i.Code.Contains("searchaction_missing"));
    }

    [Fact]
    public void ValidateSchemaObject_WebSite_SearchActionArrayWithoutSearchAction()
    {
        var json = """{"@type": "WebSite", "name": "Test", "url": "https://example.com", "potentialAction": [{"@type": "ViewAction"}]}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_searchaction_missing");
    }

    [Fact]
    public void ValidateSchemaObject_WebSite_SearchActionInvalidType()
    {
        var json = """{"@type": "WebSite", "name": "Test", "url": "https://example.com", "potentialAction": "not an object"}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_searchaction_invalid");
    }

    // ── ValidateSchemaObject: Article author validation ─────────────

    [Fact]
    public void ValidateSchemaObject_Article_WithPersonAuthor_NoError()
    {
        var json = """{"@type": "Article", "author": {"@type": "Person", "name": "John"}}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.DoesNotContain(issues, i => i.Code.Contains("author"));
    }

    [Fact]
    public void ValidateSchemaObject_Article_AuthorStringOnly_WarnsTypeMissing()
    {
        var json = """{"@type": "Article", "author": "John"}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code.Contains("author_type_missing"));
    }

    [Fact]
    public void ValidateSchemaObject_Article_AuthorEmptyString_ReportedAsMissing()
    {
        var json = """{"@type": "Article", "headline": "Test", "datePublished": "2024-01-01", "author": "", "image": "https://example.com/img.jpg"}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_article_author_missing");
    }

    [Fact]
    public void ValidateSchemaObject_Article_AuthorArray()
    {
        var json = """{"@type": "Article", "author": [{"@type": "Person", "name": "John"}, {"@type": "Organization", "name": "Acme"}]}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.DoesNotContain(issues, i => i.Code.Contains("author_type_invalid"));
    }

    [Fact]
    public void ValidateSchemaObject_Article_AuthorInvalidType()
    {
        var json = """{"@type": "Article", "author": 123}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code.Contains("author_type_invalid"));
    }

    [Fact]
    public void ValidateSchemaObject_Article_AuthorUnsupportedType()
    {
        var json = """{"@type": "Article", "author": {"@type": "Thing", "name": "Test"}}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code.Contains("author_type_invalid"));
    }

    [Fact]
    public void ValidateSchemaObject_Article_AuthorMissingName()
    {
        var json = """{"@type": "Article", "author": {"@type": "Person"}}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code.Contains("author_name_missing"));
    }

    // ── ValidateSchemaObject: ItemList validation ───────────────────

    [Fact]
    public void ValidateSchemaObject_ItemList_ValidItems()
    {
        var json = """{"@type": "ItemList", "itemListElement": [{"@type": "ListItem", "position": 1, "name": "First", "url": "https://example.com/first"}]}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.DoesNotContain(issues, i => i.Code.Contains("itemlist"));
    }

    [Fact]
    public void ValidateSchemaObject_ItemList_EmptyElements()
    {
        var json = """{"@type": "ItemList", "itemListElement": []}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_itemlist_elements_missing");
    }

    [Fact]
    public void ValidateSchemaObject_ItemList_NonObjectItem()
    {
        var json = """{"@type": "ItemList", "itemListElement": ["string"]}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_itemlist_item_invalid");
    }

    [Fact]
    public void ValidateSchemaObject_ItemList_MissingPosition()
    {
        var json = """{"@type": "ItemList", "itemListElement": [{"@type": "ListItem", "name": "First"}]}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_itemlist_position_missing");
    }

    [Fact]
    public void ValidateSchemaObject_ItemList_MissingElements()
    {
        var json = """{"@type": "ItemList"}""";
        using var doc = JsonDocument.Parse(json);
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ValidateSchemaObject(doc.RootElement, "/test/", issues);
        Assert.Contains(issues, i => i.Code == "seo.schema_itemlist_elements_missing");
    }

    // ── NewsArticle publisher audit ────────────────────────────

    [Fact]
    public void ExtractSchemaTypes_NewsArticleWithoutPublisher_Warns()
    {
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ExtractSchemaTypes(
            ["""{"@type":"NewsArticle","headline":"News","datePublished":"2026-08-05T00:00:00Z","author":{"@type":"Person","name":"Desk"},"image":"https://example.com/news.jpg"}"""],
            "/news/",
            issues);

        Assert.Contains(issues, issue =>
            issue.Code == "seo.schema_newsarticle_publisher_missing" &&
            issue.Severity == "warning");
    }

    [Theory]
    [InlineData("Organization")]
    [InlineData("NewsMediaOrganization")]
    public void ExtractSchemaTypes_NewsArticleWithSupportedPublisher_DoesNotWarn(string publisherType)
    {
        var json = $$$"""{"@type":"NewsArticle","headline":"News","datePublished":"2026-08-05T00:00:00Z","author":{"@type":"Person","name":"Desk"},"image":"https://example.com/news.jpg","publisher":{"@type":"{{{publisherType}}}","name":"Example News"}}""";
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ExtractSchemaTypes([json], "/news/", issues);

        Assert.DoesNotContain(issues,
            issue => issue.Code.Contains("publisher", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtractSchemaTypes_NewsArticleWithPersonPublisher_WarnsTypeInvalid()
    {
        var json = """{"@type":"NewsArticle","headline":"News","datePublished":"2026-08-05T00:00:00Z","author":{"@type":"Person","name":"Desk"},"image":"https://example.com/news.jpg","publisher":{"@type":"Person","name":"Desk"}}""";
        var issues = new List<SeoAuditIssue>();
        SeoSchemaValidator.ExtractSchemaTypes([json], "/news/", issues);

        Assert.Contains(issues, issue =>
            issue.Code == "seo.schema_newsarticle_publisher_type_invalid" &&
            issue.Severity == "warning");
    }
}
