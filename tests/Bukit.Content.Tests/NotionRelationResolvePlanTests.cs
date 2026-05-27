using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionRelationResolvePlanTests
{
    [Fact]
    public void BuildMissingIds_DeduplicatesSkipsIndexedAndLimitsToTaxonomyRelations()
    {
        var candidates = new[]
        {
            new NotionRelationResolveCandidate(
                new[] { "tags", "authors" },
                new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tags"] = new("relation", new[] { "tag-1", "tag-2", "tag-1" }),
                    ["authors"] = new("relation", new[] { "author-1" })
                }),
            new NotionRelationResolveCandidate(
                new[] { "categories" },
                new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["categories"] = new("relation", new[] { "cat-1", "tag-2", "cat-2" })
                })
        };

        var existingIndex = new Dictionary<string, RelationTargetInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["tag-2"] = new("tag-2", "Tag 2", "tag-2", "page", null)
        };

        var missing = NotionRelationResolvePlan.BuildMissingIds(candidates, existingIndex, maxResolve: 2);

        Assert.Equal(new[] { "tag-1", "cat-1" }, missing);
    }
}
