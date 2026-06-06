using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class ContentModelSchemaValidator
{
    internal static readonly ContentModelSchema Default = new(
        Statuses: ["draft", "published", "archived", "expired"],
        ReviewStatuses: ["draft", "published", "reviewed", "verified", "needs-review", "archived", "expired"],
        SyncStatuses: ["synced", "pending", "failed", "manual", "unknown"],
        RequireRelationTargets: true,
        RequireMediaAlt: true);

    internal static IReadOnlyList<ContentSchemaValidator.SchemaValidationError> Validate(
        CanonicalContentGraph graph,
        ContentModelSchema? schema = null)
    {
        schema ??= Default;
        var errors = new List<ContentSchemaValidator.SchemaValidationError>();

        foreach (var record in graph.Records)
        {
            ValidateRecord(record, schema, errors);
        }

        return errors;
    }

    private static void ValidateRecord(
        ContentRecord record,
        ContentModelSchema schema,
        List<ContentSchemaValidator.SchemaValidationError> errors)
    {
        CheckAllowed(schema.ContentTypes, record.Identity.ContentType, "identity.content_type", "canonical_content_type_invalid", record, errors);
        CheckAllowed(schema.Statuses, record.Identity.Status, "identity.status", "canonical_status_invalid", record, errors);
        CheckAllowed(schema.ReviewStatuses, record.Trust.ReviewStatus, "trust.review_status", "canonical_review_status_invalid", record, errors);
        CheckAllowed(schema.SyncStatuses, record.Provenance.SyncStatus, "provenance.sync_status", "canonical_sync_status_invalid", record, errors);

        Require(schema.RequireSummary, record.Presentation.Summary, "presentation.summary", "canonical_summary_missing", "Canonical content summary is required.", record, errors);
        Require(schema.RequireAuthor, record.Ownership.Author, "ownership.author", "canonical_author_missing", "Canonical content author is required.", record, errors);
        Require(schema.RequireOrganization, record.Ownership.Organization, "ownership.organization", "canonical_organization_missing", "Canonical content organization is required.", record, errors);
        Require(schema.RequireUpdatedAt, record.Lifecycle.UpdatedAt, "lifecycle.updated_at", "canonical_updated_at_missing", "Canonical content updated time is required.", record, errors);
        Require(schema.RequireReviewedAt, record.Lifecycle.ReviewedAt, "lifecycle.reviewed_at", "canonical_reviewed_at_missing", "Canonical content reviewed time is required.", record, errors);

        if (schema.RequireProvenance &&
            string.IsNullOrWhiteSpace(record.Provenance.Source) &&
            string.IsNullOrWhiteSpace(record.Provenance.OriginalSource))
        {
            Add(errors, "provenance", "canonical_provenance_missing", "Canonical provenance source or original source is required.", record);
        }

        if (schema.RequireEntityIds)
        {
            foreach (var entity in record.Entities.Where(x => string.IsNullOrWhiteSpace(x.Id)))
            {
                Add(errors, "entities.id", "canonical_entity_id_missing", $"Canonical entity '{entity.Name}' is missing an id.", record);
            }
        }

        if (schema.RequireRelationTargets)
        {
            foreach (var relation in record.Relations.Where(x => string.IsNullOrWhiteSpace(x.Target)))
            {
                Add(errors, "relations.target", "canonical_relation_target_missing", $"Canonical relation '{relation.Type}' is missing a target.", record);
            }
        }

        foreach (var media in record.Media)
        {
            if (schema.RequireMediaAlt && RequiresAlt(media) && string.IsNullOrWhiteSpace(media.Alt))
            {
                Add(errors, "media.alt", "canonical_media_alt_missing", $"Media asset '{media.Url}' is missing alt text.", record);
            }

            if (schema.RequireMediaDescription && string.IsNullOrWhiteSpace(media.Description))
            {
                Add(errors, "media.description", "canonical_media_description_missing", $"Media asset '{media.Url}' is missing a description.", record);
            }

            if (schema.RequireMediaLicense && string.IsNullOrWhiteSpace(media.License))
            {
                Add(errors, "media.license", "canonical_media_license_missing", $"Media asset '{media.Url}' is missing a license.", record);
            }
        }
    }

    private static void CheckAllowed(
        IReadOnlyList<string>? allowed,
        string? value,
        string field,
        string code,
        ContentRecord record,
        List<ContentSchemaValidator.SchemaValidationError> errors)
    {
        if (allowed is null || allowed.Count == 0 || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!allowed.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
        {
            Add(errors, field, code, $"Canonical field '{field}' must be one of: {string.Join(", ", allowed)}.", record);
        }
    }

    private static void Require(
        bool required,
        object? value,
        string field,
        string code,
        string message,
        ContentRecord record,
        List<ContentSchemaValidator.SchemaValidationError> errors)
    {
        if (!required)
        {
            return;
        }

        if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
        {
            Add(errors, field, code, message, record);
        }
    }

    private static bool RequiresAlt(MediaAsset media)
        => string.Equals(media.Kind, "image", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(media.Url);

    private static void Add(
        List<ContentSchemaValidator.SchemaValidationError> errors,
        string field,
        string code,
        string message,
        ContentRecord record)
        => errors.Add(new ContentSchemaValidator.SchemaValidationError(field, code, message, record.Identity.Id));
}
