using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

[Trait("Category", "Contract")]
public sealed class ContentModelSchemaValidatorRelationTargetTests
{
    [Fact]
    public void Validate_UnresolvedContentTarget_ReportsUnresolvedRelation()
    {
        var graph = Graph(Record(
            "post-1",
            relations: [new ContentRelation("related-to", "missing-post", "content", "missing-post")]));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.Contains(errors, error =>
            error.Code == "canonical_relation_target_unresolved" &&
            error.Field == "relations.target" &&
            error.SourcePath == "post-1" &&
            error.Message.Contains("missing-post", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ContentTargetResolvedByIdOrSlug_IsAccepted()
    {
        var graph = Graph(
            Record("post-1",
                relations:
                [
                    new ContentRelation("related-to", "post-2", "content", "post-2"),
                    new ContentRelation("related-to", "post-3-slug", "content", "post-3-slug")
                ]),
            Record("post-2"),
            Record("post-3", slug: "post-3-slug"));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.DoesNotContain(errors, error => error.Code == "canonical_relation_target_unresolved");
    }

    [Fact]
    public void Validate_EntityTargetResolvedByIdOrName_IsAccepted()
    {
        var graph = Graph(
            [new EntityRecord("person", "Alice", Id: "alice-1")],
            Record("post-1",
                relations:
                [
                    new ContentRelation("mentions", "alice-1", "person", "alice-1"),
                    new ContentRelation("mentions", "Alice", "person")
                ]));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.DoesNotContain(errors, error => error.Code == "canonical_relation_target_unresolved");
    }

    [Fact]
    public void Validate_UnresolvedEntityTarget_ReportsUnresolvedRelation()
    {
        var graph = Graph(Record(
            "post-1",
            relations: [new ContentRelation("mentions", "Bob", "person")]));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.Contains(errors, error =>
            error.Code == "canonical_relation_target_unresolved" &&
            error.SourcePath == "post-1");
    }

    [Fact]
    public void Validate_TranslationLanguageTarget_IsExemptFromResolution()
    {
        var graph = Graph(Record(
            "post-1",
            relations: [new ContentRelation("translation-of", "fr", "content", "fr")]));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.DoesNotContain(errors, error => error.Code == "canonical_relation_target_unresolved");
    }

    [Fact]
    public void Validate_ExternalUrlTarget_IsAccepted()
    {
        var graph = Graph(Record(
            "post-1",
            relations: [new ContentRelation("related-to", "https://example.com/external", "content", "https://example.com/external")]));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.DoesNotContain(errors, error => error.Code == "canonical_relation_target_unresolved");
    }

    [Fact]
    public void Validate_RootRelativeTarget_IsNotTreatedAsExternal()
    {
        var graph = Graph(Record(
            "post-1",
            relations: [new ContentRelation("related-to", "/posts/missing/", "content", "/posts/missing/")]));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.Contains(errors, error => error.Code == "canonical_relation_target_unresolved");
    }

    [Fact]
    public void Validate_WhitespacePaddedTarget_IsTrimmed()
    {
        var graph = Graph(
            Record("post-1", relations: [new ContentRelation("related-to", " post-2 ", "content", " post-2 ")]),
            Record("post-2"));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.DoesNotContain(errors, error => error.Code == "canonical_relation_target_unresolved");
    }

    [Fact]
    public void Validate_TargetIdTakesPrecedenceOverDisplayTarget()
    {
        var graph = Graph(
            Record("post-1", relations: [new ContentRelation("related-to", "missing-label", "content", "post-2")]),
            Record("post-2"));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.DoesNotContain(errors, error => error.Code == "canonical_relation_target_unresolved");
    }

    [Fact]
    public void Validate_TargetTypeMismatch_ReportsUnresolved()
    {
        var graph = Graph(
            [new EntityRecord("person", "Alice", Id: "alice-1")],
            Record("post-1", relations: [new ContentRelation("mentions", "alice-1", "company", "alice-1")]));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.Contains(errors, error => error.Code == "canonical_relation_target_unresolved");
    }

    [Fact]
    public void Validate_RequireRelationTargetsFalse_DoesNotResolveTargets()
    {
        var graph = Graph(Record(
            "post-1",
            relations: [new ContentRelation("related-to", "missing-post", "content", "missing-post")]));

        var errors = ContentModelSchemaValidator.Validate(
            graph,
            new ContentModelSchema(RequireRelationTargets: false));

        Assert.DoesNotContain(errors, error => error.Code == "canonical_relation_target_unresolved");
    }

    [Fact]
    public void Validate_BlankTarget_StillReportsMissingRelationTarget()
    {
        var graph = Graph(Record(
            "post-1",
            relations: [new ContentRelation("related-to", " ", "content", " ")]));

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.Contains(errors, error => error.Code == "canonical_relation_target_missing");
    }

    private static CanonicalContentGraph Graph(params ContentRecord[] records)
        => new(records, records.SelectMany(record => record.Entities).ToArray());

    private static CanonicalContentGraph Graph(EntityRecord[] entities, params ContentRecord[] records)
        => new(records, entities);

    private static ContentRecord Record(
        string id,
        string slug = "",
        ContentRelation[]? relations = null)
        => new(
            new ContentIdentity(id, string.IsNullOrEmpty(slug) ? id : slug, id, "post", "published"),
            new ContentPresentation("Title", "Summary", "<p>Body</p>", "en", []),
            new ContentClassification("post", "posts", [], []),
            new ContentOwnership("Alice", null, null, null),
            new ContentLifecycle(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), null, null, null),
            new ProvenanceRecord("markdown", null, [], [], null),
            new TrustMetadata(0.9, "approved", []),
            [],
            relations ?? [],
            []);
}
