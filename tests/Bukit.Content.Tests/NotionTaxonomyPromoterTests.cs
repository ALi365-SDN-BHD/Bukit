using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionTaxonomyPromoterTests
{
    [Fact]
    public void ProjectRelationTaxonomyTerms_WithLinksField_ProjectsTermsToFields()
    {
        var projectedValues = new Dictionary<string, object>();
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

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        Assert.True(projectedValues.ContainsKey("tags"));
        var terms = Assert.IsType<List<string>>(projectedValues["tags"]);
        Assert.Equal(new[] { "Docs", "Release" }, terms);
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_UsesTitleFirst_ThenSlug_ThenId()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "T1", ["slug"] = "s1", ["id"] = "i1" },
                new() { ["slug"] = "s2", ["id"] = "i2" },
                new() { ["id"] = "i3" }
            })
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        var terms = Assert.IsType<List<string>>(projectedValues["tags"]);
        Assert.Equal(new[] { "T1", "s2", "i3" }, terms);
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_DeduplicatesByCaseInsensitive()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "Docs" },
                new() { ["title"] = "docs" },
                new() { ["title"] = "DOCS" }
            })
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        var terms = Assert.IsType<List<string>>(projectedValues["tags"]);
        Assert.Single(terms);
        Assert.Equal("Docs", terms[0]);
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_TrimsWhitespace()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "  Padded  " }
            })
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        var terms = Assert.IsType<List<string>>(projectedValues["tags"]);
        Assert.Single(terms);
        Assert.Equal("Padded", terms[0]);
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_WhenLinksFieldMissing_DoesNothing()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        Assert.False(projectedValues.ContainsKey("tags"));
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_WhenLinksFieldValueNull_DoesNothing()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", null!)
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        Assert.False(projectedValues.ContainsKey("tags"));
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_WithEmptyLinks_DoesNothing()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>())
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        Assert.False(projectedValues.ContainsKey("tags"));
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_WithNullLinkEntries_SkipsThem()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                null!,
                new() { ["title"] = "Valid" }
            })
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        var terms = Assert.IsType<List<string>>(projectedValues["tags"]);
        Assert.Single(terms);
        Assert.Equal("Valid", terms[0]);
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_WithEmptyTrimmedTerms_SkipsThem()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "   " },
                new() { ["slug"] = "" },
                new() { ["id"] = "real" }
            })
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        var terms = Assert.IsType<List<string>>(projectedValues["tags"]);
        Assert.Single(terms);
        Assert.Equal("real", terms[0]);
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_WhenLinksValueNotEnumerable_DoesNothing()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("text", "not-a-list")
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        Assert.False(projectedValues.ContainsKey("tags"));
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_WithCategoriesKey_UsesCorrectLinkKey()
    {
        var projectedValues = new Dictionary<string, object>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["categories_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new() { ["title"] = "Tech" },
                new() { ["title"] = "Life" }
            })
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "categories");

        Assert.True(projectedValues.ContainsKey("categories"));
        var terms = Assert.IsType<List<string>>(projectedValues["categories"]);
        Assert.Equal(new[] { "Tech", "Life" }, terms);
    }
}
