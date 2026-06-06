namespace Bukit.Engine.Abstractions.Content;

public static class ContentItemExtensions
{
    public static string? GetTextValue(this ContentItem item, string key)
    {
        if (item.Fields is not null)
        {
            if (item.Fields.TryGetValue(key, out var field) && field.Value is not null)
            {
                var text = field.Value.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            var caseInsensitiveKey = item.Fields.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (caseInsensitiveKey is not null &&
                item.Fields.TryGetValue(caseInsensitiveKey, out var alternateField) &&
                alternateField.Value is not null)
            {
                var alternateText = alternateField.Value.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(alternateText))
                {
                    return alternateText;
                }
            }
        }

        if (item.Meta.TryGetValue(key, out var metaValue) && metaValue is not null)
        {
            var text = metaValue.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    public static string GetContentType(this ContentItem item, string defaultType = "")
    {
        return item.GetTextValue("type") ?? defaultType;
    }

    public static string GetCollection(this ContentItem item, string defaultCollection = "")
    {
        return item.GetTextValue("collection") ?? defaultCollection;
    }

    public static IReadOnlyList<string> GetTextValues(this ContentItem item, string key)
    {
        if (item.Fields is not null)
        {
            if (TryGetFieldValues(item.Fields, key, out var fieldValues))
            {
                return fieldValues;
            }

            var caseInsensitiveKey = item.Fields.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (caseInsensitiveKey is not null &&
                TryGetFieldValues(item.Fields, caseInsensitiveKey, out var alternateFieldValues))
            {
                return alternateFieldValues;
            }
        }

        if (TryConvertValues(item.Meta, key, out var metaValues))
        {
            return metaValues;
        }

        return Array.Empty<string>();
    }

    public static string? GetSummary(this ContentItem item)
    {
        return item.GetTextValue("summary")
               ?? item.GetTextValue("description")
               ?? item.GetTextValue("excerpt");
    }

    private static bool TryGetFieldValues(
        IReadOnlyDictionary<string, ContentField> fields,
        string key,
        out IReadOnlyList<string> values)
    {
        if (fields.TryGetValue(key, out var field))
        {
            return TryConvertValues(field.Value, out values);
        }

        values = Array.Empty<string>();
        return false;
    }

    private static bool TryConvertValues(
        IReadOnlyDictionary<string, object> meta,
        string key,
        out IReadOnlyList<string> values)
    {
        if (meta.TryGetValue(key, out var metaValue))
        {
            return TryConvertValues(metaValue, out values);
        }

        values = Array.Empty<string>();
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
