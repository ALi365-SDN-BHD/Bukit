using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionTaxonomyPromoterTests
{
    [Fact]
    public void PromoteRelationTaxonomyTerms_WithLinksField_PromotesTermsToMeta()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["title"] = "Docs",
                    ["slug"] = "docs",
                    ["id"] = "page-1"
                },
                new()
                {
                    ["title"] = "Release",
                    ["slug"] = "release",
                    ["id"] = "page-2"
                }
            })
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        Assert.True(meta.ContainsKey("tags"));
        var terms = Assert.IsType<List<string>>(meta["tags"]);
        Assert.Equal(new[] { "Docs", "Release" }, terms);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_UsesTitleFirst_ThenSlug_ThenId()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "T1", ["slug"] = "s1", ["id"] = "i1" },
                new() { ["slug"] = "s2", ["id"] = "i2" },
                new() { ["id"] = "i3" }
            })
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        var terms = Assert.IsType<List<string>>(meta["tags"]);
        Assert.Equal(new[] { "T1", "s2", "i3" }, terms);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_DeduplicatesByCaseInsensitive()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "Docs" },
                new() { ["title"] = "docs" },
                new() { ["title"] = "DOCS" }
            })
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        var terms = Assert.IsType<List<string>>(meta["tags"]);
        Assert.Single(terms);
        Assert.Equal("Docs", terms[0]);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_TrimsWhitespace()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "  Padded  " }
            })
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        var terms = Assert.IsType<List<string>>(meta["tags"]);
        Assert.Single(terms);
        Assert.Equal("Padded", terms[0]);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WhenLinksFieldMissing_DoesNothing()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        Assert.False(meta.ContainsKey("tags"));
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WhenLinksFieldValueNull_DoesNothing()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", null!)
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        Assert.False(meta.ContainsKey("tags"));
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WithEmptyLinks_DoesNothing()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>())
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        Assert.False(meta.ContainsKey("tags"));
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WithNullLinkEntries_SkipsThem()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                null!,
                new() { ["title"] = "Valid" }
            })
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        var terms = Assert.IsType<List<string>>(meta["tags"]);
        Assert.Single(terms);
        Assert.Equal("Valid", terms[0]);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WithEmptyTrimmedTerms_SkipsThem()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "   " },
                new() { ["slug"] = "" },
                new() { ["id"] = "real" }
            })
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        var terms = Assert.IsType<List<string>>(meta["tags"]);
        Assert.Single(terms);
        Assert.Equal("real", terms[0]);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WhenLinksValueNotEnumerable_DoesNothing()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("text", "not-a-list")
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        Assert.False(meta.ContainsKey("tags"));
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WithCategoriesKey_UsesCorrectLinkKey()
    {
        var meta = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["categories_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "Tech" },
                new() { ["title"] = "Life" }
            })
        };

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "categories");

        Assert.True(meta.ContainsKey("categories"));
        var terms = Assert.IsType<List<string>>(meta["categories"]);
        Assert.Equal(new[] { "Tech", "Life" }, terms);
    }
}
