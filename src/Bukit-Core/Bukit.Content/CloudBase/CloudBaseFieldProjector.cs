using System.Text.Json;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content.CloudBase;

/// <summary>
/// Projects row columns into public content fields. Personal and operator columns are always dropped,
/// including in <c>mode: all</c>, because configuration must not be trusted to hide them (D7).
/// </summary>
internal static class CloudBaseFieldProjector
{
    private static readonly HashSet<string> HardExcluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "user_id", "owner", "created_by", "updated_by", "_openid"
    };

    internal static Dictionary<string, ContentField> Project(
        JsonElement row,
        string policyMode,
        IReadOnlyList<string>? allowedFields,
        string idField)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        var mode = (policyMode ?? "whitelist").Trim().ToLowerInvariant();
        HashSet<string>? allowed = mode == "all"
            ? allowedFields is null ? null : new HashSet<string>(allowedFields, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(allowedFields ?? [], StringComparer.OrdinalIgnoreCase);

        if (row.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        foreach (var property in row.EnumerateObject())
        {
            var name = property.Name;
            if (HardExcluded.Contains(name))
            {
                continue;
            }

            if (mode != "all" && !allowed!.Contains(name))
            {
                continue;
            }

            if (mode == "all" && name.Equals(idField, StringComparison.OrdinalIgnoreCase) &&
                (allowed is null || !allowed.Contains(name)))
            {
                continue;
            }

            if (TryConvert(property.Value, out var field))
            {
                fields[name] = field;
            }
        }

        return fields;
    }

    private static bool TryConvert(JsonElement value, out ContentField field)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                field = new ContentField("text", value.GetString());
                return true;
            case JsonValueKind.Number when value.TryGetDouble(out var number):
                field = new ContentField("number", number);
                return true;
            case JsonValueKind.True or JsonValueKind.False:
                field = new ContentField("check", value.GetBoolean());
                return true;
            case JsonValueKind.Array:
                var items = value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToArray();
                field = new ContentField("list", items);
                return items.Length > 0;
            default:
                field = new ContentField("text", null);
                return false;
        }
    }
}
