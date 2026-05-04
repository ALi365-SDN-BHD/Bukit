using Bukit.Content;
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
    public void PromoteRelationTaxonomyTerms_UsesTitleThenSlugThenId()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
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

        NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(meta, fields, "tags");

        var terms = Assert.IsType<List<string>>(meta["tags"]);
        Assert.Equal(new[] { "Visa", "mastercard", "id3" }, terms);
    }
}
