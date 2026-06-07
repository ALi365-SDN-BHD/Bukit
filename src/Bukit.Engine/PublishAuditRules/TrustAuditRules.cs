namespace Bukit.Engine.PublishAuditRules;

internal static class TrustAuditRules
{
    internal static void Analyze(PublishDocument document, List<PublishAuditIssue> issues)
    {
        if (!document.Indexable ||
            string.Equals(document.ContentType, "list", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(document.Author))
        {
            issues.Add(Warning("publish.author_missing", document.RouteUrl, "Published content is missing author metadata."));
        }

        if (string.IsNullOrWhiteSpace(document.Source))
        {
            issues.Add(Warning("publish.source_missing", document.RouteUrl, "Published content is missing source/provenance metadata."));
        }

        if (document.ContentRecord is not null &&
            string.IsNullOrWhiteSpace(document.ContentRecord.Provenance.OriginalSource) &&
            document.ContentRecord.Provenance.Citations.Count == 0 &&
            document.ContentRecord.Provenance.References.Count == 0)
        {
            issues.Add(Warning("publish.source_references_missing", document.RouteUrl, "Published content is missing source references, citations, or original source metadata."));
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

        if (document.EntityNames.Count == 0)
        {
            issues.Add(Warning("publish.entity_missing", document.RouteUrl, "Published content does not declare any entities."));
        }
        else if (document.ContentRecord is not null &&
                 document.ContentRecord.Entities.Any(entity => string.IsNullOrWhiteSpace(entity.Description)))
        {
            issues.Add(Warning("publish.entity_summary_missing", document.RouteUrl, "Published content declares entities without machine-readable summaries."));
        }
    }

    private static PublishAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);
}
