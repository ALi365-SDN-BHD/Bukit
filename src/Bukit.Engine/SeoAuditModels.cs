using System.Text.Json.Serialization;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static class SeoAuditModels
{
    internal const string ReportSchemaVersion = "1.0";
    internal const string ReportSchema = "https://bukit.dev/schemas/seo-report.v1.json";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SeoAuditReport))]
internal sealed partial class SeoAuditReportJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(GeoReport))]
internal sealed partial class GeoReportJsonContext : JsonSerializerContext;

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
    DateTimeOffset LastModified,
    string? ContentType,
    string? SourceItemId,
    bool SitemapIncluded,
    bool SearchIncluded,
    bool RssIncluded,
    IReadOnlyList<SeoAlternateModel> Alternates,
    IReadOnlyList<string> SchemaTypes);

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
    int GeoScore);
