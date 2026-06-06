using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class CanonicalContentValidator
{
    internal static IReadOnlyList<ContentSchemaValidator.SchemaValidationError> Validate(CanonicalContentGraph graph)
    {
        if (graph.Records.Count == 0)
        {
            return Array.Empty<ContentSchemaValidator.SchemaValidationError>();
        }

        var errors = new List<ContentSchemaValidator.SchemaValidationError>();
        foreach (var record in graph.Records)
        {
            if (record.Provenance.Source is not null &&
                string.IsNullOrWhiteSpace(record.Provenance.Source))
            {
                errors.Add(new ContentSchemaValidator.SchemaValidationError(
                    "provenance.source",
                    "canonical_source_missing",
                    "Canonical provenance source is blank.",
                    record.Identity.Id));
            }

            if (string.IsNullOrWhiteSpace(record.Trust.ReviewStatus))
            {
                errors.Add(new ContentSchemaValidator.SchemaValidationError(
                    "trust.review_status",
                    "canonical_review_status_missing",
                    "Canonical trust review status is blank.",
                    record.Identity.Id));
            }

            foreach (var relation in record.Relations)
            {
                if (string.IsNullOrWhiteSpace(relation.Target))
                {
                    errors.Add(new ContentSchemaValidator.SchemaValidationError(
                        "relations.target",
                        "canonical_relation_target_missing",
                        $"Canonical relation '{relation.Type}' is missing a target.",
                        record.Identity.Id));
                }
            }

            foreach (var media in record.Media)
            {
                if (RequiresAlt(media) && string.IsNullOrWhiteSpace(media.Alt))
                {
                    errors.Add(new ContentSchemaValidator.SchemaValidationError(
                        "media.alt",
                        "canonical_media_alt_missing",
                        $"Media asset '{media.Url}' is missing alt text.",
                        record.Identity.Id));
                }
            }
        }

        return errors;
    }

    private static bool RequiresAlt(MediaAsset media)
        => string.Equals(media.Kind, "image", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(media.Url);
}
