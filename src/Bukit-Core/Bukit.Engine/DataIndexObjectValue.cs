using System.Collections;
using System.Globalization;
using System.Text.Json;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// Reads an <c>object</c> or <c>object[]</c> data index value into one canonical form, whichever
/// source it came from: objects are ordinal maps with sorted keys, lists are <see cref="List{T}"/>,
/// and every scalar is a string (or null). Markdown front matter hands over a parsed map or list;
/// Notion hands over JSON text, which is parsed with <see cref="JsonDocument"/> (no reflection, so
/// it is Native AOT safe).
/// </summary>
/// <remarks>
/// Scalars become strings because Markdown front matter already gives every scalar as a string, so
/// a Notion row and a Markdown row with the same value build the same model and the same hash. JSON
/// numbers keep their source text (no digit is rounded away), booleans become <c>true</c> or
/// <c>false</c>, and JSON null stays null.
/// </remarks>
internal static class DataIndexObjectValue
{
    internal const string ObjectType = "object";
    internal const string ObjectArrayType = "object[]";

    /// <summary>The deepest nesting allowed; every object and every list counts as one level.</summary>
    internal const int MaxDepth = 8;

    /// <param name="raw">The source value: null, JSON text, or a parsed map or list.</param>
    /// <param name="isArray">True for <c>object[]</c>, false for <c>object</c>.</param>
    /// <param name="context">Names the source, content id and <c>scope.key</c> in every error.</param>
    /// <returns>An empty or canonical map for <c>object</c>; an empty or canonical list of maps for <c>object[]</c>.</returns>
    internal static object Read(object? raw, bool isArray, string context)
    {
        var valueType = isArray ? ObjectArrayType : ObjectType;
        object? value;
        if (raw is null || raw is string text && string.IsNullOrWhiteSpace(text))
        {
            value = null;
        }
        else if (raw is string json)
        {
            value = Parse(json.Trim(), context, valueType);
        }
        else if (raw is IDictionary or (IEnumerable and not string))
        {
            value = FromValue(raw, depth: 1, path: string.Empty, context);
        }
        else
        {
            throw Invalid(context, valueType, string.Empty, isArray ? "is not a list of objects" : "is not an object");
        }

        if (!isArray)
        {
            return value switch
            {
                null => new SortedDictionary<string, object?>(StringComparer.Ordinal),
                SortedDictionary<string, object?> map => map,
                _ => throw Invalid(context, valueType, string.Empty, "is not an object")
            };
        }

        if (value is null)
        {
            return new List<object?>();
        }

        if (value is not List<object?> list)
        {
            throw Invalid(context, valueType, string.Empty, "is not a list of objects");
        }

        for (var index = 0; index < list.Count; index++)
        {
            if (list[index] is not SortedDictionary<string, object?>)
            {
                throw Invalid(context, valueType, $"[{index}]", "is not an object");
            }
        }

        return list;
    }

    /// <summary>True when a stored value counts as empty for <c>requiredKeys</c>.</summary>
    internal static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        ICollection collection => collection.Count == 0,
        _ => false
    };

    private static object? Parse(string json, string context, string valueType)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return FromJson(document.RootElement, depth: 1, path: string.Empty, context);
        }
        catch (JsonException ex)
        {
            throw new ContentException($"{context} has value type '{valueType}' but its value is not valid JSON: {ex.Message}", ex);
        }
    }

    private static object? FromJson(JsonElement element, int depth, string path, string context)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                CheckDepth(depth, path, context);
                var map = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = Child(path, property.Name);
                    if (!map.TryAdd(property.Name, FromJson(property.Value, depth + 1, childPath, context)))
                    {
                        throw Invalid(context, null, childPath, "is a duplicate key");
                    }
                }

                return map;
            case JsonValueKind.Array:
                CheckDepth(depth, path, context);
                var list = new List<object?>(element.GetArrayLength());
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(FromJson(item, depth + 1, $"{path}[{index++}]", context));
                }

                return list;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                // The source text, so no digit is rounded away; a number past double range is no number at all.
                var number = element.GetRawText();
                return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)
                    ? number
                    : throw Invalid(context, null, path, "is not a finite number");
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            default:
                return null;
        }
    }

    private static object? FromValue(object? value, int depth, string path, string context)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return text;
            case bool flag:
                return flag ? "true" : "false";
            case double real when !double.IsFinite(real):
            case float single when !float.IsFinite(single):
                throw Invalid(context, null, path, "is not a finite number");
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
            case DateTimeOffset date:
                return date.ToString("O", CultureInfo.InvariantCulture);
            case DateTime date:
                return date.ToString("O", CultureInfo.InvariantCulture);
            case IDictionary dictionary:
                CheckDepth(depth, path, context);
                var map = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not string key)
                    {
                        throw Invalid(context, null, path, "has an object key that is not a string");
                    }

                    map[key] = FromValue(entry.Value, depth + 1, Child(path, key), context);
                }

                return map;
            case IEnumerable sequence:
                CheckDepth(depth, path, context);
                var list = new List<object?>();
                var index = 0;
                foreach (var item in sequence)
                {
                    list.Add(FromValue(item, depth + 1, $"{path}[{index++}]", context));
                }

                return list;
            default:
                throw Invalid(context, null, path, $"has an unsupported value of type '{value.GetType().Name}'");
        }
    }

    private static void CheckDepth(int depth, string path, string context)
    {
        if (depth > MaxDepth)
        {
            throw Invalid(context, null, path, $"exceeds the maximum nesting depth of {MaxDepth}");
        }
    }

    private static string Child(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";

    private static ContentException Invalid(string context, string? valueType, string path, string problem)
    {
        var subject = valueType is null ? " value" : $" has value type '{valueType}' but its value";
        var at = path.Length == 0 ? string.Empty : $" at '{path}'";
        return new ContentException($"{context}{subject}{at} {problem}.");
    }
}
