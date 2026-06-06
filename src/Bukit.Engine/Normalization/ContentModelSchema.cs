using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.Normalization;

public sealed record ContentModelSchema(
    IReadOnlyDictionary<string, CanonicalFieldMapping> CanonicalMappings,
    IReadOnlyDictionary<string, CustomFieldDefinition> CustomFields,
    IReadOnlyDictionary<string, EntityMapping> EntityMappings,
    IReadOnlyDictionary<string, RelationMapping> RelationMappings)
{
    public static readonly ContentModelSchema Default = new(
        new Dictionary<string, CanonicalFieldMapping>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, CustomFieldDefinition>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, EntityMapping>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, RelationMapping>(StringComparer.OrdinalIgnoreCase));
}

public sealed record CanonicalFieldMapping(
    string Source,
    string Target,
    string? ValueType = null,
    bool Required = false);

public sealed record CustomFieldDefinition(
    string Name,
    string ValueType,
    bool Required = false);

public sealed record EntityMapping(
    string Source,
    string EntityType);

public sealed record RelationMapping(
    string Source,
    string RelationType);
