using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

[Trait("Category", "Contract")]
public sealed class CanonicalContentGraphContractTests
{
    [Fact]
    public void BuildFromDocuments_DuplicateIds_PreservesRecordsForValidation()
    {
        var first = Document("duplicate", new ContentRelation("related-to", "post-2", "content", "post-2"));
        var second = Document("duplicate", new ContentRelation("related-to", "POST-2", "content", "POST-2"));

        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([first, second]);

        Assert.Equal(2, graph.Records.Count);
        Assert.Equal(2, graph.Documents.Count);
        Assert.Contains(
            CanonicalContentValidator.Validate(graph),
            error => error.Code == "canonical_duplicate_id");
    }

    [Fact]
    public void BuildFromDocuments_DanglingRelationTarget_IsPreserved()
    {
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([
            Document("post-1", new ContentRelation("related-to", "missing-post", "content", "missing-post"))
        ]);

        var relation = Assert.Single(graph.Relations);
        Assert.Equal("related-to", relation.Type);
        Assert.Equal("missing-post", relation.Target);
        Assert.Equal("missing-post", relation.TargetId);
    }

    [Fact]
    public void BuildFromDocuments_RelationsWithSameTargetButDifferentTargetType_AreNotDeduplicated()
    {
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([
            Document(
                "post-1",
                new ContentRelation("related-to", "same-target", "content", "same-target"),
                new ContentRelation("related-to", "same-target", "author", "same-target"))
        ]);

        Assert.Equal(2, graph.Relations.Count);
    }

    [Fact]
    public void CanonicalContentValidator_DuplicateIdsAndDanglingTargets_AreReported()
    {
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([
            Document("duplicate", new ContentRelation("related-to", "missing-post", "content", "missing-post")),
            Document("duplicate")
        ]);

        var issues = CanonicalContentValidator.Validate(graph)
            .Concat(ContentModelSchemaValidator.Validate(graph))
            .ToArray();

        Assert.Contains(issues, error => error.Code == "canonical_duplicate_id");
        Assert.Contains(issues, error =>
            error.Code == "canonical_relation_target_unresolved" &&
            error.Message.Contains("missing-post", StringComparison.Ordinal));
    }

    private static ContentDocument Document(string id, params ContentRelation[] relations)
        => new(
            new ContentRecord(
                new ContentIdentity(id, id, id, "post", "published"),
                new ContentPresentation("Title", "Summary", "<p>Body</p>", "en", []),
                new ContentClassification("post", "posts", [], []),
                new ContentOwnership("Alice", null, null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), null, null, null),
                new ProvenanceRecord("markdown", null, [], [], null),
                new TrustMetadata(0.9, "approved", []),
                [],
                relations,
                []),
            new ContentBodyRef("<p>Body</p>"));
}
