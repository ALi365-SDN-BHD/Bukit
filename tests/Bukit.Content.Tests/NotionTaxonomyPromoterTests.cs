using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionTaxonomyPromoterTests
{
    [Fact]
    public void PromoteRelationTaxonomyTerms_WithLinksField_PromotesTermsToMeta()
    {
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

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        Assert.True(result.ContainsKey("tags"));
        var terms = Assert.IsType<List<string>>(result["tags"].Value);
        Assert.Equal(new[] { "Docs", "Release" }, terms);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_UsesTitleFirst_ThenSlug_ThenId()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "T1", ["slug"] = "s1", ["id"] = "i1" },
                new() { ["slug"] = "s2", ["id"] = "i2" },
                new() { ["id"] = "i3" }
            })
        };

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        var terms = Assert.IsType<List<string>>(result["tags"].Value);
        Assert.Equal(new[] { "T1", "s2", "i3" }, terms);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_DeduplicatesByCaseInsensitive()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "Docs" },
                new() { ["title"] = "docs" },
                new() { ["title"] = "DOCS" }
            })
        };

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        var terms = Assert.IsType<List<string>>(result["tags"].Value);
        Assert.Single(terms);
        Assert.Equal("Docs", terms[0]);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_TrimsWhitespace()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "  Padded  " }
            })
        };

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        var terms = Assert.IsType<List<string>>(result["tags"].Value);
        Assert.Single(terms);
        Assert.Equal("Padded", terms[0]);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WhenLinksFieldMissing_DoesNothing()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        Assert.False(result.ContainsKey("tags"));
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WhenLinksFieldValueNull_DoesNothing()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", null!)
        };

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        Assert.False(result.ContainsKey("tags"));
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WithEmptyLinks_DoesNothing()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>())
        };

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        Assert.False(result.ContainsKey("tags"));
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WithNullLinkEntries_SkipsThem()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                null!,
                new() { ["title"] = "Valid" }
            })
        };

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        var terms = Assert.IsType<List<string>>(result["tags"].Value);
        Assert.Single(terms);
        Assert.Equal("Valid", terms[0]);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WithEmptyTrimmedTerms_SkipsThem()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "   " },
                new() { ["slug"] = "" },
                new() { ["id"] = "real" }
            })
        };

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        var terms = Assert.IsType<List<string>>(result["tags"].Value);
        Assert.Single(terms);
        Assert.Equal("real", terms[0]);
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WhenLinksValueNotEnumerable_DoesNothing()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("text", "not-a-list")
        };

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");

        Assert.False(result.ContainsKey("tags"));
    }

    [Fact]
    public void PromoteRelationTaxonomyTerms_WithCategoriesKey_UsesCorrectLinkKey()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["categories_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "Tech" },
                new() { ["title"] = "Life" }
            })
        };

        var result = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "categories");

        Assert.True(result.ContainsKey("categories"));
        var terms = Assert.IsType<List<string>>(result["categories"].Value);
        Assert.Equal(new[] { "Tech", "Life" }, terms);
    }
}
