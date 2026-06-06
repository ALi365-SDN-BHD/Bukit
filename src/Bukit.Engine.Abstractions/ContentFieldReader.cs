namespace Bukit.Engine.Abstractions.Content;

public static class ContentFieldReader
{
    public static string? GetText(ContentItem item, string key)
        => GetText(item.Fields, key);

    public static string GetContentType(ContentItem item, string defaultType = "")
        => GetText(item.Fields, "type") ?? defaultType;

    public static string GetCollection(ContentItem item, string defaultCollection = "")
        => GetText(item.Fields, "collection") ?? defaultCollection;

    public static string? GetEffectiveCollection(ContentItem item, string? defaultCollection = null)
    {
        var collection = GetCollection(item);
        if (!string.IsNullOrWhiteSpace(collection))
        {
            return collection;
        }

        var type = GetContentType(item);
        if (!string.IsNullOrWhiteSpace(type))
        {
            return type;
        }

        return defaultCollection;
    }

    public static IReadOnlyList<string> GetTextValues(ContentItem item, string key)
        => GetTextList(item.Fields, key) ?? Array.Empty<string>();

    public static string? GetSummary(ContentItem item)
        => GetText(item.Fields, "summary")
           ?? GetText(item.Fields, "description")
           ?? GetText(item.Fields, "excerpt");

    public static bool IsDataItem(ContentItem item)
        => string.Equals(GetText(item.Fields, "sourceMode"), "data", StringComparison.OrdinalIgnoreCase);

    public static bool TryGetI18nKey(IReadOnlyDictionary<string, ContentField>? fields, out string key)
    {
        key = string.Empty;
        var text = GetText(fields, "i18nKey") ?? GetText(fields, "i18n_key");
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        key = text;
        return true;
    }

    public static string? GetText(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (!TryGetField(fields, key, out var field) || field.Value is null)
        {
            return null;
        }

        var text = field.Value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static bool? GetBool(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (!TryGetField(fields, key, out var field))
        {
            return null;
        }

        return field.Value switch
        {
            null => null,
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            string s when string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase) => true,
            string s when string.Equals(s, "1", StringComparison.OrdinalIgnoreCase) => true,
            int i => i != 0,
            long l => l != 0,
            double d => Math.Abs(d) > double.Epsilon,
            decimal m => m != 0,
            _ => null
        };
    }

    public static double? GetNumber(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (!TryGetField(fields, key, out var field))
        {
            return null;
        }

        return field.Value switch
        {
            null => null,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            string s when double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }

    public static DateTimeOffset? GetDate(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (!TryGetField(fields, key, out var field) || field.Value is null)
        {
            return null;
        }

        return field.Value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            string s when DateTimeOffset.TryParse(s, out var dto) => dto,
            _ => null
        };
    }

    public static IReadOnlyList<string>? GetTextList(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (!TryGetField(fields, key, out var field))
        {
            return null;
        }

        return ToTextList(field.Value);
    }

    public static IReadOnlyList<string>? ToTextList(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                return parts.Length == 0 ? null : parts;
            case IEnumerable<string> strings:
            {
                var list = strings
                    .Select(x => x?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();
                return list.Length == 0 ? null : list;
            }
            case IEnumerable<object> objects:
            {
                var list = objects
                    .Select(x => x?.ToString()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();
                return list.Length == 0 ? null : list;
            }
            default:
                var scalar = value.ToString()?.Trim();
                return string.IsNullOrWhiteSpace(scalar) ? null : new[] { scalar };
        }
    }

    public static Dictionary<string, ContentField> ToFieldMap(
        IReadOnlyDictionary<string, object> values,
        string defaultType = "text")
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            fields[key] = new ContentField(InferFieldType(value, defaultType), value);
        }

        return fields;
    }

    public static Dictionary<string, ContentField> WithValues(
        IReadOnlyDictionary<string, ContentField>? fields,
        IReadOnlyDictionary<string, object> values,
        string defaultType = "text")
    {
        var merged = fields is null
            ? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in values)
        {
            merged[key] = new ContentField(InferFieldType(value, defaultType), value);
        }

        return merged;
    }

    public static bool TryGetField(
        IReadOnlyDictionary<string, ContentField>? fields,
        string key,
        out ContentField field)
    {
        field = default!;
        if (fields is null)
        {
            return false;
        }

        if (fields.TryGetValue(key, out field!))
        {
            return true;
        }

        var alternateKey = fields.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        return alternateKey is not null && fields.TryGetValue(alternateKey, out field!);
    }

    private static string InferFieldType(object? value, string defaultType)
        => value switch
        {
            null => defaultType,
            bool => "boolean",
            int or long or double or float or decimal => "number",
            DateTime or DateTimeOffset => "date",
            IEnumerable<string> => "multi_select",
            IEnumerable<object> objects => objects.All(IsScalarValue) ? "multi_select" : "list",
            _ => defaultType
        };

    private static bool IsScalarValue(object? value)
        => value is null or string or bool or int or long or double or float or decimal or DateTime or DateTimeOffset;
}
