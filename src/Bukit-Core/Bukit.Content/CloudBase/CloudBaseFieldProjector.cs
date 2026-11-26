using System.Runtime.InteropServices;
using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Content.CloudBase;

/// <summary>
/// Projects row columns into public content fields. Personal and operator columns are always dropped,
/// including in <c>mode: all</c>, because configuration must not be trusted to hide them (D7); the
/// same names are dropped from the keys of nested objects. An allowed column is kept whole, nested
/// content included: the whitelist names top-level columns only.
/// </summary>
/// <remarks>
/// A JSON object, or an array holding anything other than strings and nulls, becomes a structured
/// field in the canonical form of <see cref="CanonicalJsonValue"/> (sorted ordinal keys, JSON types
/// kept, at most <see cref="CanonicalJsonValue.MaxDepth"/> levels) with type <c>list</c>, the type
/// Markdown front matter gives nested values. An array of strings stays a list of non-blank strings.
/// </remarks>
internal static class CloudBaseFieldProjector
{
    /// <summary>The largest raw JSON value, in UTF-8 bytes, a structured field may hold.</summary>
    internal const int MaxStructuredFieldBytes = 256 * 1024;

    internal static HashSet<string>? BuildAllowedSet(string policyMode, IReadOnlyList<string>? allowedFields)
    {
        var mode = (policyMode ?? "whitelist").Trim().ToLowerInvariant();
        return mode == "all"
            ? allowedFields is null ? null : new HashSet<string>(allowedFields, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(allowedFields ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <param name="rejected">
    /// Receives each column dropped because its structured value is too deep, too large or invalid,
    /// with the reason; the reason names key paths only, never values.
    /// </param>
    internal static Dictionary<string, ContentField> ProjectWithSet(
        JsonElement row,
        string policyMode,
        IReadOnlySet<string>? allowed,
        string idField,
        ICollection<(string Field, string Problem)>? rejected = null)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        var mode = (policyMode ?? "whitelist").Trim().ToLowerInvariant();

        if (row.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        foreach (var property in row.EnumerateObject())
        {
            var name = property.Name;
            if (CloudBaseFieldRules.IsHardExcluded(name))
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

            try
            {
                if (TryConvert(name, property.Value, out var field))
                {
                    fields[name] = field;
                }
            }
            catch (ContentException ex)
            {
                rejected?.Add((name, ex.Message));
            }
        }

        return fields;
    }

    private static bool TryConvert(string name, JsonElement value, out ContentField field)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                field = new ContentField("text", value.GetString());
                return true;
            // Integers first: a bigint id above 2^53 must not be rounded through double.
            // Other numbers stay double, the type content schema validation accepts.
            case JsonValueKind.Number when value.TryGetInt64(out var integer):
                field = new ContentField("number", integer);
                return true;
            case JsonValueKind.Number when value.TryGetDouble(out var number):
                field = new ContentField("number", number);
                return true;
            case JsonValueKind.True or JsonValueKind.False:
                field = new ContentField("check", value.GetBoolean());
                return true;
            // Nulls are dropped like blank strings, so a list of text stays a list of text.
            case JsonValueKind.Array when IsTextList(value):
                var items = value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToArray();
                field = new ContentField("list", items);
                return items.Length > 0;
            // An empty array is a text list above; an object left empty (after dropping personal keys) is absent like it.
            case JsonValueKind.Array or JsonValueKind.Object:
                var structured = ToStructured(name, value);
                field = new ContentField("list", structured);
                return structured is not SortedDictionary<string, object?> { Count: 0 };
            default:
                field = new ContentField("text", null);
                return false;
        }
    }

    /// <summary>
    /// True for a value the projector rejects as a structured field (too large, too deep or invalid),
    /// judged on the raw value as online. The content snapshot leaves such a column out, so an offline
    /// build publishes what an online build does.
    /// </summary>
    internal static bool IsRejectedStructured(string name, JsonElement value)
    {
        if (value.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object) ||
            (value.ValueKind == JsonValueKind.Array && IsTextList(value)))
        {
            return false;
        }

        try
        {
            ToStructured(name, value);
            return false;
        }
        catch (ContentException)
        {
            return true;
        }
    }

    private static bool IsTextList(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            {
                return false;
            }
        }

        return true;
    }

    /// <exception cref="ContentException">The value is too large, too deep or otherwise invalid.</exception>
    private static object ToStructured(string name, JsonElement value)
    {
        var context = $"CloudBase field '{name}'";
        var bytes = JsonMarshal.GetRawUtf8Value(value).Length;
        if (bytes > MaxStructuredFieldBytes)
        {
            throw CanonicalJsonValue.Invalid(
                context,
                null,
                string.Empty,
                $"is {bytes} bytes, more than the maximum of {MaxStructuredFieldBytes}");
        }

        var structured = CanonicalJsonValue.FromJson(value, depth: 1, path: string.Empty, context)!;
        RemoveHardExcludedKeys(structured);
        return structured;
    }

    /// <summary>Drops personal and operator keys (D7) from every nested object.</summary>
    private static void RemoveHardExcludedKeys(object? value)
    {
        switch (value)
        {
            case SortedDictionary<string, object?> map:
                foreach (var key in map.Keys.Where(CloudBaseFieldRules.IsHardExcluded).ToArray())
                {
                    map.Remove(key);
                }

                foreach (var child in map.Values)
                {
                    RemoveHardExcludedKeys(child);
                }

                break;
            case List<object?> list:
                foreach (var item in list)
                {
                    RemoveHardExcludedKeys(item);
                }

                break;
        }
    }
}
