using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Engine.Tests.Infrastructure;

// shortcut: active local report vocabulary only; extend with failing cases when inventory introduces a keyword or external reference.
internal static class LocalJsonSchema
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "$schema", "$id", "$defs", "$ref", "title", "description", "$comment", "default", "examples",
        "type", "const", "enum", "properties", "required", "additionalProperties", "items", "minimum", "maximum",
        "minLength", "pattern", "format", "minItems", "maxItems", "uniqueItems", "oneOf"
    };

    public static void AssertValid(JsonElement value, JsonElement schema)
    {
        CheckVocabulary(schema, schema);
        Validate(value, schema, schema, "$", 0);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private static JsonElement Resolve(JsonElement root, string reference)
    {
        Require(reference.StartsWith("#/", StringComparison.Ordinal), $"Unsupported reference: {reference}");
        var target = root;
        foreach (var segment in reference[2..].Split('/'))
        {
            var key = Uri.UnescapeDataString(segment).Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            Require(target.ValueKind == JsonValueKind.Object && target.TryGetProperty(key, out _), $"Unresolved reference: {reference}");
            target = target.GetProperty(key);
        }
        Require(target.ValueKind is JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False, $"Reference is not a schema: {reference}");
        return target;
    }

    private static void CheckVocabulary(JsonElement schema, JsonElement root)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
        Require(schema.ValueKind == JsonValueKind.Object, "Schema must be an object or boolean.");
        foreach (var property in schema.EnumerateObject())
        {
            Require(Keywords.Contains(property.Name), $"Unsupported schema keyword: {property.Name}");
            switch (property.Name)
            {
                case "$ref":
                    Resolve(root, property.Value.GetString()!);
                    break;
                case "$defs":
                case "properties":
                    foreach (var child in property.Value.EnumerateObject()) CheckVocabulary(child.Value, root);
                    break;
                case "items":
                case "additionalProperties":
                    CheckVocabulary(property.Value, root);
                    break;
                case "oneOf":
                    foreach (var child in property.Value.EnumerateArray()) CheckVocabulary(child, root);
                    break;
                case "format":
                    Require(property.Value.GetString() is "date" or "date-time" or "uri", "Unsupported schema format.");
                    break;
                case "type":
                    foreach (var type in property.Value.ValueKind == JsonValueKind.Array ? property.Value.EnumerateArray().ToArray() : [property.Value])
                        Require(type.GetString() is "object" or "array" or "string" or "boolean" or "number" or "integer" or "null", "Unsupported schema type.");
                    break;
            }
        }
    }

    private static void Validate(JsonElement value, JsonElement schema, JsonElement root, string path, int depth)
    {
        Require(depth < 100, $"{path}: recursive schema limit exceeded");
        if (schema.ValueKind == JsonValueKind.True) return;
        Require(schema.ValueKind != JsonValueKind.False, $"{path}: false schema rejects {value.GetRawText()}");
        if (schema.TryGetProperty("$ref", out var reference))
            Validate(value, Resolve(root, reference.GetString()!), root, path, depth + 1);
        if (schema.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().ToArray() : [type];
            Require(types.Any(item => MatchesType(value, item.GetString()!)), $"{path}: wrong type for {value.GetRawText()}");
        }
        if (schema.TryGetProperty("const", out var constant)) Require(Equal(value, constant), $"{path}: const mismatch");
        if (schema.TryGetProperty("enum", out var values)) Require(values.EnumerateArray().Any(item => Equal(value, item)), $"{path}: enum mismatch");
        if (schema.TryGetProperty("oneOf", out var alternatives))
        {
            var matches = 0;
            foreach (var alternative in alternatives.EnumerateArray())
            {
                try { Validate(value, alternative, root, path, depth + 1); matches++; }
                catch (InvalidDataException) { }
            }
            Require(matches == 1, $"{path}: oneOf matched {matches} branches");
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (schema.TryGetProperty("minimum", out var minimum)) Require(Compare(value, minimum) >= 0, $"{path}: below minimum");
            if (schema.TryGetProperty("maximum", out var maximum)) Require(Compare(value, maximum) <= 0, $"{path}: above maximum");
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (schema.TryGetProperty("minLength", out var length)) Require(text.EnumerateRunes().Count() >= length.GetInt32(), $"{path}: too short");
            if (schema.TryGetProperty("pattern", out var pattern)) Require(Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant), $"{path}: pattern mismatch");
            if (schema.TryGetProperty("format", out var format))
            {
                var valid = format.GetString() switch
                {
                    "date" => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                    "date-time" => Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}[Tt]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:[Zz]|[+-]\d{2}:\d{2})$") &&
                        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                    "uri" => !text.Any(char.IsWhiteSpace) && Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.IsWellFormedOriginalString(),
                    _ => false
                };
                Require(valid, $"{path}: invalid {format.GetString()}");
            }
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            var array = value.EnumerateArray().ToArray();
            if (schema.TryGetProperty("minItems", out var min)) Require(array.Length >= min.GetInt32(), $"{path}: too few items");
            if (schema.TryGetProperty("maxItems", out var max)) Require(array.Length <= max.GetInt32(), $"{path}: too many items");
            if (schema.TryGetProperty("uniqueItems", out var unique) && unique.GetBoolean())
                for (var i = 0; i < array.Length; i++)
                    Require(!array.Take(i).Any(item => Equal(array[i], item)), $"{path}: duplicate item");
            if (schema.TryGetProperty("items", out var items))
                for (var i = 0; i < array.Length; i++) Validate(array[i], items, root, $"{path}[{i}]", depth + 1);
        }
        if (value.ValueKind != JsonValueKind.Object) return;
        if (schema.TryGetProperty("required", out var required))
            foreach (var name in required.EnumerateArray()) Require(value.TryGetProperty(name.GetString()!, out _), $"{path}: missing {name.GetString()}");
        var properties = schema.TryGetProperty("properties", out var declared) ? declared : default;
        foreach (var property in value.EnumerateObject())
        {
            if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var child))
                Validate(property.Value, child, root, path + "." + property.Name, depth + 1);
            else if (schema.TryGetProperty("additionalProperties", out var additional))
                Validate(property.Value, additional, root, path + "." + property.Name, depth + 1);
        }
    }

    private static bool MatchesType(JsonElement value, string type) => type switch
    {
        "null" => value.ValueKind == JsonValueKind.Null,
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number && Number(value).Scale >= 0,
        _ => false
    };

    private static int Compare(JsonElement left, JsonElement right)
    {
        var a = Number(left);
        var b = Number(right);
        if (a.Sign != b.Sign) return a.Sign.CompareTo(b.Sign);
        if (a.Sign == 0) return 0;
        var magnitude = (a.Scale + a.Digits.Length).CompareTo(b.Scale + b.Digits.Length);
        var width = Math.Max(a.Digits.Length, b.Digits.Length);
        return a.Sign * (magnitude != 0 ? magnitude : string.CompareOrdinal(a.Digits.PadRight(width, '0'), b.Digits.PadRight(width, '0')));
    }

    // Compare decimal digits and exponents exactly, without rounding or expanding enormous powers of ten.
    private static (int Sign, string Digits, BigInteger Scale) Number(JsonElement value)
    {
        var text = value.GetRawText();
        var sign = text[0] == '-' ? -1 : 1;
        if (sign < 0) text = text[1..];
        var parts = text.Split('e', 'E');
        var point = parts[0].IndexOf('.');
        var scale = (parts.Length == 1 ? BigInteger.Zero : BigInteger.Parse(parts[1], CultureInfo.InvariantCulture))
            - (point < 0 ? 0 : parts[0].Length - point - 1);
        var digits = parts[0].Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0) return (0, "0", BigInteger.Zero);
        var significant = digits.TrimEnd('0');
        return (sign, significant, scale + digits.Length - significant.Length);
    }

    private static bool Equal(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind) return false;
        return left.ValueKind switch
        {
            JsonValueKind.Object => left.EnumerateObject().Count() == right.EnumerateObject().Count() &&
                left.EnumerateObject().All(property => right.TryGetProperty(property.Name, out var other) && Equal(property.Value, other)),
            JsonValueKind.Array => left.GetArrayLength() == right.GetArrayLength() &&
                left.EnumerateArray().Zip(right.EnumerateArray()).All(pair => Equal(pair.First, pair.Second)),
            JsonValueKind.Number => Compare(left, right) == 0,
            JsonValueKind.String => left.GetString() == right.GetString(),
            _ => true
        };
    }
}
