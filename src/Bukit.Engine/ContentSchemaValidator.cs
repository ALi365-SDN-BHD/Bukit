using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

public static class ContentSchemaValidator
{
    public static List<SchemaValidationError> Validate(
        IReadOnlyDictionary<string, object> meta,
        IReadOnlyList<SchemaFieldDefinition>? schema,
        string sourcePath)
    {
        var errors = new List<SchemaValidationError>();

        if (schema is null || schema.Count == 0)
        {
            return errors;
        }

        foreach (var field in schema)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                continue;
            }

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
            }
        }

        return errors;
    }

    private static bool ValidateType(string expectedType, object value)
    {
        return expectedType switch
        {
            "string" => value is string,
            "number" or "int" => value is int or long or double or float,
            "bool" or "boolean" => value is bool,
            "date" or "datetime" => value is DateTime or DateTimeOffset,
            "list" or "array" or "string[]" => value is IEnumerable<object> or System.Collections.IList,
            _ => true
        };
    }

    public sealed record SchemaValidationError(
        string Field,
        string Code,
        string Message,
        string? SourcePath);
}
