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

        var errors = CanonicalContentValidator.Validate(graph, [], "en");

        Assert.Contains(errors, error =>
            error.Code == "canonical_duplicate_id" &&
            error.Field == "identity.id" &&
            error.SourcePath == "post-1");
    }

    [Fact]
    public void Validate_SameIdInDifferentLanguages_IsNotDuplicate()
    {
        var graph = new CanonicalContentGraph(
        [
            Record(id: "pages:index", language: "en"),
            Record(id: "pages:index", language: "zh-CN")
        ], []);

        var errors = CanonicalContentValidator.Validate(graph, Languages, "en");

        Assert.DoesNotContain(errors, error => error.Code == "canonical_duplicate_id");
    }

    [Fact]
    public void Validate_SameIdInOneLanguage_IgnoresLanguageCase()
    {
        var graph = new CanonicalContentGraph(
        [
            Record(id: "pages:index", language: "zh-CN"),
            Record(id: "pages:index", language: "zh-cn"),
            Record(id: "pages:index", language: "en")
        ], []);

        var error = Assert.Single(
            CanonicalContentValidator.Validate(graph, Languages, "en"),
            error => error.Code == "canonical_duplicate_id");
        Assert.Equal("pages:index", error.SourcePath);
        Assert.Contains("zh-CN", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PageWithoutLanguage_CollidesWithTheDefaultLanguage()
    {
        var graph = new CanonicalContentGraph(
        [
            Record(id: "pages:a", language: "und"),
            Record(id: "pages:a", language: "en"),
            Record(id: "pages:b", language: "und"),
            Record(id: "pages:b", language: "zh-CN")
        ], []);

        var error = Assert.Single(
            CanonicalContentValidator.Validate(graph, Languages, "en"),
            error => error.Code == "canonical_duplicate_id");
        Assert.Equal("pages:a", error.SourcePath);
        Assert.Contains("in language 'en'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SingleLanguageSite_KeepsIdsUniqueAcrossLanguages()
    {
        var graph = new CanonicalContentGraph(
        [
            Record(id: "pages:index", language: "en"),
            Record(id: "pages:index", language: "zh-CN")
        ], []);

        var error = Assert.Single(
            CanonicalContentValidator.Validate(graph, [], "en"),
            error => error.Code == "canonical_duplicate_id");
        Assert.Equal("Duplicate canonical content id 'pages:index'.", error.Message);
    }

    [Fact]
    public void Validate_ShouldReportRelationTargetGap_WhenRelationTargetIsBlank()
    {
        var graph = new CanonicalContentGraph(
        [
            Record(
                relations: [new ContentRelation("related-to", " ")])
        ], []);

        var errors = CanonicalContentValidator.Validate(graph, [], "en");

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

        var errors = CanonicalContentValidator.Validate(graph, [], "en");

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

        var errors = CanonicalContentValidator.Validate(graph, [], "en");

        Assert.Contains(errors, error =>
            error.Code == "canonical_review_status_missing" &&
            error.Field == "trust.review_status" &&
            error.SourcePath == "post-1");
    }

    private static readonly string[] Languages = ["en", "zh-CN"];

    private static ContentRecord Record(
        string? source = "markdown",
        string reviewStatus = "approved",
        IReadOnlyList<ContentRelation>? relations = null,
        string id = "post-1",
        string language = "en")
        => new(
            new ContentIdentity(id, id, id, "post", "published"),
            new ContentPresentation("Post", "Summary", "<p>Body</p>", language, []),
            new ContentClassification("post", "post", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), null, null, null),
            new ProvenanceRecord(source, null, [], [], null),
            new TrustMetadata(null, reviewStatus, []),
            [],
            relations ?? [],
            []);
}
