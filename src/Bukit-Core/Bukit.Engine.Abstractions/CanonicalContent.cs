namespace Bukit.Engine.Abstractions.Content;

public sealed record CanonicalContentGraph(
    IReadOnlyList<ContentRecord> Records,
    IReadOnlyList<EntityRecord> Entities,
    IReadOnlyList<ContentRelation> Relations,
    IReadOnlyList<ContentDocument> Documents)
{
    public static readonly CanonicalContentGraph Empty = new(
        Array.Empty<ContentRecord>(),
        Array.Empty<EntityRecord>(),
        Array.Empty<ContentRelation>(),
        Array.Empty<ContentDocument>());

    public CanonicalContentGraph(
        IReadOnlyList<ContentRecord> records,
        IReadOnlyList<EntityRecord> entities)
        : this(records, entities, records.SelectMany(x => x.Relations).ToArray(), Array.Empty<ContentDocument>())
    {
    }
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
    string? Reviewer)
{
    public string? AuthorType { get; init; }
    public bool UsesAuthorRelation { get; init; }
    public IReadOnlyList<ContentAuthorProfile> AuthorProfiles { get; init; } = Array.Empty<ContentAuthorProfile>();
}

public sealed record ContentAuthorProfile(
    string? Id,
    string? Title,
    string? Slug,
    string? Type,
    string? Image,
    IReadOnlyList<string> SameAs);

internal sealed record ContentAuthorProjection(
    bool UsesAuthorRelation,
    IReadOnlyList<ContentAuthorProfile> Profiles);

internal static class ContentAuthorProfileProjectionReader
{
    internal static ContentAuthorProjection Read(IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (fields is null)
        {
            return new ContentAuthorProjection(false, Array.Empty<ContentAuthorProfile>());
        }

        var relation = fields.FirstOrDefault(static pair =>
            string.Equals(pair.Key, "authoredBy", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(relation.Key))
        {
            return new ContentAuthorProjection(false, Array.Empty<ContentAuthorProfile>());
        }

        var items = relation.Value.Value switch
        {
            null => Array.Empty<object>(),
            string id => new object[] { id },
            IEnumerable<object> values => values.ToArray(),
            _ => new[] { relation.Value.Value }
        };
        var profiles = items
            .Select(ReadProfile)
            .ToArray();
        return new ContentAuthorProjection(true, profiles);
    }

    private static ContentAuthorProfile ReadProfile(object? value)
    {
        if (value is not IReadOnlyDictionary<string, object?> map)
        {
            var id = Clean(value?.ToString());
            return new ContentAuthorProfile(id, null, null, null, null, Array.Empty<string>());
        }

        return new ContentAuthorProfile(
            Text(map, "id"),
            Text(map, "title"),
            Text(map, "slug"),
            Text(map, "type"),
            Text(map, "image"),
            TextList(map, "sameAs"));
    }

    private static string? Text(IReadOnlyDictionary<string, object?> map, string key)
        => Clean(map.FirstOrDefault(pair =>
            string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value?.ToString());

    private static IReadOnlyList<string> TextList(IReadOnlyDictionary<string, object?> map, string key)
    {
        var value = map.FirstOrDefault(pair =>
            string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        return ContentFieldReader.ToTextList(value) ?? Array.Empty<string>();
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ContentLifecycle(
    DateTimeOffset PublishedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ReviewedAt)
{
    public bool Evergreen { get; init; }
}

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
    IReadOnlyList<string>? SameAs = null)
{
    public LocalBusinessProfile? LocalBusinessProfile { get; init; }
}

public sealed record LocalBusinessProfile
{
    public bool AddressVerified { get; init; }
    public bool LocalOperationsVerified { get; init; }
    public string? StreetAddress { get; init; }
    public string? AddressLocality { get; init; }
    public string? AddressRegion { get; init; }
    public string? PostalCode { get; init; }
    public string? AddressCountry { get; init; }
    public string? LocalOperationsDescription { get; init; }

    public bool HasCompleteVerifiedLocalOperations =>
        AddressVerified &&
        LocalOperationsVerified &&
        !string.IsNullOrWhiteSpace(StreetAddress) &&
        !string.IsNullOrWhiteSpace(AddressLocality) &&
        !string.IsNullOrWhiteSpace(AddressRegion) &&
        !string.IsNullOrWhiteSpace(PostalCode) &&
        !string.IsNullOrWhiteSpace(AddressCountry) &&
        !string.IsNullOrWhiteSpace(LocalOperationsDescription);
}

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
