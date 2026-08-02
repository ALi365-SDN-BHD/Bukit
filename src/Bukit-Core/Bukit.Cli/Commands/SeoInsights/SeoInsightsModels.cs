using System.Text.Json.Serialization;

namespace Bukit.Cli.Commands.SeoInsights;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(SeoObservationWindow))]
[JsonSerializable(typeof(SeoObservationRow))]
[JsonSerializable(typeof(SeoObservationDataset))]
[JsonSerializable(typeof(SeoObservationRouteCandidate))]
[JsonSerializable(typeof(SeoObservationMetrics))]
[JsonSerializable(typeof(SeoInsightsSource))]
[JsonSerializable(typeof(SeoJoinCounts))]
[JsonSerializable(typeof(SeoProviderJoinQuality))]
[JsonSerializable(typeof(SeoJoinQuality))]
[JsonSerializable(typeof(SeoInsightsRoute))]
[JsonSerializable(typeof(SeoUnmatchedObservation))]
[JsonSerializable(typeof(SeoAmbiguousObservation))]
[JsonSerializable(typeof(SeoInsightsReport))]
internal sealed partial class SeoInsightsJsonContext : JsonSerializerContext;

internal sealed record SeoObservationWindow(
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone);

internal sealed record SeoObservationRow(
    string Url,
    long? Impressions,
    long? Clicks,
    double? AveragePosition,
    long? Sessions,
    long? EngagedSessions,
    long? KeyEvents);

internal sealed record SeoObservationDataset(
    string Schema,
    string SchemaVersion,
    string Provider,
    string Scope,
    DateTimeOffset CollectedAt,
    SeoObservationWindow Window,
    IReadOnlyList<SeoObservationRow> Rows);

internal sealed record SeoObservationMetrics(
    long? Impressions,
    long? Clicks,
    double? AveragePosition,
    double? Ctr,
    long? Sessions,
    long? EngagedSessions,
    long? KeyEvents,
    double? EngagementRate,
    double? KeyEventRate);

internal sealed record SeoInsightsSource(
    string Provider,
    string Scope,
    DateTimeOffset CollectedAt,
    long RowCount);

internal sealed record SeoJoinCounts(
    long Total,
    long Matched,
    long Unmatched,
    long Ambiguous);

internal sealed record SeoProviderJoinQuality(
    string Provider,
    SeoJoinCounts Counts);

internal sealed record SeoJoinQuality(
    SeoJoinCounts Overall,
    IReadOnlyList<SeoProviderJoinQuality> Providers);

internal sealed record SeoInsightsRoute(
    string RouteKey,
    string? ContentKey,
    string Route,
    string Canonical,
    SeoObservationMetrics Metrics);

internal sealed record SeoUnmatchedObservation(
    string Provider,
    string Scope,
    string OriginalUrl,
    string? NormalizedUrl,
    string? ErrorCode,
    SeoObservationMetrics Metrics);

internal sealed record SeoAmbiguousObservation(
    string Provider,
    string Scope,
    string OriginalUrl,
    string NormalizedUrl,
    SeoObservationMetrics Metrics,
    IReadOnlyList<SeoObservationRouteCandidate> Candidates);

internal sealed record SeoInsightsReport(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    SeoObservationWindow Window,
    IReadOnlyList<SeoInsightsSource> Sources,
    SeoJoinQuality JoinQuality,
    IReadOnlyList<SeoInsightsRoute> Routes,
    IReadOnlyList<SeoUnmatchedObservation> Unmatched,
    IReadOnlyList<SeoAmbiguousObservation> Ambiguous);
