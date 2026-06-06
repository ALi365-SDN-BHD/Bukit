using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using System.Globalization;

namespace Bukit.Engine;

public static class ContentSchemaValidator
{
    public static List<SchemaValidationError> ValidateFields(
        IReadOnlyDictionary<string, ContentField>? fields,
        IReadOnlyList<SchemaFieldDefinition>? schema,
        string sourcePath,
        string failMode = "warn")
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (fields is not null)
        {
            foreach (var (key, field) in fields)
            {
                if (field.Value is not null)
                {
                    values[key] = field.Value;
                }
            }
        }

        return Validate(values, schema, sourcePath, failMode);
    }

    public static List<SchemaValidationError> Validate(
        IReadOnlyDictionary<string, object> meta,
        IReadOnlyList<SchemaFieldDefinition>? schema,
        string sourcePath,
        string failMode = "warn")
    {
        var errors = new List<SchemaValidationError>();

        if (schema is null || schema.Count == 0)
        {
            return errors;
        }

        var schemaFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in schema)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                continue;
            }

            schemaFieldNames.Add(field.Name);

            var hasValue = meta.TryGetValue(field.Name, out var rawValue) && rawValue is not null;

            if (field.Required && !hasValue && field.Default is null)
            {
                errors.Add(new SchemaValidationError(
                    field.Name,
                    "required",
                    $"Field '{field.Name}' is required but missing.",
                    sourcePath));
                continue;
            }

            if (!hasValue)
            {
                continue;
            }

            var expectedType = (field.Type ?? "string").Trim().ToLowerInvariant();
            if (!ValidateType(expectedType, rawValue!))
            {
                var actualType = rawValue!.GetType().Name.ToLowerInvariant();
                errors.Add(new SchemaValidationError(
                    field.Name,
                    "type_mismatch",
                    $"Field '{field.Name}' expected type '{expectedType}' but got '{actualType}'.",
                    sourcePath));
                continue;
            }

            if (field.Enum is { Count: > 0 } allowed && !MatchesEnum(rawValue!, allowed))
            {
                errors.Add(new SchemaValidationError(
                    field.Name,
                    "enum_mismatch",
                    $"Field '{field.Name}' must be one of: {string.Join(", ", allowed)}.",
                    sourcePath));
            }

            if (!string.IsNullOrWhiteSpace(field.Format) && !ValidateFormat(field.Format, rawValue!))
            {
                errors.Add(new SchemaValidationError(
                    field.Name,
                    "format_mismatch",
                    $"Field '{field.Name}' must match format '{field.Format}'.",
                    sourcePath));
            }

            if ((field.Min is not null || field.Max is not null) && !ValidateRange(rawValue!, field.Min, field.Max))
            {
                errors.Add(new SchemaValidationError(
                    field.Name,
                    "range_mismatch",
                    $"Field '{field.Name}' must be within range {field.Min?.ToString(CultureInfo.InvariantCulture) ?? "-∞"}..{field.Max?.ToString(CultureInfo.InvariantCulture) ?? "∞"}.",
                    sourcePath));
            }
        }

        if (failMode != "off")
        {
            foreach (var key in meta.Keys)
            {
                if (!schemaFieldNames.Contains(key) && !IsKnownSystemField(key))
                {
                    errors.Add(new SchemaValidationError(
                        key,
                        "unknown_field",
                        $"Field '{key}' is not declared in the collection schema.",
                        sourcePath));
                }
            }
        }

        return errors;
    }

    private static bool IsKnownSystemField(string fieldName)
    {
        return s_knownSystemFields.Contains(fieldName);
    }

    private static readonly HashSet<string> s_knownSystemFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "collection", "type", "draft",
        "title", "slug", "id",
        "template", "layout",
        "source", "sourcePath", "path", "file",
        "weight", "order",
        "tags", "categories",
        "author",
        "created", "modified", "published", "updated",
        "seo_title", "seo_desc", "schema_type", "geo_schema_type",
        "description", "summary", "excerpt",
        "image", "icon",
        "seo_keywords", "ai_summary", "source_url", "original_url",
        "cover_prompt", "generation_notes", "language", "url",
        "outputPath", "i18nKey", "notionPageId", "bodyFingerprint",
        "route"
    };

    private static bool ValidateType(string expectedType, object value)
    {
        return expectedType switch
        {
            "string" or "text" => value is string,
            "number" or "int" => value is int or long or double or float,
            "bool" or "boolean" => value is bool,
            "date" or "datetime" => value is DateTime or DateTimeOffset,
            "list" or "array" or "string[]" => value is IEnumerable<object> or System.Collections.IList,
            _ => true
        };
    }

    private static ContentField ToContentField(string? type, object value)
    {
        var normalized = (type ?? "string").Trim().ToLowerInvariant();
        return normalized switch
        {
            "bool" or "boolean" => new ContentField("bool", value is bool b ? b : bool.TryParse(value.ToString(), out var parsed) && parsed),
            "number" or "int" => new ContentField("number", value),
            "date" or "datetime" => new ContentField("date", value),
            "list" or "array" or "string[]" => new ContentField("list", value),
            "string" => new ContentField("text", value.ToString() ?? string.Empty),
            _ => new ContentField("text", value.ToString() ?? string.Empty)
        };
    }

    private static bool MatchesEnum(object value, IReadOnlyList<string> allowed)
    {
        var text = value.ToString();
        return !string.IsNullOrWhiteSpace(text) &&
               allowed.Any(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateFormat(string format, object value)
    {
        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return format.Trim().ToLowerInvariant() switch
        {
            "url" or "uri" => Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                              (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
            "email" => text.Contains('@', StringComparison.Ordinal) &&
                       text.IndexOf('@') > 0 &&
                       text.IndexOf('@') < text.Length - 1,
            "date" or "datetime" => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
            "slug" => text.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '/'),
            _ => true
        };
    }

    private static bool ValidateRange(object value, double? min, double? max)
    {
        if (!TryConvertToDouble(value, out var number))
        {
            var textLength = value.ToString()?.Length;
            if (textLength is null)
            {
                return false;
            }

            number = textLength.Value;
        }

        return (min is null || number >= min.Value) &&
               (max is null || number <= max.Value);
    }

    private static bool TryConvertToDouble(object value, out double number)
    {
        return value switch
        {
            byte b => Set(b, out number),
            short s => Set(s, out number),
            int i => Set(i, out number),
            long l => Set(l, out number),
            float f => Set(f, out number),
            double d => Set(d, out number),
            decimal m => Set((double)m, out number),
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => Set(parsed, out number),
            _ => Set(0, out number, false)
        };
    }

    private static bool Set(double value, out double number, bool result = true)
    {
        number = value;
        return result;
    }

    public sealed record SchemaValidationError(
        string Field,
        string Code,
        string Message,
        string? SourcePath);

    public static string ResolveSchemaFailMode(CollectionConfig? collection, string globalSchemaFailMode)
    {
        if (!string.IsNullOrWhiteSpace(collection?.SchemaFailMode))
        {
            return collection!.SchemaFailMode!.Trim().ToLowerInvariant();
        }

        return (globalSchemaFailMode ?? "warn").Trim().ToLowerInvariant();
    }
}
