using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// Reads an <c>object</c> or <c>object[]</c> data index value into one canonical form, whichever
/// source it came from: objects are ordinal maps with sorted keys, lists are <see cref="List{T}"/>,
/// and scalars are strings, booleans, numbers (<see cref="long"/>, then <see cref="decimal"/>, then
/// <see cref="double"/>) or null. Markdown front matter hands over a parsed map or list; Notion hands
/// over JSON text, which is parsed with <see cref="JsonDocument"/> (no reflection, so it is Native
/// AOT safe).
/// </summary>
/// <remarks>
/// JSON says which scalars are numbers and booleans. Markdown front matter gives every scalar as a
/// string, so a Markdown string is read as a boolean when it is <c>true</c> or <c>false</c> (in the
/// YAML core-schema spellings) and as a number when it is in JSON number form; anything else, such
/// as <c>0123</c> or <c>+60</c>, stays text. The same value therefore builds the same model and hash
/// from either source, and a template's <c>if info.open</c> is false for <c>false</c>.
/// </remarks>
internal static partial class DataIndexObjectValue
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

        // A single mapping stands for a list of one, as for a mini-program object[] field;
        // the list it is wrapped in is one more level.
        if (value is SortedDictionary<string, object?> single)
        {
            if (Depth(single) + 1 > MaxDepth)
            {
                throw Invalid(context, valueType, string.Empty, $"exceeds the maximum nesting depth of {MaxDepth} as a list of one object");
            }

            return new List<object?> { single };
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

    /// <summary>
    /// True when a stored value counts as empty for <c>requiredKeys</c>: null, blank text, or a
    /// mapping or list that holds nothing but empty values (so <c>{title: null}</c> is empty too).
    /// </summary>
    internal static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        SortedDictionary<string, object?> map => map.Values.All(IsEmpty),
        List<object?> list => list.All(IsEmpty),
        _ => false
    };

    /// <summary>The nesting depth of a canonical value: every mapping and list is one level.</summary>
    private static int Depth(object? value) => value switch
    {
        SortedDictionary<string, object?> map => 1 + map.Values.Select(Depth).DefaultIfEmpty(0).Max(),
        List<object?> list => 1 + list.Select(Depth).DefaultIfEmpty(0).Max(),
        _ => 0
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
                    var childPath = Key(path, property.Name, context);
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
                return ParseNumber(element.GetRawText(), path, context);
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }

    /// <summary>
    /// A Markdown scalar, which front matter always gives as text: <c>true</c>/<c>false</c> in the
    /// YAML core-schema spellings become booleans and JSON-form numbers become numbers, so Markdown
    /// and Notion agree. Other text, including <c>0123</c>, <c>+60</c> and <c>.5</c>, stays text.
    /// </summary>
    private static object ResolveText(string text, string path, string context) => text switch
    {
        "true" or "True" or "TRUE" => true,
        "false" or "False" or "FALSE" => false,
        _ when JsonNumber().IsMatch(text) => ParseNumber(text, path, context),
        _ => text
    };

    /// <summary>
    /// A JSON-form number as <see cref="long"/> when it is a whole number in range, as
    /// <see cref="decimal"/> when it fits (keeping its digits, so <c>12.50</c> stays <c>12.50</c>),
    /// and otherwise as a finite <see cref="double"/>.
    /// </summary>
    private static object ParseNumber(string text, string path, string context)
    {
        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
        {
            return whole;
        }

        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact))
        {
            return exact;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && double.IsFinite(real)
            ? real
            : throw Invalid(context, null, path, "is not a finite number");
    }

    [GeneratedRegex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex JsonNumber();

    private static object? FromValue(object? value, int depth, string path, string context)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return ResolveText(text, path, context);
            case bool flag:
                return flag;
            case double real when !double.IsFinite(real):
            case float single when !float.IsFinite(single):
                throw Invalid(context, null, path, "is not a finite number");
            case sbyte or byte or short or ushort or int or uint or long:
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            case ulong large:
                return large <= long.MaxValue ? (long)large : (decimal)large;
            case decimal exact:
                return exact;
            case float or double:
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
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

                    map[key] = FromValue(entry.Value, depth + 1, Key(path, key, context), context);
                }

                return map;
            // A map that implements only the generic dictionary interfaces is still a map, not a list of pairs.
            case IEnumerable<KeyValuePair<string, object?>> pairs:
                CheckDepth(depth, path, context);
                var pairMap = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                foreach (var (key, item) in pairs)
                {
                    var childPath = Key(path, key, context);
                    if (!pairMap.TryAdd(key, FromValue(item, depth + 1, childPath, context)))
                    {
                        throw Invalid(context, null, childPath, "is a duplicate key");
                    }
                }

                return pairMap;
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

    /// <summary>
    /// The path of an object key. A blank key is rejected: templates cannot read it, so it would
    /// reach the mini-program output and the hash but never a page.
    /// </summary>
    private static string Key(string path, string key, string context)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw Invalid(context, null, path, "has a blank object key");
        }

        return path.Length == 0 ? key : $"{path}.{key}";
    }

    private static ContentException Invalid(string context, string? valueType, string path, string problem)
    {
        var subject = valueType is null ? " value" : $" has value type '{valueType}' but its value";
        var at = path.Length == 0 ? string.Empty : $" at '{path}'";
        return new ContentException($"{context}{subject}{at} {problem}.");
    }
}
