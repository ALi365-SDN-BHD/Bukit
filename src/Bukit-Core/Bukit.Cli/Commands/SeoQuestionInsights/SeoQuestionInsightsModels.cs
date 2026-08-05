using System.Text.Json.Serialization;
using Bukit.Cli.Commands.SeoInsights;

namespace Bukit.Cli.Commands.SeoQuestionInsights;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SeoQuestionTarget))]
[JsonSerializable(typeof(SeoQuestionTargetMap))]
[JsonSerializable(typeof(SearchQuestionObservationRow))]
[JsonSerializable(typeof(SearchQuestionObservationDataset))]
[JsonSerializable(typeof(SeoQuestionInsightsSource))]
[JsonSerializable(typeof(SeoQuestionJoinCounts))]
[JsonSerializable(typeof(SeoQuestionJoinQuality))]
[JsonSerializable(typeof(SeoQuestionRouteCoverage))]
[JsonSerializable(typeof(SeoQuestionCoverage))]
[JsonSerializable(typeof(SeoQuestionUnmatchedTarget))]
[JsonSerializable(typeof(SeoQuestionUnmatchedObservation))]
[JsonSerializable(typeof(SeoQuestionAmbiguousObservation))]
[JsonSerializable(typeof(SeoQuestionInsightsReport))]
internal sealed partial class SeoQuestionInsightsJsonContext : JsonSerializerContext;

internal sealed record SeoQuestionTarget(
    string QuestionKey,
    string TopicKey,
    string Intent,
    string Locale,
    string Priority,
    IReadOnlyList<string> CoveredRouteKeys);

internal sealed record SeoQuestionTargetMap(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SeoQuestionTarget> Questions);

internal sealed record SearchQuestionObservationRow(
    string QuestionKey,
    string TopicKey,
    string Url,
    string Locale,
    string Device,
    long Impressions,
    long Clicks,
    double AveragePosition);

internal sealed record SearchQuestionObservationDataset(
    string Schema,
    string SchemaVersion,
    string Provider,
    string Scope,
    DateTimeOffset CollectedAt,
    string CollectionMethod,
    SeoObservationWindow Window,
    IReadOnlyList<SearchQuestionObservationRow> Rows);

internal sealed record SeoQuestionInsightsSource(
    string Provider,
    string Scope,
    string CollectionMethod,
    DateTimeOffset CollectedAt,
    string Path);

internal sealed record SeoQuestionJoinCounts(
    long SourceRows,
    long MatchedRows,
    long UnmatchedRows,
    long AmbiguousRows);

internal sealed record SeoQuestionJoinQuality(
    SeoQuestionJoinCounts Overall,
    SeoQuestionJoinCounts Targets,
    SeoQuestionJoinCounts Observations);

internal sealed record SeoQuestionRouteCoverage(
    string RouteKey,
    string Canonical,
    long Impressions,
    long Clicks,
    double? Ctr,
    double AveragePosition);

internal sealed record SeoQuestionCoverage(
    string QuestionKey,
    string TopicKey,
    string Intent,
    string Locale,
    string Priority,
    long TotalImpressions,
    long TotalClicks,
    IReadOnlyList<SeoQuestionRouteCoverage> Routes);

internal sealed record SeoQuestionUnmatchedTarget(
    string QuestionKey,
    string RouteKey,
    string ErrorCode);

internal sealed record SeoQuestionUnmatchedObservation(
    string QuestionKey,
    string Url,
    string? ErrorCode);

internal sealed record SeoQuestionAmbiguousObservation(
    string QuestionKey,
    string Url,
    IReadOnlyList<string> CandidateRouteKeys);

internal sealed record SeoQuestionInsightsReport(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    SeoObservationWindow Window,
    IReadOnlyList<SeoQuestionInsightsSource> Sources,
    SeoQuestionJoinQuality JoinQuality,
    IReadOnlyList<SeoQuestionCoverage> Questions,
    IReadOnlyList<SeoQuestionUnmatchedTarget> UnmatchedTargets,
    IReadOnlyList<SeoQuestionUnmatchedObservation> UnmatchedObservations,
    IReadOnlyList<SeoQuestionAmbiguousObservation> AmbiguousObservations);
