namespace Bukit.Engine.Abstractions.Content;

public sealed record ContentModelSchema(
    IReadOnlyList<string>? ContentTypes = null,
    IReadOnlyList<string>? Statuses = null,
    IReadOnlyList<string>? ReviewStatuses = null,
    IReadOnlyList<string>? SyncStatuses = null,
    IReadOnlyDictionary<string, CanonicalFieldMapping>? CanonicalMappings = null,
    IReadOnlyDictionary<string, CustomFieldDefinition>? CustomFields = null,
    IReadOnlyDictionary<string, EntityMapping>? EntityMappings = null,
    IReadOnlyDictionary<string, RelationMapping>? RelationMappings = null,
    MediaPolicy? Media = null,
    bool RejectUnknownRawKeys = false,
    bool RequireSummary = false,
    bool RequireAuthor = false,
    bool RequireOrganization = false,
    bool RequireUpdatedAt = false,
    bool RequireProvenance = false,
    bool RequireReviewedAt = false,
    bool RequireMediaAlt = true,
    bool RequireMediaDescription = false,
    bool RequireMediaLicense = false,
    bool RequireEntityIds = false,
    bool RequireRelationTargets = true);

public sealed record CanonicalFieldMapping(
    string CanonicalField,
    string? RawKey = null,
    string? SemanticType = null,
    bool Required = false);

public sealed record CustomFieldDefinition(
    string Name,
    string FieldType,
    bool Required = false,
    string? SemanticType = null);

public sealed record EntityMapping(
    string RawKey,
    string EntityType,
    string? IdField = null,
    string? NameField = null,
    bool Required = false);

public sealed record RelationMapping(
    string RawKey,
    string RelationType,
    string? TargetType = null,
    bool Required = false);

public sealed record MediaPolicy(
    bool RequireAlt = true,
    bool RequireDescription = false,
    bool RequireLicense = false,
    IReadOnlyList<string>? AllowedKinds = null);

public interface IContentNormalizer
{
    ContentDocument Normalize(RawContentDocument raw, ContentModelSchema? schema = null);
}
