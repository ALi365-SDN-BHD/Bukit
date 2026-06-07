using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionRelationLinkBuilderTests
{
    [Fact]
    public void EnrichFields_WhenRelationIdsResolvable_AddsLinksField()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["payments"] = new ContentField("list", new List<string> { "id1", "id2" })
        };

        var index = NotionRelationLinkBuilder.BuildIndex(new[]
        {
            new RelationTargetInfo("id1", "visa", "visa", "page", "https://visa.example"),
            new RelationTargetInfo("id2", "master", "master", "page", null)
        });

        var enriched = NotionRelationLinkBuilder.EnrichFields(fields, new[] { "payments" }, index);

        Assert.True(enriched.ContainsKey("payments"));
        Assert.True(enriched.ContainsKey("payments_links"));

        var linksField = enriched["payments_links"];
        Assert.Equal("list", linksField.Type);

        var links = Assert.IsType<List<Dictionary<string, object?>>>(linksField.Value);
        Assert.Equal(2, links.Count);

        Assert.Equal("id1", links[0]["id"]);
        Assert.Equal("visa", links[0]["title"]);
        Assert.Equal("https://visa.example", links[0]["url"]);
        Assert.Equal("visa", links[0]["slug"]);
        Assert.Equal("page", links[0]["type"]);

        Assert.Equal("id2", links[1]["id"]);
        Assert.Equal("master", links[1]["title"]);
        Assert.Null(links[1]["url"]);
    }

    [Fact]
    public void EnrichFields_WhenRelationIdMissing_StillAddsEntryWithNulls()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["payments"] = new ContentField("list", new List<string> { "missing" })
        };

        var enriched = NotionRelationLinkBuilder.EnrichFields(fields, new[] { "payments" }, new Dictionary<string, RelationTargetInfo>());

        Assert.Same(fields, enriched);
    }

    [Fact]
    public void EnrichFields_WhenLinksKeyAlreadyExists_DoesNotOverride()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["payments"] = new ContentField("list", new List<string> { "id1" }),
            ["payments_links"] = new ContentField("list", new List<object>())
        };

        var index = NotionRelationLinkBuilder.BuildIndex(new[]
        {
            new RelationTargetInfo("id1", "visa", "visa", "page", "https://visa.example")
        });

        var enriched = NotionRelationLinkBuilder.EnrichFields(fields, new[] { "payments" }, index);
        Assert.Same(fields, enriched);
    }

    [Fact]
    public void BuildIndex_SkipsBlankPageIdsAndKeepsLastDuplicate()
    {
        var index = NotionRelationLinkBuilder.BuildIndex(new[]
        {
            new RelationTargetInfo("", "Blank", "blank", "page", null),
            new RelationTargetInfo("id1", "Old", "old", "page", null),
            new RelationTargetInfo("ID1", "New", "new", "post", "https://example.test/new")
        });

        var target = Assert.Single(index);
        Assert.Equal("ID1", target.Value.PageId);
        Assert.Equal("New", target.Value.Title);
    }

    [Fact]
    public void EnrichFields_WithUnusableRelationKeys_ReturnsOriginalFields()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["not_list"] = new ContentField("text", "id1"),
            ["empty_ids"] = new ContentField("list", new List<string> { " ", "" })
        };
        var index = NotionRelationLinkBuilder.BuildIndex(new[]
        {
            new RelationTargetInfo("id1", "Visa", "visa", "page", null)
        });

        Assert.Same(fields, NotionRelationLinkBuilder.EnrichFields(fields, Array.Empty<string>(), index));
        Assert.Same(fields, NotionRelationLinkBuilder.EnrichFields(fields, new[] { "", "missing", "not_list", "empty_ids" }, index));
    }

    [Fact]
    public void EnrichFields_WhenIndexMissesId_AddsFallbackLinkEntry()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["payments"] = new ContentField("list", new List<string> { " missing-id " })
        };
        var index = NotionRelationLinkBuilder.BuildIndex(new[]
        {
            new RelationTargetInfo("other", "Other", "other", "page", null)
        });

        var enriched = NotionRelationLinkBuilder.EnrichFields(fields, new[] { "payments" }, index);

        var links = Assert.IsType<List<Dictionary<string, object?>>>(enriched["payments_links"].Value);
        var link = Assert.Single(links);
        Assert.Equal("missing-id", link["id"]);
        Assert.Null(link["title"]);
        Assert.Null(link["url"]);
        Assert.Null(link["slug"]);
        Assert.Null(link["type"]);
    }

    [Fact]
    public void ProjectRelationTaxonomyTerms_UsesTitleThenSlugThenId()
    {
        var projectedValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = new List<string> { "id1", "id2" }
        };

        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags_links"] = new ContentField("list", new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["id"] = "id1",
                    ["title"] = "Visa",
                    ["slug"] = "visa"
                },
                new()
                {
                    ["id"] = "id2",
                    ["title"] = null,
                    ["slug"] = "mastercard"
                },
                new()
                {
                    ["id"] = "id3",
                    ["title"] = null,
                    ["slug"] = null
                }
            })
        };

        NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(projectedValues, fields, "tags");

        var terms = Assert.IsType<List<string>>(projectedValues["tags"]);
        Assert.Equal(new[] { "Visa", "mastercard", "id3" }, terms);
    }
}
