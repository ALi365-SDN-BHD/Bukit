using Bukit.Engine.Abstractions.Content;
using System.Globalization;

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

        var documentsById = graph.Documents
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var record in graph.Records)
        {
            ValidateRecord(record, schema, errors);
            documentsById.TryGetValue(record.Identity.Id, out var document);
            ValidateDocumentFields(record, document, schema, errors);
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
            if (schema.Media?.AllowedKinds is { Count: > 0 } allowedKinds &&
                !allowedKinds.Any(kind => string.Equals(kind, media.Kind, StringComparison.OrdinalIgnoreCase)))
            {
                Add(errors, "media.kind", "content.media_kind_not_allowed", $"Media asset kind '{media.Kind}' is not allowed.", record);
            }

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

    private static void ValidateDocumentFields(
        ContentRecord record,
        ContentDocument? document,
        ContentModelSchema schema,
        List<ContentSchemaValidator.SchemaValidationError> errors)
    {
        if (document is null)
        {
            return;
        }

        foreach (var definition in EnumerateFieldDefinitions(schema, document.CustomFields))
        {
            ValidateCustomField(record, document, definition, errors);
        }

        foreach (var mapping in schema.EntityMappings?.Values ?? Array.Empty<EntityMapping>())
        {
            ValidateRequiredMapping(
                record,
                document,
                mapping.RawKey,
                mapping.Required,
                $"entities.{mapping.RawKey}",
                "content.entity_mapping_required_missing",
                $"Required entity mapping '{mapping.RawKey}' is missing.",
                errors);
            ValidateReferenceRule(record, document, mapping.RawKey, mapping.Reference, $"entities.{mapping.RawKey}", errors);
        }

        foreach (var mapping in schema.RelationMappings?.Values ?? Array.Empty<RelationMapping>())
        {
            ValidateRequiredMapping(
                record,
                document,
                mapping.RawKey,
                mapping.Required,
                $"relations.{mapping.RawKey}",
                "content.relation_mapping_required_missing",
                $"Required relation mapping '{mapping.RawKey}' is missing.",
                errors);
            ValidateReferenceRule(record, document, mapping.RawKey, mapping.Reference, $"relations.{mapping.RawKey}", errors);
        }
    }

    private static IEnumerable<CustomFieldDefinition> EnumerateFieldDefinitions(
        ContentModelSchema schema,
        IReadOnlyDictionary<string, ContentField>? fields)
    {
        foreach (var definition in schema.CustomFields?.Values ?? Array.Empty<CustomFieldDefinition>())
        {
            yield return definition;
        }

        var collection = ContentFieldReader.GetText(fields, "collection")
            ?? ContentFieldReader.GetText(fields, "type");
        if (!string.IsNullOrWhiteSpace(collection) &&
            schema.FieldScopes?.TryGetValue(collection, out var scopedFields) is true)
        {
            foreach (var definition in scopedFields)
            {
                yield return definition;
            }
        }
    }

    private static void ValidateCustomField(
        ContentRecord record,
        ContentDocument document,
        CustomFieldDefinition definition,
        List<ContentSchemaValidator.SchemaValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return;
        }

        if (!ValidateSourcePolicy(definition.SourcePolicy))
        {
            Add(errors, $"fields.{definition.Name}", "content.custom_field_source_policy_invalid", $"Custom field '{definition.Name}' has invalid source policy '{definition.SourcePolicy}'.", record);
        }

        var hasField = ContentFieldReader.TryGetField(document.CustomFields, definition.Name, out var field) &&
            field.Value is not null;
        if (definition.Required && !hasField && definition.Default is null)
        {
            Add(errors, $"fields.{definition.Name}", "content.custom_field_required_missing", $"Required custom field '{definition.Name}' is missing.", record);
            return;
        }

        if (!hasField)
        {
            return;
        }

        var value = field.Value!;
        if (!ValidateFieldType(definition.FieldType, value))
        {
            Add(errors, $"fields.{definition.Name}", "content.custom_field_type_mismatch", $"Custom field '{definition.Name}' expected type '{definition.FieldType}'.", record);
            return;
        }

        if (definition.Enum is { Count: > 0 } allowed && !MatchesEnum(value, allowed))
        {
            Add(errors, $"fields.{definition.Name}", "content.custom_field_enum_mismatch", $"Custom field '{definition.Name}' must be one of: {string.Join(", ", allowed)}.", record);
        }

        var format = definition.Format ?? definition.SemanticType;
        if (!string.IsNullOrWhiteSpace(format) && !ValidateFormat(format, value))
        {
            Add(errors, $"fields.{definition.Name}", "content.custom_field_format_mismatch", $"Custom field '{definition.Name}' must match format '{format}'.", record);
        }

        if ((definition.Min is not null || definition.Max is not null) && !ValidateRange(value, definition.Min, definition.Max))
        {
            Add(errors, $"fields.{definition.Name}", "content.custom_field_range_mismatch", $"Custom field '{definition.Name}' must be within range {definition.Min?.ToString(CultureInfo.InvariantCulture) ?? "-inf"}..{definition.Max?.ToString(CultureInfo.InvariantCulture) ?? "inf"}.", record);
        }

        ValidateReferenceRule(record, document, definition.Name, definition.Reference, $"fields.{definition.Name}", errors);
    }

    private static void ValidateRequiredMapping(
        ContentRecord record,
        ContentDocument document,
        string rawKey,
        bool required,
        string field,
        string code,
        string message,
        List<ContentSchemaValidator.SchemaValidationError> errors)
    {
        if (!required)
        {
            return;
        }

        if (!ContentFieldReader.TryGetField(document.CustomFields, rawKey, out var contentField) ||
            IsEmpty(contentField.Value))
        {
            Add(errors, field, code, message, record);
        }
    }

    private static void ValidateReferenceRule(
        ContentRecord record,
        ContentDocument document,
        string rawKey,
        ContentReferenceRule? rule,
        string field,
        List<ContentSchemaValidator.SchemaValidationError> errors)
    {
        if (rule is null ||
            !ContentFieldReader.TryGetField(document.CustomFields, rawKey, out var contentField) ||
            IsEmpty(contentField.Value))
        {
            return;
        }

        foreach (var reference in EnumerateReferenceMaps(contentField.Value))
        {
            RequireReferenceMember(record, rule.Required, rule.IdField, reference, field, "id", errors);
            RequireReferenceMember(record, rule.Required, rule.LabelField, reference, field, "label", errors);
            RequireReferenceMember(record, rule.Required, rule.UrlField, reference, field, "url", errors);
        }
    }

    private static void RequireReferenceMember(
        ContentRecord record,
        bool required,
        string? member,
        IReadOnlyDictionary<string, object?> reference,
        string field,
        string role,
        List<ContentSchemaValidator.SchemaValidationError> errors)
    {
        if (!required || string.IsNullOrWhiteSpace(member))
        {
            return;
        }

        if (!reference.TryGetValue(member, out var value) || IsEmpty(value))
        {
            Add(errors, field, "content.reference_field_missing", $"Reference field '{field}' is missing required {role} member '{member}'.", record);
        }
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> EnumerateReferenceMaps(object? value)
    {
        switch (value)
        {
            case IReadOnlyDictionary<string, object?> map:
                yield return map;
                break;
            case IDictionary<string, object?> map:
                yield return new Dictionary<string, object?>(map, StringComparer.OrdinalIgnoreCase);
                break;
            case IEnumerable<IReadOnlyDictionary<string, object?>> maps:
                foreach (var map in maps)
                {
                    yield return map;
                }
                break;
            case IEnumerable<object> objects:
                foreach (var item in objects)
                {
                    if (item is IReadOnlyDictionary<string, object?> itemMap)
                    {
                        yield return itemMap;
                    }
                }
                break;
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

    private static bool ValidateSourcePolicy(string? sourcePolicy)
    {
        if (string.IsNullOrWhiteSpace(sourcePolicy))
        {
            return true;
        }

        return sourcePolicy.Trim().ToLowerInvariant() is "raw" or "canonical" or "custom" or "provider" or "any";
    }

    private static bool ValidateFieldType(string? expectedType, object value)
    {
        return (expectedType ?? "string").Trim().ToLowerInvariant() switch
        {
            "string" or "text" => value is string,
            "number" or "int" or "integer" => value is byte or short or int or long or float or double or decimal,
            "bool" or "boolean" => value is bool,
            "date" or "datetime" => value is DateTime or DateTimeOffset || value is string s && DateTimeOffset.TryParse(s, out _),
            "list" or "array" or "string[]" => value is System.Collections.IEnumerable and not string,
            _ => true
        };
    }

    private static bool MatchesEnum(object value, IReadOnlyList<string> allowed)
    {
        var values = ContentFieldReader.ToTextList(value) ?? Array.Empty<string>();
        return values.Count > 0 &&
            values.All(item => allowed.Any(allowedValue => string.Equals(allowedValue, item, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ValidateFormat(string format, object value)
    {
        var values = ContentFieldReader.ToTextList(value) ?? Array.Empty<string>();
        if (values.Count == 0)
        {
            return false;
        }

        return values.All(text => format.Trim().ToLowerInvariant() switch
        {
            "url" or "uri" => Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                              (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
            "email" => text.Contains('@', StringComparison.Ordinal) &&
                       text.IndexOf('@', StringComparison.Ordinal) > 0 &&
                       text.IndexOf('@', StringComparison.Ordinal) < text.Length - 1,
            "date" or "datetime" => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
            "slug" => text.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '/'),
            _ => true
        });
    }

    private static bool ValidateRange(object value, double? min, double? max)
    {
        if (!TryConvertToDouble(value, out var number))
        {
            number = value.ToString()?.Length ?? 0;
        }

        return (min is null || number >= min.Value) &&
               (max is null || number <= max.Value);
    }

    private static bool TryConvertToDouble(object value, out double number)
    {
        return value switch
        {
            byte b => Set(b, out number),
            short s => Set(s, out number),
            int i => Set(i, out number),
            long l => Set(l, out number),
            float f => Set(f, out number),
            double d => Set(d, out number),
            decimal m => Set((double)m, out number),
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => Set(parsed, out number),
            _ => Set(0, out number, false)
        };
    }

    private static bool Set(double value, out double number, bool result = true)
    {
        number = value;
        return result;
    }

    private static bool IsEmpty(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var _ in enumerable)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static void Add(
        List<ContentSchemaValidator.SchemaValidationError> errors,
        string field,
        string code,
        string message,
        ContentRecord record)
        => errors.Add(new ContentSchemaValidator.SchemaValidationError(field, code, message, record.Identity.Id));
}
