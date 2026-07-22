using System.Text.Json;

namespace Bukit.Cli.Commands;

internal static class AuditReportJsonReader
{
    internal static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    internal static JsonElement ReadRequiredObject(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path}.{property} must be an object.");
        }

        return value;
    }

    internal static JsonElement ReadRequiredArray(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path}.{property} must be an array.");
        }

        return value;
    }

    internal static string ReadRequiredString(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"{path}.{property} must be a non-empty string.");
        }

        return value.GetString()!;
    }

    internal static void ReadOptionalString(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{path}.{property} must be a string or null.");
        }
    }

    internal static int ReadRequiredInt(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException($"{path}.{property} must be an integer.");
        }

        return result;
    }

    internal static void ReadOptionalInt(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out _))
        {
            throw new InvalidDataException($"{path}.{property} must be an integer.");
        }
    }

    internal static int? TryReadOptionalInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    internal static bool ReadRequiredBool(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            throw new InvalidDataException($"{path}.{property} must be a boolean.");
        }

        return value.GetBoolean();
    }

    internal static void ReadOptionalBool(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new InvalidDataException($"{path}.{property} must be a boolean.");
        }
    }

    internal static void ReadOptionalStringArray(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path}.{property} must be an array or null.");
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"{path}.{property}[{index}] must be a string.");
            }

            index++;
        }
    }

    internal static void EnsureObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path} must be an object.");
        }
    }

    internal static void EnsureAllowedProperties(JsonElement element, string path, params string[] allowed)
    {
        var set = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!set.Contains(property.Name))
            {
                throw new InvalidDataException($"{path}.{property.Name} is not allowed by the SEO report schema.");
            }
        }
    }
}
