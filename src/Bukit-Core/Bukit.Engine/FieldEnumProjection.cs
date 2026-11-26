using Bukit.Config;

namespace Bukit.Engine;

/// <summary>
/// <c>site.field_enums</c>: the enum options of <c>content.modelSchema</c> fields in definition
/// order, each <c>{ value, label }</c> (label falls back to the value). Fields of
/// <c>customFields</c> sit at <c>site.field_enums.&lt;field&gt;</c>; fields of a field scope at
/// <c>site.field_enums.scopes.&lt;scope&gt;.&lt;field&gt;</c>.
/// </summary>
internal static class FieldEnumProjection
{
    internal const string ScopesKey = "scopes";

    internal static Dictionary<string, object>? Build(ContentModelSchemaConfig? schema)
    {
        if (schema is null)
        {
            return null;
        }

        var result = Fields(schema.CustomFields);
        var scopes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (scope, fields) in schema.FieldScopes ?? new Dictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>())
        {
            if (Fields(fields) is { Count: > 0 } scoped)
            {
                scopes[scope] = scoped;
            }
        }

        if (scopes.Count > 0)
        {
            result[ScopesKey] = scopes;
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, object> Fields(IReadOnlyList<CustomFieldDefinitionConfig>? fields)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields ?? [])
        {
            if (field.Enum is { Count: > 0 } values)
            {
                result[field.Name] = values
                    .Select(value => (object)new SortedDictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["label"] = field.EnumLabels is not null && field.EnumLabels.TryGetValue(value, out var label) ? label : value,
                        ["value"] = value
                    })
                    .ToList();
            }
        }

        return result;
    }
}
