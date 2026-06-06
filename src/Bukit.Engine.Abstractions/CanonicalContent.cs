namespace Bukit.Engine.Abstractions.Content;

public sealed record CanonicalContentGraph(
    IReadOnlyList<ContentRecord> Records,
    IReadOnlyList<EntityRecord> Entities)
{
    public static readonly CanonicalContentGraph Empty = new(Array.Empty<ContentRecord>(), Array.Empty<EntityRecord>());
}

public sealed record ContentRecord(
    ContentIdentity Identity,
    ContentPresentation Presentation,
    ContentClassification Classification,
    ContentOwnership Ownership,
    ContentLifecycle Lifecycle,
    ProvenanceRecord Provenance,
    TrustMetadata Trust,
    IReadOnlyList<EntityRecord> Entities,
    IReadOnlyList<ContentRelation> Relations,
    IReadOnlyList<MediaAsset> Media);

public sealed record ContentIdentity(
    string Id,
    string Slug,
    string CanonicalUrlKey,
    string ContentType,
    string Status);

public sealed record ContentPresentation(
    string Title,
    string? Summary,
    string? Body,
    string Language,
    IReadOnlyList<string> Translations);

public sealed record ContentClassification(
    string Type,
    string Collection,
    IReadOnlyList<string> Sections,
    IReadOnlyList<string> Tags);

public sealed record ContentOwnership(
    string? Author,
    string? Organization,
    string? Owner,
    string? Reviewer);

public sealed record ContentLifecycle(
    DateTimeOffset PublishedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ReviewedAt);

public sealed record ProvenanceRecord(
    string? Source,
    string? OriginalSource,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> References,
    string? SyncStatus);

public sealed record TrustMetadata(
    double? CredibilityScore,
    string ReviewStatus,
    IReadOnlyList<string> QualityFlags);

public sealed record EntityRecord(
    string Type,
    string Name,
    string? Description = null,
    string? Id = null,
    string? Url = null,
    IReadOnlyList<string>? SameAs = null);

public sealed record ContentRelation(
    string Type,
    string Target,
    string? TargetType = null,
    string? TargetId = null);

public sealed record MediaAsset(
    string Kind,
    string Url,
    string? Alt = null,
    string? Caption = null,
    string? Description = null,
    string? License = null);
