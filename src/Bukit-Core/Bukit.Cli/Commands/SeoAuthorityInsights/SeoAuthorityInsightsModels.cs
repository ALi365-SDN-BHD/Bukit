using System.Text.Json.Serialization;

namespace Bukit.Cli.Commands.SeoAuthorityInsights;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ExternalAuthorityObservationDataset))]
[JsonSerializable(typeof(ExternalAuthorityReport))]
internal sealed partial class SeoAuthorityInsightsJsonContext : JsonSerializerContext;

internal sealed record ExternalAuthorityObservationRow(
    string SourceUrl,
    string SourceType,
    DateTimeOffset ObservedAt,
    string Status,
    string? QuestionKey,
    string? TopicKey,
    string? EntityKey,
    string ContextHash,
    IReadOnlyList<string> CitedUrls);

internal sealed record ExternalAuthorityObservationDataset(
    string Schema,
    string SchemaVersion,
    string Provider,
    DateTimeOffset CollectedAt,
    string CollectionMethod,
    IReadOnlyList<ExternalAuthorityObservationRow> Rows);

internal sealed record ExternalAuthoritySourceRecord(
    string Provider,
    string SourceType,
    string Status,
    DateTimeOffset ObservedAt,
    string SourceUrl,
    string ContextHash,
    IReadOnlyList<string> CitedRouteKeys);

internal sealed record ExternalAuthorityOverall(
    long Sources,
    long ActiveSources,
    long ActiveCitedRoutes);

internal sealed record ExternalAuthorityProviderCounts(
    string Provider,
    long Sources,
    long ActiveSources);

internal sealed record ExternalAuthoritySourceTypeCounts(
    string SourceType,
    long Sources,
    long ActiveSources);

internal sealed record ExternalAuthorityStatusCounts(
    string Status,
    long Sources);

internal sealed record ExternalAuthorityRouteCitation(
    string RouteKey,
    string Canonical,
    long ActiveSources);

internal sealed record ExternalAuthorityUnmatchedCitedUrl(
    string Url,
    string? ErrorCode);

internal sealed record ExternalAuthorityAmbiguousCitedUrl(
    string Url,
    IReadOnlyList<string> CandidateRouteKeys);

internal sealed record ExternalAuthorityJoinQuality(
    long SourceRows,
    long MatchedRows,
    long UnmatchedRows,
    long AmbiguousRows);

internal sealed record ExternalAuthorityReport(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ExternalAuthoritySourceRecord> Sources,
    ExternalAuthorityOverall Overall,
    IReadOnlyList<ExternalAuthorityProviderCounts> Providers,
    IReadOnlyList<ExternalAuthoritySourceTypeCounts> SourceTypes,
    IReadOnlyList<ExternalAuthorityStatusCounts> Statuses,
    IReadOnlyList<ExternalAuthorityRouteCitation> Routes,
    IReadOnlyList<ExternalAuthorityUnmatchedCitedUrl> UnmatchedCitedUrls,
    IReadOnlyList<ExternalAuthorityAmbiguousCitedUrl> AmbiguousCitedUrls,
    ExternalAuthorityJoinQuality JoinQuality);
