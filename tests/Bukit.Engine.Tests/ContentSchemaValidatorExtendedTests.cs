using Bukit.Engine.Abstractions.Content;
using Bukit.Config;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentSchemaValidatorExtendedTests
{
    [Fact]
    public void Validate_AllowsNotionLastEditedTimeAsSystemField()
    {
        var schema = new[]
        {
            new CustomFieldDefinitionConfig { Name = "headline", FieldType = "string" }
        };
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["headline"] = "Hello",
            ["last_edited_time"] = DateTimeOffset.Parse("2026-07-12T08:30:00+08:00")
        };

        var errors = ContentSchemaValidator.Validate(meta, schema, "notion-page", failMode: "error");

        Assert.DoesNotContain(errors, e => e.Field == "last_edited_time" && e.Code == "unknown_field");
    }

    [Fact]
    public void CanonicalContentGraphBuilder_UsesNotionLastEditedTimeForLifecycle()
    {
        var updatedAt = DateTimeOffset.Parse("2026-07-12T08:30:00+08:00");
        var raw = new RawContentDocument(
            Id: "notion-page",
            Title: "Notion page",
            Slug: "notion-page",
            PublishAt: DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            Body: new RawBody(),
            CustomFields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["last_edited_time"] = updatedAt
            }));

        var document = ContentDocumentNormalizer.ToDocument(raw);
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(new[] { document });

        Assert.Equal(updatedAt, Assert.Single(graph.Records).Lifecycle.UpdatedAt);
    }

    [Fact]
    public void Validate_ReportsEnumFormatAndRangeErrors()
    {
        var schema = new[]
        {
            new CustomFieldDefinitionConfig { Name = "status", FieldType = "string", Enum = new[] { "draft", "published" } },
            new CustomFieldDefinitionConfig { Name = "canonical", FieldType = "string", Format = "url" },
            new CustomFieldDefinitionConfig { Name = "score", FieldType = "number", Min = 1, Max = 10 }
        };
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = "archived",
            ["canonical"] = "not a url",
            ["score"] = 42
        };

        var errors = ContentSchemaValidator.Validate(meta, schema, "post.md");

        Assert.Contains(errors, e => e.Field == "status" && e.Code == "enum_mismatch");
        Assert.Contains(errors, e => e.Field == "canonical" && e.Code == "format_mismatch");
        Assert.Contains(errors, e => e.Field == "score" && e.Code == "range_mismatch");
    }

    [Fact]
    public void ContentModelSchemaValidator_ReportsCanonicalStatusErrors()
    {
        var document = ContentDocument.Create(
            "hello",
            "Hello",
            "hello",
            DateTimeOffset.UtcNow,
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["status"] = "unknown",
                ["review_status"] = "unreviewable"
            }));
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(new[] { document });

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.Contains(errors, e => e.Code == "canonical_status_invalid" && e.SourcePath == "hello");
        Assert.Contains(errors, e => e.Code == "canonical_review_status_invalid" && e.SourcePath == "hello");
    }

    [Fact]
    public void CanonicalContentGraphBuilder_DoesNotProjectAuthorProfileTypeAsOwnershipWithoutAuthor()
    {
        var raw = new RawContentDocument(
            Id: "editorial-profile",
            Title: "Editorial profile",
            Slug: "editorial-profile",
            PublishAt: DateTimeOffset.Parse("2026-07-25T00:00:00Z"),
            Body: new RawBody(),
            CustomFields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "author-profile",
                ["authorType"] = "Organization"
            }));

        var document = ContentDocumentNormalizer.ToDocument(raw);
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments([document]);
        var record = Assert.Single(graph.Records);

        Assert.Equal("Organization", ContentFieldReader.GetText(document.CustomFields, "authorType"));
        Assert.Null(record.Ownership.Author);
        Assert.Null(record.Ownership.AuthorType);
        Assert.DoesNotContain(
            ContentModelSchemaValidator.Validate(graph),
            error => error.Code == "canonical_author_type_without_author");
    }

    [Fact]
    public void ContentModelSchemaValidator_ReportsInvalidAndOrphanedAuthorTypes()
    {
        var invalid = ContentDocument.Create(
            "invalid-author-type",
            "Invalid author type",
            "invalid-author-type",
            DateTimeOffset.UtcNow,
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["author"] = "Editorial Desk",
                ["authorType"] = "Company"
            }));
        var orphaned = ContentDocument.Create(
            "orphaned-author-type",
            "Orphaned author type",
            "orphaned-author-type",
            DateTimeOffset.UtcNow,
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["authorType"] = "Organization"
            }));

        var errors = ContentModelSchemaValidator.Validate(
            CanonicalContentGraphBuilder.BuildFromDocuments(new[] { invalid, orphaned }));

        Assert.Contains(errors, e =>
            e.Code == "canonical_author_type_invalid" &&
            e.Field == "ownership.author_type" &&
            e.SourcePath == "invalid-author-type");
        Assert.Contains(errors, e =>
            e.Code == "canonical_author_type_without_author" &&
            e.Field == "ownership.author_type" &&
            e.SourcePath == "orphaned-author-type");
    }
}
