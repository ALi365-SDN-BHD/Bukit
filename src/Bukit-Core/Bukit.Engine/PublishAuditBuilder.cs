namespace Bukit.Engine;

internal static class PublishAuditBuilder
{
    internal static PublishAuditReport Build(SeoAuditReport report)
        => Build(report, Array.Empty<PublishDocument>(), null);

    internal static PublishAuditReport Build(SeoAuditReport report, IReadOnlyList<PublishDocument> publishDocuments)
        => Build(report, publishDocuments, null);

    internal static PublishAuditReport Build(SeoAuditReport report, IReadOnlyList<PublishDocument> publishDocuments, string? outputDir)
        => Build(report, publishDocuments, outputDir, report.Issues.Select(PublishAuditIssue.FromSeoIssue).ToArray());

    internal static PublishAuditReport Build(
        SeoAuditReport report,
        IReadOnlyList<PublishDocument> publishDocuments,
        string? outputDir,
        IReadOnlyList<PublishAuditIssue> issues)
    {
        var documentsByRoute = publishDocuments.ToDictionary(x => x.RouteUrl, StringComparer.OrdinalIgnoreCase);
        var documents = report.Routes.Select(route =>
        {
            documentsByRoute.TryGetValue(route.Url, out var document);
            return new PublishAuditDocument(
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
            Summary: document?.Summary,
            UpdatedAt: document?.UpdatedAt,
            SourceReferences: document?.SourceReferences ?? Array.Empty<string>(),
            EntityNames: route.EntityNames ?? Array.Empty<string>(),
            EntitySummaries: document?.EntitySummaries ?? Array.Empty<PublishEntitySummary>(),
            RepresentationKinds: route.RepresentationKinds ?? Array.Empty<string>(),
            Representations: BuildRepresentations(route, document, outputDir),
            SchemaTypes: route.SchemaTypes,
            StructuredDataTypes: route.SchemaTypes,
            SemanticOutline: document?.SemanticOutline ?? Array.Empty<PublishSemanticOutlineItem>(),
            SitemapIncluded: route.SitemapIncluded,
            SearchIncluded: route.SearchIncluded,
            RssIncluded: route.RssIncluded,
            AtomFeedIncluded: document?.AtomFeedIncluded ?? false,
            JsonFeedIncluded: document?.JsonFeedIncluded ?? false,
            LlmsIncluded: document?.LlmsIncluded ?? false,
            LlmsFullIncluded: document?.LlmsFullIncluded ?? false,
            RobotsIncluded: document?.RobotsIncluded ?? false,
            ManifestIncluded: document?.ManifestIncluded ?? false);
        }).ToArray();

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
            Issues: issues,
            Summary: summary);
    }

    private static IReadOnlyList<PublishRepresentationInventoryItem> BuildRepresentations(
        SeoAuditRoute route,
        PublishDocument? document,
        string? outputDir)
    {
        var record = document?.ContentRecord;
        return (route.RepresentationKinds ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(kind => BuildRepresentation(route, document, record, kind, outputDir))
            .ToArray();
    }

    private static PublishRepresentationInventoryItem BuildRepresentation(
        SeoAuditRoute route,
        PublishDocument? document,
        Engine.Abstractions.Content.ContentRecord? record,
        string kind,
        string? outputDir)
    {
        var (url, path) = ResolveRepresentationLocation(route, record, kind);
        var generated = IsAggregateKind(kind)
            ? IsAggregateIncluded(route, document, kind)
            : outputDir is not null && !string.IsNullOrWhiteSpace(path)
            ? File.Exists(Path.Combine(outputDir, path))
            : IsAggregateIncluded(route, document, kind);
        return new PublishRepresentationInventoryItem(kind, url, path, generated, route.Indexable);
    }

    private static (string Url, string Path) ResolveRepresentationLocation(
        SeoAuditRoute route,
        Engine.Abstractions.Content.ContentRecord? record,
        string kind)
    {
        if (string.Equals(kind, "html", StringComparison.OrdinalIgnoreCase))
        {
            return (route.Url, route.OutputPath.Replace('\\', '/'));
        }

        if (string.Equals(kind, "semantic-html", StringComparison.OrdinalIgnoreCase))
        {
            return (route.Url, route.OutputPath.Replace('\\', '/'));
        }

        if (record is not null && string.Equals(kind, "json", StringComparison.OrdinalIgnoreCase))
        {
            return (DefaultContentProjectionWriter.GetContentProjectionUrl(record, ".json"), ToProjectionPath(record, ".json"));
        }

        if (record is not null && string.Equals(kind, "markdown", StringComparison.OrdinalIgnoreCase))
        {
            return (DefaultContentProjectionWriter.GetContentProjectionUrl(record, ".md"), ToProjectionPath(record, ".md"));
        }

        if (string.Equals(kind, "jsonld", StringComparison.OrdinalIgnoreCase))
        {
            return (route.Canonical, string.Empty);
        }

        var aggregate = PublishRepresentationRegistry.AggregateRepresentations()
            .FirstOrDefault(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase));
        return aggregate is null ? (route.Url, string.Empty) : ("/" + aggregate.Path.Replace('\\', '/'), aggregate.Path.Replace('\\', '/'));
    }

    private static string ToProjectionPath(Engine.Abstractions.Content.ContentRecord record, string extension)
        => DefaultContentProjectionWriter.GetContentProjectionUrl(record, extension).TrimStart('/');

    private static bool IsAggregateIncluded(SeoAuditRoute route, PublishDocument? document, string kind)
        => kind.ToLowerInvariant() switch
        {
            "sitemap" => route.SitemapIncluded,
            "search" => route.SearchIncluded,
            "feed" => route.RssIncluded,
            "rss" => route.RssIncluded,
            "atom" => document?.AtomFeedIncluded ?? false,
            "jsonfeed" => document?.JsonFeedIncluded ?? false,
            "llms" => document?.LlmsIncluded ?? false,
            "llms-full" => document?.LlmsFullIncluded ?? false,
            "robots" => document?.RobotsIncluded ?? false,
            "agent-manifest" => document?.ManifestIncluded ?? false,
            _ => false
        };

    private static bool IsAggregateKind(string kind)
        => PublishRepresentationRegistry.AggregateRepresentations()
            .Any(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase));
}
