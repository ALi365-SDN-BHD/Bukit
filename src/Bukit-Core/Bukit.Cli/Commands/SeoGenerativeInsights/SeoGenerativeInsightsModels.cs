using System.Text.Json.Serialization;

namespace Bukit.Cli.Commands.SeoGenerativeInsights;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GenerativeAnswerObservationRow))]
[JsonSerializable(typeof(GenerativeAnswerObservationDataset))]
[JsonSerializable(typeof(GenerativeCitationSource))]
[JsonSerializable(typeof(GenerativeEngineStats))]
[JsonSerializable(typeof(GenerativeRouteCitation))]
[JsonSerializable(typeof(GenerativeQuestionStats))]
[JsonSerializable(typeof(GenerativeUnmatchedCitedUrl))]
[JsonSerializable(typeof(GenerativeAmbiguousCitedUrl))]
[JsonSerializable(typeof(GenerativeExternalCitedUrl))]
[JsonSerializable(typeof(GenerativeJoinQuality))]
[JsonSerializable(typeof(GenerativeStats))]
[JsonSerializable(typeof(GenerativeCitationReport))]
internal sealed partial class SeoGenerativeInsightsJsonContext : JsonSerializerContext;

internal sealed record GenerativeAnswerObservationRow(
    string QuestionKey,
    int PromptVariant,
    int RunIndex,
    bool BrandMentioned,
    bool SiteCited,
    IReadOnlyList<string> CitedUrls,
    long? CitationPosition,
    string AnswerHash);

internal sealed record GenerativeAnswerObservationDataset(
    string Schema,
    string SchemaVersion,
    string Engine,
    string PromptSetVersion,
    string Locale,
    DateTimeOffset CollectedAt,
    string CollectionMethod,
    IReadOnlyList<GenerativeAnswerObservationRow> Rows);

internal sealed record GenerativeCitationSource(
    string Engine,
    string PromptSetVersion,
    string Locale,
    DateTimeOffset CollectedAt,
    string CollectionMethod,
    string Path,
    long RowCount);

internal sealed record GenerativeStats(
    long Runs,
    long BrandMentions,
    double? BrandMentionRate,
    long SiteCitations,
    double? SiteCitationRate);

internal sealed record GenerativeEngineStats(
    string Engine,
    long Runs,
    long BrandMentions,
    double? BrandMentionRate,
    long SiteCitations,
    double? SiteCitationRate);

internal sealed record GenerativeRouteCitation(
    string RouteKey,
    string Canonical,
    long CitationRuns);

internal sealed record GenerativeQuestionStats(
    string QuestionKey,
    long Runs,
    long BrandMentions,
    double? BrandMentionRate,
    long SiteCitations,
    double? SiteCitationRate,
    IReadOnlyList<GenerativeRouteCitation> Routes);

internal sealed record GenerativeUnmatchedCitedUrl(
    string Url,
    string? ErrorCode);

internal sealed record GenerativeAmbiguousCitedUrl(
    string Url,
    IReadOnlyList<string> CandidateRouteKeys);

internal sealed record GenerativeExternalCitedUrl(
    string Url,
    long CitationRuns);

internal sealed record GenerativeJoinQuality(
    long SourceRows,
    long MatchedRows,
    long UnmatchedRows,
    long AmbiguousRows);

internal sealed record GenerativeCitationReport(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<GenerativeCitationSource> Sources,
    GenerativeStats Overall,
    IReadOnlyList<GenerativeEngineStats> Engines,
    IReadOnlyList<GenerativeQuestionStats> Questions,
    IReadOnlyList<GenerativeUnmatchedCitedUrl> UnmatchedCitedUrls,
    IReadOnlyList<GenerativeAmbiguousCitedUrl> AmbiguousCitedUrls,
    IReadOnlyList<GenerativeExternalCitedUrl> ExternalCitedUrls,
    GenerativeJoinQuality JoinQuality);

internal sealed record GenerativeCitedUrlClassification(
    string Url,
    string Kind,
    string? ErrorCode);

internal sealed record GenerativeRowValidation(
    IReadOnlyList<GenerativeCitedUrlClassification> CitedUrls);

internal sealed record GenerativeAnswerObservationValidation(
    IReadOnlyList<GenerativeRowValidation> Rows);
