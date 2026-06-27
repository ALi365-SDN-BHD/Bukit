using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Notion.Mapping;
using Bukit.Notion.Seed;

namespace Bukit.Notion.Push;

internal static class NotionUniqueValueResolver
{
    public static bool TryResolve(NotionDatabaseMapEntry entry, NotionSeedRecord record, out string? value)
    {
        value = null;
        string? uniqueSource = ResolveSource(entry);
        return !string.IsNullOrWhiteSpace(uniqueSource)
            && TryGetScalarValue(record, uniqueSource, out value);
    }

    public static string BuildQueryJson(NotionDatabaseMapEntry entry, string uniqueValue)
    {
        string propertyType = entry.UniqueField is not null
            && entry.Properties.TryGetValue(entry.UniqueField, out NotionPropertyMapping? property)
            && !string.IsNullOrWhiteSpace(property.Type)
                ? property.Type
                : "rich_text";

        var root = new JsonObject
        {
            ["filter"] = new JsonObject
            {
                ["property"] = entry.UniqueField,
                [propertyType] = CreateFilterValue(propertyType, uniqueValue)
            },
            ["page_size"] = 1
        };
        return root.ToJsonString();
    }

    private static JsonObject CreateFilterValue(string propertyType, string uniqueValue)
    {
        if (propertyType == "number")
        {
            var number = new JsonObject();
            number["equals"] = decimal.TryParse(
                uniqueValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal value)
                    ? value
                    : null;
            return number;
        }

        return propertyType switch
        {
            "checkbox" => new JsonObject { ["equals"] = bool.TryParse(uniqueValue, out bool value) && value },
            "multi_select" => new JsonObject { ["contains"] = uniqueValue },
            _ => new JsonObject { ["equals"] = uniqueValue }
        };
    }

    private static string? ResolveSource(NotionDatabaseMapEntry entry)
    {
        if (entry.UniqueField is not null
            && entry.Properties.TryGetValue(entry.UniqueField, out NotionPropertyMapping? property)
            && !string.IsNullOrWhiteSpace(property.Source))
        {
            return property.Source;
        }

        return entry.UniqueField is null ? null : ToSnakeCase(entry.UniqueField);
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var chars = new List<char>();
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsUpper(current))
            {
                if (index > 0)
                {
                    chars.Add('_');
                }

                chars.Add(char.ToLowerInvariant(current));
            }
            else
            {
                chars.Add(current);
            }
        }

        return new string(chars.ToArray());
    }

    private static bool TryGetScalarValue(NotionSeedRecord record, string key, out string? value)
    {
        value = null;
        if (!record.Fields.TryGetValue(key, out JsonElement element))
        {
            return false;
        }

        value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => NormalizeNumber(element),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? NormalizeNumber(JsonElement element)
        => decimal.TryParse(
            element.GetRawText(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out decimal value)
                ? value.ToString("G29", CultureInfo.InvariantCulture)
                : null;
}
