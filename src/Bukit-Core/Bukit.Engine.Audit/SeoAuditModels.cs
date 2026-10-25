using System.Text.Json.Serialization;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static class SeoAuditModels
{
    internal const string ReportSchemaVersion = "1.0";
    internal const string ReportSchema = "https://bukit.dev/schemas/seo-report.v1.json";
    internal const string PublishAuditSchema = "https://bukit.dev/schemas/publish-audit-report.v1.json";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SeoAuditReport))]
internal sealed partial class SeoAuditReportJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(GeoReport))]
internal sealed partial class GeoReportJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(AgentManifest))]
internal sealed partial class AgentManifestJsonContext : JsonSerializerContext;

internal sealed record AgentManifest(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AgentManifestDocument> Documents);

internal sealed record AgentManifestDocument(
    string Id,
    string CanonicalId,
    string Route,
    string? Language,
    string? ReviewStatus,
    string? Source,
    IReadOnlyList<string> Entities,
    IReadOnlyList<AgentManifestRepresentation> Representations);

internal sealed record AgentManifestRepresentation(string Kind, string Url);

internal sealed record GeoReport(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    int GeoScore,
    bool LlmsTxtGenerated,
    bool LlmsFullTxtGenerated,
    int GeoEnhancedCount,
    IReadOnlyList<GeoRouteEntry> GeoEnhancedRoutes);

internal sealed record GeoRouteEntry(string Url, IReadOnlyList<string> SchemaTypes);

internal sealed record SeoAuditReport(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string SiteName,
    string? SiteUrl,
    string BaseUrl,
    IReadOnlyList<SeoAuditRoute> Routes,
    IReadOnlyList<SeoAuditIssue> Issues,
    SeoAuditSummary Summary);

internal sealed record SeoAuditRoute(
    string Url,
    string OutputPath,
    string? Title,
    string? Description,
    string Canonical,
    string? Robots,
    bool Indexable,
    DateTimeOffset? LastModified,
    string? ContentType,
    string? SourceItemId,
    bool SitemapIncluded,
    bool SearchIncluded,
    bool RssIncluded,
    IReadOnlyList<SeoAlternateModel> Alternates,
    IReadOnlyList<string> SchemaTypes,
    string? Language = null,
    string? Author = null,
    string? Organization = null,
    string? Source = null,
    string? OriginalSource = null,
    string? ReviewStatus = null,
    IReadOnlyList<string>? EntityNames = null,
    IReadOnlyList<string>? RepresentationKinds = null);

internal sealed record SeoAuditIssue(string Severity, string Code, string? Route, string Message);

internal sealed record SeoAuditSummary(
    int RouteCount,
    int IndexableCount,
    int NonIndexableCount,
    int ErrorCount,
    int WarningCount,
    bool LlmsTxtGenerated,
    bool LlmsFullTxtGenerated,
    int GeoEnhancedCount,
    int GeoScore,
    int PublishIssueCount = 0,
    int MachineReadabilityIssueCount = 0,
    int TrustIssueCount = 0,
    int RepresentationGapCount = 0);
