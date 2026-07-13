using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.PublishAuditRules;

internal sealed record TrustAuditRequirements(
    bool RequireAuthor,
    bool RequireProvenance,
    bool RequireEntities)
{
    internal static TrustAuditRequirements From(ContentModelSchema schema)
        => new(
            schema.RequireAuthor,
            schema.RequireProvenance,
            schema.EntityMappings?.Values.Any(mapping => mapping.Required) == true);
}

internal static class TrustAuditRules
{
    internal static void Analyze(
        PublishDocument document,
        TrustAuditRequirements requirements,
        List<PublishAuditIssue> issues)
    {
        if (!document.Indexable || !PublishDocumentAuditScope.IsContentBacked(document))
        {
            return;
        }

        if (requirements.RequireAuthor && string.IsNullOrWhiteSpace(document.Author))
        {
            issues.Add(Warning("publish.author_missing", document.RouteUrl, "Published content is missing author metadata."));
        }

        if (requirements.RequireProvenance &&
            string.IsNullOrWhiteSpace(document.Source) &&
            string.IsNullOrWhiteSpace(document.OriginalSource))
        {
            issues.Add(Warning("publish.source_missing", document.RouteUrl, "Published content is missing source/provenance metadata."));
        }

        if (string.IsNullOrWhiteSpace(document.ReviewStatus))
        {
            issues.Add(Warning("publish.review_status_missing", document.RouteUrl, "Published content is missing review status metadata."));
        }

        if (document.ContentRecord?.Lifecycle.UpdatedAt is null)
        {
            issues.Add(Warning("publish.updated_at_missing", document.RouteUrl, "Published content is missing explicit updated-at metadata."));
        }

        if (document.ContentRecord is not null &&
            string.IsNullOrWhiteSpace(document.ContentRecord.Presentation.Summary))
        {
            issues.Add(Warning("publish.summary_missing", document.RouteUrl, "Published content is missing a canonical summary for machine-readable previews."));
        }

        if (requirements.RequireEntities && document.EntityNames.Count == 0)
        {
            issues.Add(Warning("publish.entity_missing", document.RouteUrl, "Published content does not declare any entities."));
        }
    }

    private static PublishAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);
}
