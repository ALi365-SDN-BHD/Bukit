using System.Text.Json.Serialization;
using Bukit.Shared;

namespace Bukit.WechatSyncing;

public sealed record WechatSyncField(string Type, object? Value);

public sealed record WechatSyncItem(
    string Id,
    string? Title,
    string Slug,
    DateTimeOffset PublishAt,
    string? ContentHtml,
    IReadOnlyDictionary<string, object> Metadata,
    IReadOnlyDictionary<string, WechatSyncField> Fields);

public sealed record WechatSyncRoute(
    string Url,
    string OutputPath,
    string Template);

public sealed class WechatSyncContext
{
    public required string RootDir { get; init; }
    public required string OutputDir { get; init; }
    public required string BaseUrl { get; init; }
    public required string SiteName { get; init; }
    public string? SiteUrl { get; init; }
    public string? MediaDownloadDir { get; init; }
    public required IReadOnlyList<(WechatSyncItem Item, WechatSyncRoute Route)> Routed { get; init; }
    public required ILogger Logger { get; init; }
}

public sealed record WechatSyncOptions(
    HashSet<string> SourceNames,
    HashSet<string> ContentTypes,
    HashSet<string> DefaultTypesWhenMissing,
    string CacheFile,
    int MaxAttempts,
    int BaseDelayMs,
    int BackoffFactor,
    string AppIdEnv,
    string AppSecretEnv,
    string ForceRetryIgnoreCacheEnv,
    string? Author,
    string? DefaultThumbMediaId,
    bool NeedOpenComment,
    bool OnlyFansCanComment,
    string SiteName,
    string? SiteUrl,
    string BaseUrl,
    bool ProcessImages = false,
    bool Passthrough = false,
    string Target = "draft",
    int PublishPollMaxAttempts = 10,
    int PublishPollIntervalSeconds = 5,
    bool Force = false,
    string? DefaultImageUrl = null)
{
    public HashSet<string>? DraftReviewStatuses { get; init; }
    public HashSet<string>? PublishReviewStatuses { get; init; }
}

public sealed record WechatSyncResult(
    bool Success,
    int Candidates,
    int Synced,
    int Skipped,
    IReadOnlyList<WechatSyncMessage> Messages,
    IReadOnlyList<WechatSyncDiagnostic> Diagnostics,
    string CachePath);

public sealed record WechatSyncMessage(string Level, string Message);

public sealed record WechatSyncDiagnostic(string Code, string Severity, string Message, string? Path = null);

internal sealed record WechatSyncCandidate(
    string SyncKey,
    string SourceKey,
    string SourceId,
    WechatSyncItem Item,
    WechatSyncRoute Route,
    DateTimeOffset? ExpiresAt);

internal sealed record WechatSyncPlanExclusion(
    string Code,
    string Severity,
    string Message,
    string? Path);

internal sealed record WechatSyncPlan(
    IReadOnlyList<WechatSyncCandidate> Candidates,
    IReadOnlyList<WechatSyncPlanExclusion> Exclusions)
{
    internal bool HasErrors
        => Exclusions.Any(exclusion => exclusion.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
}

internal static class ContentBodyResolver
{
    internal static string GetHtml(WechatSyncItem item)
        => item.ContentHtml ?? string.Empty;
}

internal sealed record ContentProjectionAgentManifest(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ContentProjectionAgentManifestDocument> Documents);

internal sealed record ContentProjectionAgentManifestDocument(
    string Id,
    string CanonicalId,
    string Route,
    string Language,
    string ReviewStatus,
    string? Source,
    IReadOnlyList<string> Entities,
    IReadOnlyList<ContentProjectionRepresentation> Representations,
    DateTimeOffset PublishedAt);

internal sealed record ContentProjectionRepresentation(string Kind, string Url);

internal sealed record ContentProjectionDocument(
    string Id,
    string Slug,
    string CanonicalUrlKey,
    string Route,
    string Title,
    string? Summary,
    string? Body,
    string Language,
    string Type,
    string Collection,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Sections,
    string? Author,
    string? Organization,
    DateTimeOffset PublishedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ReviewedAt,
    string? Source,
    string? OriginalSource,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> References,
    string? SyncStatus,
    string ReviewStatus,
    double? CredibilityScore,
    IReadOnlyList<string> QualityFlags,
    IReadOnlyList<ContentProjectionMediaAsset> Media,
    string? Canonical);

internal sealed record ContentProjectionMediaAsset(
    string Kind,
    string Url,
    string? Alt = null,
    string? Caption = null,
    string? Description = null,
    string? License = null);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ContentProjectionAgentManifest))]
[JsonSerializable(typeof(ContentProjectionDocument))]
internal sealed partial class WechatSyncInputJsonContext : JsonSerializerContext;
