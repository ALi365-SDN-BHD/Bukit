using Xunit;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;

namespace Bukit.Engine.Tests;

/// <summary>
/// Tests for TaxonomyPageCreator URL building, escaping, and page generation.
/// </summary>
public sealed class TaxonomyPageCreatorTests
{
    // ── EscapeHtml / EscapeAttr ─────────────────────────────────────

    [Fact]
    public void EscapeHtml_EncodesSpecialChars()
    {
        var result = TaxonomyPageCreator.EscapeHtml("<a href=\"x\">&'");
        Assert.Equal("&lt;a href=&quot;x&quot;&gt;&amp;&#39;", result);
    }

    [Fact]
    public void EscapeHtml_PlainText_Unchanged()
    {
        Assert.Equal("hello", TaxonomyPageCreator.EscapeHtml("hello"));
    }

    [Fact]
    public void EscapeAttr_SameAsEscapeHtml()
    {
        Assert.Equal(TaxonomyPageCreator.EscapeHtml("<b>"), TaxonomyPageCreator.EscapeAttr("<b>"));
    }

    // ── NormalizeRoutePrefix ────────────────────────────────────────

    [Theory]
    [InlineData("tags", null, "/tags")]
    [InlineData("tags", "topics", "/topics")]
    [InlineData("tags", "/topics/", "/topics")]
    [InlineData("tags", "", "/tags")]
    [InlineData("tags", "/", "/")]
    [InlineData("categories", " /cats ", "/cats")]
    public void NormalizeRoutePrefix_VariousInputs(string kind, string? routePrefix, string expected)
    {
        Assert.Equal(expected, TaxonomyPageCreator.NormalizeRoutePrefix(kind, routePrefix));
    }

    // ── BuildIndexUrl ───────────────────────────────────────────────

    [Theory]
    [InlineData("/tags", "/tags/")]
    [InlineData("/", "/")]
    public void BuildIndexUrl_VariousPrefixes(string prefix, string expected)
    {
        Assert.Equal(expected, TaxonomyPageCreator.BuildIndexUrl(prefix));
    }

    // ── BuildTermUrl ────────────────────────────────────────────────

    [Theory]
    [InlineData("/tags", "news", "/tags/news/")]
    [InlineData("/", "news", "/news/")]
    public void BuildTermUrl_VariousPrefixes(string prefix, string slug, string expected)
    {
        Assert.Equal(expected, TaxonomyPageCreator.BuildTermUrl(prefix, slug));
    }

    // ── BuildTermPageUrl ────────────────────────────────────────────

    [Theory]
    [InlineData("/tags", "news", 2, "/tags/news/page/2/")]
    [InlineData("/", "news", 3, "/news/page/3/")]
    public void BuildTermPageUrl_VariousPrefixes(string prefix, string slug, int page, string expected)
    {
        Assert.Equal(expected, TaxonomyPageCreator.BuildTermPageUrl(prefix, slug, page));
    }

    // ── CreateIndexPage ─────────────────────────────────────────────

    [Fact]
    public void CreateIndexPage_GeneratesRoutedDocument()
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["news"] = new("News", "news")
            {
                Weight = 1,
                Pages = [MakeTaxonomyPage("p1", "Post 1", "/p1/")]
            }
        };

        var routed = TaxonomyPageCreator.CreateIndexPage(
            "", "tags", "/tags", "Tags", "All tags", terms.Values.ToList(),
            new Dictionary<string, TaxonomyHierarchyBuilder.HierarchyInfo>(), "index.html",
            DateTimeOffset.UtcNow, emitContentHtml: true, null, "as-is");

        Assert.Equal("/tags/", routed.Route.Url);
        Assert.Equal("Tags", routed.Document.Title);
        Assert.Equal("derived", ContentFieldReader.GetText(routed.Document.CustomFields, "type"));
        Assert.Contains("News", routed.Document.Body.Html);
    }

    [Fact]
    public void CreateIndexPage_NoEmitHtml_BodyEmpty()
    {
        var routed = TaxonomyPageCreator.CreateIndexPage(
            "", "tags", "/tags", "Tags", null, [],
            new Dictionary<string, TaxonomyHierarchyBuilder.HierarchyInfo>(), "index.html",
            DateTimeOffset.UtcNow, emitContentHtml: false, null, "as-is");

        Assert.Equal(string.Empty, routed.Document.Body.Html);
    }

    // ── CreateTermPage ──────────────────────────────────────────────

    [Fact]
    public void CreateTermPage_FirstPage_UsesTermUrl()
    {
        var term = new TaxonomyTerm("News", "news")
        {
            Pages = [MakeTaxonomyPage("p1", "Post 1", "/p1/")]
        };

        var routed = TaxonomyPageCreator.CreateTermPage(
            "", "tags", "/tags", "Tag", term, null, "term.html",
            DateTimeOffset.UtcNow, emitContentHtml: false, pageSize: 10, page: 1, totalPages: 1,
            [MakeTaxonomyPage("p1", "Post 1", "/p1/")], null, "as-is");

        Assert.Equal("/tags/news/", routed.Route.Url);
        Assert.NotNull(routed.Document.CustomFields);
        Assert.True(routed.Document.CustomFields!.TryGetValue("items", out var items));
    }

    [Fact]
    public void CreateTermPage_PageTwo_UsesPagedUrl()
    {
        var term = new TaxonomyTerm("News", "news")
        {
            Pages = [MakeTaxonomyPage("p1", "Post 1", "/p1/")]
        };

        var routed = TaxonomyPageCreator.CreateTermPage(
            "", "tags", "/tags", "Tag", term, null, "term.html",
            DateTimeOffset.UtcNow, emitContentHtml: false, pageSize: 1, page: 2, totalPages: 2,
            [MakeTaxonomyPage("p2", "Post 2", "/p2/")], null, "as-is");

        Assert.Equal("/tags/news/page/2/", routed.Route.Url);
        Assert.NotNull(routed.Document.CustomFields);
        Assert.True(routed.Document.CustomFields!.TryGetValue("pagination", out var pagination));
    }

    [Fact]
    public void CreateTermPage_WithExtraFields_SetsFieldsValue()
    {
        var term = new TaxonomyTerm("News", "news")
        {
            Pages = [MakeTaxonomyPage("p1", "Post 1", "/p1/", extra: new Dictionary<string, object> { ["author"] = "Jane" })]
        };

        var routed = TaxonomyPageCreator.CreateTermPage(
            "", "tags", "/tags", "Tag", term, null, "term.html",
            DateTimeOffset.UtcNow, emitContentHtml: false, pageSize: 10, page: 1, totalPages: 1,
            [MakeTaxonomyPage("p1", "Post 1", "/p1/", extra: new Dictionary<string, object> { ["author"] = "Jane" })], null, "as-is");

        Assert.NotNull(routed.Document.CustomFields);
        Assert.True(routed.Document.CustomFields!.TryGetValue("items", out var items));
    }

    // ── CreateKind ──────────────────────────────────────────────────

    [Fact]
    public void CreateKind_WithIndexEnabled_GeneratesIndexAndTermPages()
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["news"] = new("News", "news")
            {
                Pages =
                [
                    MakeTaxonomyPage("p1", "Post 1", "/p1/"),
                    MakeTaxonomyPage("p2", "Post 2", "/p2/")
                ]
            }
        };

        var derived = TaxonomyPageCreator.CreateKind(
            "", "tags", null, "Tags", null, "Tag", terms, "index.html", "term.html",
            emitContentHtml: false, pageSize: 1, indexEnabled: true, hierarchical: false,
            null, "as-is");

        // index page + 2 term pages (pageSize=1)
        Assert.Equal(3, derived.Count);
        Assert.Contains(derived, d => d.Route.Url == "/tags/");
    }

    [Fact]
    public void CreateKind_IndexDisabled_NoIndexPage()
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["news"] = new("News", "news")
            {
                Pages = [MakeTaxonomyPage("p1", "Post 1", "/p1/")]
            }
        };

        var derived = TaxonomyPageCreator.CreateKind(
            "", "tags", null, "Tags", null, "Tag", terms, "index.html", "term.html",
            emitContentHtml: false, pageSize: 10, indexEnabled: false, hierarchical: false,
            null, "as-is");

        Assert.DoesNotContain(derived, d => d.Route.Url == "/tags/");
        Assert.Single(derived);
    }

    [Fact]
    public void CreateKind_EmptyTerm_GeneratesPage()
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["empty"] = new("Empty", "empty")
        };

        var derived = TaxonomyPageCreator.CreateKind(
            "", "tags", null, "Tags", null, "Tag", terms, "index.html", "term.html",
            emitContentHtml: false, pageSize: 10, indexEnabled: false, hierarchical: false,
            null, "as-is");

        Assert.Single(derived);
        Assert.Equal("/tags/empty/", derived[0].Route.Url);
    }

    private static TaxonomyPage MakeTaxonomyPage(string id, string title, string url, IReadOnlyDictionary<string, object>? extra = null)
        => new(
            Id: id,
            Title: title,
            Url: url,
            PublishAt: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Summary: null,
            Extra: extra,
            IsPinned: false,
            PinOrder: null);
}
