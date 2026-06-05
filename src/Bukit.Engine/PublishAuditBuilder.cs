namespace Bukit.Engine;

internal static class PublishAuditBuilder
{
    internal static PublishAuditReport Build(SeoAuditReport report)
    {
        var documents = report.Routes.Select(route => new PublishAuditDocument(
            RouteUrl: route.Url,
            OutputPath: route.OutputPath,
            Canonical: route.Canonical,
            Indexable: route.Indexable,
            LastModified: route.LastModified,
            ContentType: route.ContentType,
            SourceItemId: route.SourceItemId,
            Title: route.Title,
            Description: route.Description,
            Language: route.Language,
            Author: route.Author,
            Organization: route.Organization,
            Source: route.Source,
            OriginalSource: route.OriginalSource,
            ReviewStatus: route.ReviewStatus,
            EntityNames: route.EntityNames ?? Array.Empty<string>(),
            RepresentationKinds: route.RepresentationKinds ?? Array.Empty<string>(),
            SchemaTypes: route.SchemaTypes,
            SitemapIncluded: route.SitemapIncluded,
            SearchIncluded: route.SearchIncluded,
            RssIncluded: route.RssIncluded)).ToArray();

        var summary = new PublishAuditSummary(
            DocumentCount: documents.Length,
            IndexableCount: documents.Count(x => x.Indexable),
            NonIndexableCount: documents.Count(x => !x.Indexable),
            ErrorCount: report.Summary.ErrorCount,
            WarningCount: report.Summary.WarningCount,
            PublishIssueCount: report.Summary.PublishIssueCount,
            MachineReadabilityIssueCount: report.Summary.MachineReadabilityIssueCount,
            TrustIssueCount: report.Summary.TrustIssueCount,
            RepresentationGapCount: report.Summary.RepresentationGapCount);

        return new PublishAuditReport(
            Schema: PublishAuditReportWriter.Schema,
            SchemaVersion: SeoAuditModels.ReportSchemaVersion,
            GeneratedAt: report.GeneratedAt,
            SiteName: report.SiteName,
            SiteUrl: report.SiteUrl,
            BaseUrl: report.BaseUrl,
            Documents: documents,
            Issues: report.Issues,
            Summary: summary);
    }
}
