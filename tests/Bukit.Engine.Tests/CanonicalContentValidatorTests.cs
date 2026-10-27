using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

[Trait("Category", "Contract")]
public sealed class CanonicalContentValidatorTests
{
    [Fact]
    public void Validate_ShouldReportDuplicateContentIds()
    {
        var graph = new CanonicalContentGraph(
        [
            Record(id: "post-1"),
            Record(id: "POST-1")
        ], []);

        var errors = CanonicalContentValidator.Validate(graph);

        Assert.Contains(errors, error =>
            error.Code == "canonical_duplicate_id" &&
            error.Field == "identity.id" &&
            error.SourcePath == "post-1");
    }
    [Fact]
    public void Validate_ShouldReportRelationTargetGap_WhenRelationTargetIsBlank()
    {
        var graph = new CanonicalContentGraph(
        [
            Record(
                relations: [new ContentRelation("related-to", " ")])
        ], []);

        var errors = CanonicalContentValidator.Validate(graph);

        Assert.Contains(errors, error =>
            error.Code == "canonical_relation_target_missing" &&
            error.Field == "relations.target" &&
            error.SourcePath == "post-1");
    }

    [Fact]
    public void Validate_ShouldReportProvenanceSourceGap_WhenSourceIsBlank()
    {
        var graph = new CanonicalContentGraph(
        [
            Record(source: " ")
        ], []);

        var errors = CanonicalContentValidator.Validate(graph);

        Assert.Contains(errors, error =>
            error.Code == "canonical_source_missing" &&
            error.Field == "provenance.source" &&
            error.SourcePath == "post-1");
    }

    [Fact]
    public void Validate_ShouldReportTrustReviewStatusGap_WhenReviewStatusIsBlank()
    {
        var graph = new CanonicalContentGraph(
        [
            Record(reviewStatus: " ")
        ], []);

        var errors = CanonicalContentValidator.Validate(graph);

        Assert.Contains(errors, error =>
            error.Code == "canonical_review_status_missing" &&
            error.Field == "trust.review_status" &&
            error.SourcePath == "post-1");
    }

    private static ContentRecord Record(
        string? source = "markdown",
        string reviewStatus = "approved",
        IReadOnlyList<ContentRelation>? relations = null,
        string id = "post-1")
        => new(
            new ContentIdentity(id, id, id, "post", "published"),
            new ContentPresentation("Post", "Summary", "<p>Body</p>", "en", []),
            new ContentClassification("post", "post", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), null, null, null),
            new ProvenanceRecord(source, null, [], [], null),
            new TrustMetadata(null, reviewStatus, []),
            [],
            relations ?? [],
            []);
}
