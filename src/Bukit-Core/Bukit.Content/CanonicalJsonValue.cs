using System.Globalization;
using System.Text.Json;
using Bukit.Shared;

namespace Bukit.Content;

/// <summary>
/// The canonical form of a structured JSON value, shared by data index <c>object</c> values and
/// CloudBase structured fields: objects are ordinal maps with sorted keys, lists are
/// <see cref="List{T}"/>, and scalars are strings, booleans, numbers (<see cref="long"/>, then
/// <see cref="decimal"/>, then <see cref="double"/>) or null. Every object and every list counts as one
/// nesting level, up to <see cref="MaxDepth"/>. Parsing uses <see cref="JsonElement"/> only (no
/// reflection), so it is Native AOT safe.
/// </summary>
internal static class CanonicalJsonValue
{
    /// <summary>The deepest nesting allowed; every object and every list counts as one level.</summary>
    internal const int MaxDepth = 8;

    /// <param name="element">The JSON value.</param>
    /// <param name="depth">The level of <paramref name="element"/> when it is an object or list; 1 at the root.</param>
    /// <param name="path">The path of <paramref name="element"/>, empty at the root.</param>
    /// <param name="context">Names the value's source in every error.</param>
    internal static object? FromJson(JsonElement element, int depth, string path, string context)
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
    /// A JSON-form number as <see cref="long"/> when it is a whole number in range, as
    /// <see cref="decimal"/> when it fits (keeping its digits, so <c>12.50</c> stays <c>12.50</c>),
    /// and otherwise as a finite <see cref="double"/>.
    /// </summary>
    internal static object ParseNumber(string text, string path, string context)
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

    internal static void CheckDepth(int depth, string path, string context)
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
    internal static string Key(string path, string key, string context)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw Invalid(context, null, path, "has a blank object key");
        }

        return path.Length == 0 ? key : $"{path}.{key}";
    }

    internal static ContentException Invalid(string context, string? valueType, string path, string problem)
    {
        var subject = valueType is null ? " value" : $" has value type '{valueType}' but its value";
        var at = path.Length == 0 ? string.Empty : $" at '{path}'";
        return new ContentException($"{context}{subject}{at} {problem}.");
    }
}
