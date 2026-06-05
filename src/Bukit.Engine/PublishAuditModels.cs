using System.Text.Json.Serialization;

namespace Bukit.Engine;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(PublishAuditReport))]
internal sealed partial class PublishAuditReportJsonContext : JsonSerializerContext;

internal sealed record PublishAuditReport(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string SiteName,
    string? SiteUrl,
    string BaseUrl,
    IReadOnlyList<PublishAuditDocument> Documents,
    IReadOnlyList<SeoAuditIssue> Issues,
    PublishAuditSummary Summary);

internal sealed record PublishAuditDocument(
    string RouteUrl,
    string OutputPath,
    string Canonical,
    bool Indexable,
    DateTimeOffset LastModified,
    string? ContentType,
    string? SourceItemId,
    string? Title,
    string? Description,
    string? Language,
    string? Author,
    string? Organization,
    string? Source,
    string? OriginalSource,
    string? ReviewStatus,
    string? Summary,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<string> SourceReferences,
    IReadOnlyList<string> EntityNames,
    IReadOnlyList<PublishEntitySummary> EntitySummaries,
    IReadOnlyList<string> RepresentationKinds,
    IReadOnlyList<string> SchemaTypes,
    IReadOnlyList<string> StructuredDataTypes,
    IReadOnlyList<PublishSemanticOutlineItem> SemanticOutline,
    bool SitemapIncluded,
    bool SearchIncluded,
    bool RssIncluded,
    bool JsonFeedIncluded,
    bool ManifestIncluded);

internal sealed record PublishAuditSummary(
    int DocumentCount,
    int IndexableCount,
    int NonIndexableCount,
    int ErrorCount,
    int WarningCount,
    int PublishIssueCount,
    int MachineReadabilityIssueCount,
    int TrustIssueCount,
    int RepresentationGapCount);
