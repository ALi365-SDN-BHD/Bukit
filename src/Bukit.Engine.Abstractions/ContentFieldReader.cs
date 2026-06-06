namespace Bukit.Engine.Abstractions.Content;

public static class ContentFieldReader
{
    public static string? GetText(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (TryGetField(fields, key, out var field) && field.Value is not null)
        {
            var text = field.Value.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return null;
    }

    public static string GetContentType(IReadOnlyDictionary<string, ContentField>? fields, string defaultType = "")
        => GetText(fields, "type") ?? defaultType;

    public static string GetCollection(IReadOnlyDictionary<string, ContentField>? fields, string defaultCollection = "")
        => GetText(fields, "collection") ?? defaultCollection;

    public static string? GetSummary(IReadOnlyDictionary<string, ContentField>? fields)
        => GetText(fields, "summary")
           ?? GetText(fields, "description")
           ?? GetText(fields, "excerpt");

    public static IReadOnlyList<string> GetTextList(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null || string.IsNullOrWhiteSpace(key))
        {
            return Array.Empty<string>();
        }

        return TryGetField(fields, key, out var field) && TryConvertValues(field.Value, out var values)
            ? values
            : Array.Empty<string>();
    }

    private static bool TryGetField(
        IReadOnlyDictionary<string, ContentField> fields,
        string key,
        out ContentField field)
    {
        if (fields.TryGetValue(key, out field!))
        {
            return true;
        }

        var caseInsensitiveKey = fields.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        if (caseInsensitiveKey is not null && fields.TryGetValue(caseInsensitiveKey, out field!))
        {
            return true;
        }

        field = default!;
        return false;
    }

    private static bool TryConvertValues(object? value, out IReadOnlyList<string> values)
    {
        switch (value)
        {
            case null:
                values = Array.Empty<string>();
                return false;
            case string text:
                values = string.IsNullOrWhiteSpace(text)
                    ? Array.Empty<string>()
                    : [text.Trim()];
                return values.Count > 0;
            case IEnumerable<string> strings:
                values = strings
                    .Select(x => x?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();
                return values.Count > 0;
            case IEnumerable<object> objects:
                values = objects
                    .Select(x => x?.ToString()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();
                return values.Count > 0;
            default:
                var scalar = value.ToString()?.Trim();
                values = string.IsNullOrWhiteSpace(scalar)
                    ? Array.Empty<string>()
                    : [scalar];
                return values.Count > 0;
        }
    }
}
