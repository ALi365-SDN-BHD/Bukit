using Bukit.Engine.Abstractions.Content;
using Bukit.Content;
using Xunit;

namespace Bukit.Engine.Tests;

/// <summary>
/// Extended tests for ContentModelSchemaValidator custom field, media, and mapping validation.
/// </summary>
public sealed class ContentModelSchemaValidatorCoverageTests
{
    private static ContentDocument MakeDoc(
        string id,
        IReadOnlyDictionary<string, object>? fields = null,
        string type = "post")
    {
        var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = type
        };
        if (fields is not null)
        {
            foreach (var (k, v) in fields)
            {
                map[k] = v;
            }
        }

        return ContentDocument.Create(
            id, id, id, DateTimeOffset.UtcNow, null,
            ContentFieldReader.ToFieldMap(map));
    }

    private static IReadOnlyList<ContentValidationIssue> Validate(params ContentDocument[] docs)
        => ContentModelSchemaValidator.Validate(
            CanonicalContentGraphBuilder.BuildFromDocuments(docs));

    // ── Custom field required ────────────────────────────────────────

    [Fact]
    public void Validate_RequiredCustomFieldMissing_ReportsError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            CustomFields = new Dictionary<string, CustomFieldDefinition>
            {
                ["headline"] = new("headline", "string", Required: true)
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([MakeDoc("post-1", new Dictionary<string, object> { ["body"] = "x" })]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.custom_field_required_missing" && e.SourcePath == "post-1");
    }

    [Fact]
    public void Validate_RequiredCustomFieldWithDefault_NoError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            CustomFields = new Dictionary<string, CustomFieldDefinition>
            {
                ["status"] = new("status", "string", Required: true, Default: "draft")
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([MakeDoc("post-1")]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.DoesNotContain(errors, e => e.Code == "content.custom_field_required_missing");
    }

    // ── Custom field type mismatch ───────────────────────────────────

    [Fact]
    public void Validate_CustomFieldTypeMismatch_ReportsError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            CustomFields = new Dictionary<string, CustomFieldDefinition>
            {
                ["count"] = new("count", "number")
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(
            [MakeDoc("post-1", new Dictionary<string, object> { ["count"] = "not-a-number" })]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.custom_field_type_mismatch" && e.SourcePath == "post-1");
    }

    // ── Custom field enum mismatch ───────────────────────────────────

    [Fact]
    public void Validate_CustomFieldEnumMismatch_ReportsError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            CustomFields = new Dictionary<string, CustomFieldDefinition>
            {
                ["status"] = new("status", "string", Enum: ["draft", "published"])
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(
            [MakeDoc("post-1", new Dictionary<string, object> { ["status"] = "archived" })]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.custom_field_enum_mismatch");
    }

    // ── Custom field format mismatch ─────────────────────────────────

    [Fact]
    public void Validate_CustomFieldFormatMismatch_ReportsError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            CustomFields = new Dictionary<string, CustomFieldDefinition>
            {
                ["canonical"] = new("canonical", "string", Format: "url")
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(
            [MakeDoc("post-1", new Dictionary<string, object> { ["canonical"] = "not-a-url" })]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.custom_field_format_mismatch");
    }

    // ── Custom field range mismatch ──────────────────────────────────

    [Fact]
    public void Validate_CustomFieldRangeMismatch_ReportsError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            CustomFields = new Dictionary<string, CustomFieldDefinition>
            {
                ["score"] = new("score", "number", Min: 1, Max: 10)
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(
            [MakeDoc("post-1", new Dictionary<string, object> { ["score"] = 42 })]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.custom_field_range_mismatch");
    }

    // ── Invalid source policy ────────────────────────────────────────

    [Fact]
    public void Validate_InvalidSourcePolicy_ReportsError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            CustomFields = new Dictionary<string, CustomFieldDefinition>
            {
                ["headline"] = new("headline", "string", SourcePolicy: "invalid-policy")
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(
            [MakeDoc("post-1", new Dictionary<string, object> { ["headline"] = "Hello" })]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.custom_field_source_policy_invalid");
    }

    // ── Required entity mapping ──────────────────────────────────────

    [Fact]
    public void Validate_RequiredEntityMappingMissing_ReportsError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            EntityMappings = new Dictionary<string, EntityMapping>
            {
                ["authorName"] = new("authorName", "Person", Required: true)
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([MakeDoc("post-1")]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.entity_mapping_required_missing");
    }

    // ── Required relation mapping ────────────────────────────────────

    [Fact]
    public void Validate_RequiredRelationMappingMissing_ReportsError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            RelationMappings = new Dictionary<string, RelationMapping>
            {
                ["relatedPost"] = new("relatedPost", "related-to", Required: true)
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([MakeDoc("post-1")]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.relation_mapping_required_missing");
    }

    // ── Reference member required ────────────────────────────────────

    [Fact]
    public void Validate_ReferenceMissingRequiredMember_ReportsError()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            EntityMappings = new Dictionary<string, EntityMapping>
            {
                ["authorRef"] = new(
                    "authorRef",
                    "Person",
                    Required: true,
                    Reference: new ContentReferenceRule(IdField: "id", LabelField: "label", Required: true))
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(
            [MakeDoc("post-1", new Dictionary<string, object> { ["authorRef"] = new Dictionary<string, object?> { ["label"] = "John" } })]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.reference_field_missing" && e.Message.Contains("id"));
    }

    // ── Field scope per collection ───────────────────────────────────

    [Fact]
    public void Validate_ScopedFieldForCollection_Validated()
    {
        var schema = ContentModelSchemaValidator.Default with
        {
            FieldScopes = new Dictionary<string, IReadOnlyList<CustomFieldDefinition>>
            {
                ["posts"] =
                [
                    new("readingTime", "string", Required: true)
                ]
            }
        };
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(
            [MakeDoc("post-1", new Dictionary<string, object> { ["collection"] = "posts" })]);

        var errors = ContentModelSchemaValidator.Validate(graph, schema);

        Assert.Contains(errors, e => e.Code == "content.custom_field_required_missing" && e.Field == "fields.readingTime");
    }
}
