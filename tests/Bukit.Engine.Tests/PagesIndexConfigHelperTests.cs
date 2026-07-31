using Xunit;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Content;

namespace Bukit.Engine.Tests;

/// <summary>
/// Tests for PagesIndexConfigHelper configuration extraction and relation collection.
/// </summary>
public sealed class PagesIndexConfigHelperTests
{
    // ── TryGetMap ───────────────────────────────────────────────────

    [Fact]
    public void TryGetMap_ExistingKey_ReturnsMap()
    {
        var map = new Dictionary<string, object> { ["nested"] = new Dictionary<string, object> { ["a"] = 1 } };
        var result = PagesIndexConfigHelper.TryGetMap(map, "nested", out var value);
        Assert.True(result);
        Assert.Equal(1, value["a"]);
    }

    [Fact]
    public void TryGetMap_MissingKey_ReturnsFalse()
    {
        var map = new Dictionary<string, object>();
        var result = PagesIndexConfigHelper.TryGetMap(map, "missing", out _);
        Assert.False(result);
    }

    [Fact]
    public void TryGetMap_WrongType_ReturnsFalse()
    {
        var map = new Dictionary<string, object> { ["key"] = "not-a-map" };
        var result = PagesIndexConfigHelper.TryGetMap(map, "key", out _);
        Assert.False(result);
    }

    // ── TryGetString ────────────────────────────────────────────────

    [Fact]
    public void TryGetString_ExistingKey_ReturnsValue()
    {
        var map = new Dictionary<string, object> { ["name"] = "blog" };
        Assert.Equal("blog", PagesIndexConfigHelper.TryGetString(map, "name"));
    }

    [Fact]
    public void TryGetString_MissingKey_ReturnsNull()
    {
        var map = new Dictionary<string, object>();
        Assert.Null(PagesIndexConfigHelper.TryGetString(map, "name"));
    }

    // ── TryGetBool ──────────────────────────────────────────────────

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void TryGetBool_VariousValues(object value, bool expected)
    {
        var map = new Dictionary<string, object> { ["flag"] = value };
        Assert.Equal(expected, PagesIndexConfigHelper.TryGetBool(map, "flag", false));
    }

    [Fact]
    public void TryGetBool_MissingKey_ReturnsDefault()
    {
        var map = new Dictionary<string, object>();
        Assert.True(PagesIndexConfigHelper.TryGetBool(map, "flag", true));
    }

    // ── TryGetInt ───────────────────────────────────────────────────

    [Fact]
    public void TryGetInt_IntValue_ReturnsValue()
    {
        var map = new Dictionary<string, object> { ["count"] = 42 };
        Assert.Equal(42, PagesIndexConfigHelper.TryGetInt(map, "count", 0));
    }

    [Fact]
    public void TryGetInt_MissingKey_ReturnsDefault()
    {
        var map = new Dictionary<string, object>();
        Assert.Equal(7, PagesIndexConfigHelper.TryGetInt(map, "count", 7));
    }

    // ── TryGetNullableInt ───────────────────────────────────────────

    [Fact]
    public void TryGetNullableInt_IntValue_ReturnsValue()
    {
        var map = new Dictionary<string, object> { ["count"] = 42 };
        Assert.Equal(42, PagesIndexConfigHelper.TryGetNullableInt(map, "count"));
    }

    [Fact]
    public void TryGetNullableInt_MissingKey_ReturnsNull()
    {
        var map = new Dictionary<string, object>();
        Assert.Null(PagesIndexConfigHelper.TryGetNullableInt(map, "count"));
    }

    // ── TryGetStringList ────────────────────────────────────────────

    [Fact]
    public void TryGetStringList_ListValue_ReturnsList()
    {
        var map = new Dictionary<string, object> { ["items"] = new List<string> { "a", "b" } };
        var result = PagesIndexConfigHelper.TryGetStringList(map, "items");
        Assert.Equal(["a", "b"], result);
    }

    [Fact]
    public void TryGetStringList_MissingKey_ReturnsEmpty()
    {
        var map = new Dictionary<string, object>();
        Assert.Empty(PagesIndexConfigHelper.TryGetStringList(map, "items"));
    }

    // ── HasNotionContent ────────────────────────────────────────────

    [Fact]
    public void HasNotionContent_NoSources_ReturnsFalse()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "t", Title = "T" },
            Content = ContentConfigFactory.FromSources([])
        };
        Assert.False(PagesIndexConfigHelper.HasNotionContent(config));
    }

    [Fact]
    public void HasNotionContent_WithNotionSource_ReturnsTrue()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "t", Title = "T" },
            Content = ContentConfigFactory.FromSources(
            [
                new ContentSourceConfig { Type = "notion", Name = "n", Notion = new NotionConfig { DatabaseId = "db1" } }
            ])
        };
        Assert.True(PagesIndexConfigHelper.HasNotionContent(config));
    }

    [Fact]
    public void HasNotionContent_OnlyMarkdown_ReturnsFalse()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "t", Title = "T" },
            Content = ContentConfigFactory.FromSources(
            [
                new ContentSourceConfig { Type = "markdown", Name = "m", Markdown = new MarkdownConfig { Dir = "content" } }
            ])
        };
        Assert.False(PagesIndexConfigHelper.HasNotionContent(config));
    }

    // ── BuildKnownRawIdSet ──────────────────────────────────────────

    [Fact]
    public void BuildKnownRawIdSet_ExtractsRawIdsFromKeys()
    {
        var index = new Dictionary<string, object>
        {
            ["db1:page1"] = new { },
            ["page2"] = new { }
        };
        var result = PagesIndexConfigHelper.BuildKnownRawIdSet(index);
        Assert.Contains("db1:page1", result);
        Assert.Contains("page1", result);
        Assert.Contains("page2", result);
    }

    // ── CollectRelationIds ──────────────────────────────────────────

    [Fact]
    public void CollectRelationIds_CollectsUnknownIds()
    {
        var doc = ContentDocument.Create(
            "p1", "P1", "p1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField>
            {
                ["related"] = new("list", new List<string> { "known", "unknown1", "unknown2" })
            });
        var route = new RouteInfo("/p1/", "p1/index.html", "page.html");
        var routed = new[] { new RoutedContentDocument(doc, route) };
        var index = new Dictionary<string, object> { ["known"] = new { } };

        var result = PagesIndexConfigHelper.CollectRelationIds(routed, ["related"], index, maxItems: 10);

        Assert.Equal(["unknown1", "unknown2"], result);
    }

    [Fact]
    public void CollectRelationIds_RespectsMaxItems()
    {
        var doc = ContentDocument.Create(
            "p1", "P1", "p1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField>
            {
                ["related"] = new("list", new List<string> { "a", "b", "c" })
            });
        var route = new RouteInfo("/p1/", "p1/index.html", "page.html");
        var routed = new[] { new RoutedContentDocument(doc, route) };

        var result = PagesIndexConfigHelper.CollectRelationIds(routed, ["related"], new Dictionary<string, object>(), maxItems: 2);

        Assert.Equal(["a", "b"], result);
    }

    [Fact]
    public void CollectRelationIds_NoFields_ReturnsEmpty()
    {
        var doc = ContentDocument.Create("p1", "P1", "p1", DateTimeOffset.UtcNow, null, null);
        var route = new RouteInfo("/p1/", "p1/index.html", "page.html");
        var routed = new[] { new RoutedContentDocument(doc, route) };

        var result = PagesIndexConfigHelper.CollectRelationIds(routed, ["related"], new Dictionary<string, object>(), maxItems: 10);

        Assert.Empty(result);
    }

    [Fact]
    public void CollectRelationIds_EmptyFieldKeys_ReturnsEmpty()
    {
        var doc = ContentDocument.Create("p1", "P1", "p1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["related"] = new("list", new List<string> { "a" }) });
        var route = new RouteInfo("/p1/", "p1/index.html", "page.html");
        var routed = new[] { new RoutedContentDocument(doc, route) };

        var result = PagesIndexConfigHelper.CollectRelationIds(routed, [], new Dictionary<string, object>(), maxItems: 10);

        Assert.Empty(result);
    }
}
