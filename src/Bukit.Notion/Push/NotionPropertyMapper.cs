using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Notion.Mapping;
using Bukit.Notion.Seed;

namespace Bukit.Notion.Push;

internal static class NotionPropertyMapper
{
    public static JsonObject BuildPropertiesJsonObject(NotionDatabaseMapEntry entry, NotionSeedRecord record)
    {
        var properties = new JsonObject();
        foreach (NotionPropertyMapping property in entry.Properties.Values)
        {
            if (string.IsNullOrWhiteSpace(property.Source)
                || string.IsNullOrWhiteSpace(property.Type)
                || !record.Fields.TryGetValue(property.Source, out JsonElement value)
                || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            JsonNode? notionValue = ToNotionPropertyValue(property.Type, value);
            if (notionValue is not null)
            {
                properties[property.Name] = notionValue;
            }
        }

        return properties;
    }

    public static bool Validate(
        NotionDatabaseMapEntry entry,
        NotionSeedRecord record,
        string recordPath,
        List<NotionPushDiagnostic> diagnostics)
    {
        bool valid = true;
        foreach (NotionPropertyMapping property in entry.Properties.Values)
        {
            if (string.IsNullOrWhiteSpace(property.Source) || string.IsNullOrWhiteSpace(property.Type))
            {
                continue;
            }

            if (!record.Fields.TryGetValue(property.Source, out JsonElement value)
                || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                diagnostics.Add(new NotionPushDiagnostic(
                    string.Equals(property.Type, "title", StringComparison.Ordinal)
                        ? "notion.recordMissingTitlePropertyValue"
                        : "notion.recordMissingMappedProperty",
                    NotionDiagnosticSeverity.Error,
                    $"Seed record does not contain a value for mapped property {property.Name} from source {property.Source}.",
                    recordPath));
                valid = false;
                continue;
            }

            if (ToNotionPropertyValue(property.Type, value) is null)
            {
                diagnostics.Add(new NotionPushDiagnostic(
                    string.Equals(property.Type, "title", StringComparison.Ordinal)
                        ? "notion.recordMissingTitlePropertyValue"
                        : "notion.recordInvalidMappedPropertyType",
                    NotionDiagnosticSeverity.Error,
                    $"Seed record value for mapped property {property.Name} is not valid for Notion type {property.Type}.",
                    recordPath));
                valid = false;
            }
        }

        return valid;
    }

    private static JsonNode? ToNotionPropertyValue(string type, JsonElement value)
        => type switch
        {
            "title" => CreateRichTextProperty("title", ElementToString(value)),
            "rich_text" => CreateRichTextProperty("rich_text", ElementToString(value)),
            "checkbox" => value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? new JsonObject { ["checkbox"] = value.GetBoolean() }
                : null,
            "number" => value.ValueKind == JsonValueKind.Number
                ? new JsonObject { ["number"] = JsonValue.Create(value.GetDecimal()) }
                : null,
            "select" => CreateNamedProperty("select", ElementToString(value)),
            "multi_select" => CreateMultiSelectProperty(value),
            "url" => CreateStringProperty("url", ElementToString(value)),
            "email" => CreateStringProperty("email", ElementToString(value)),
            "phone_number" => CreateStringProperty("phone_number", ElementToString(value)),
            "date" => CreateDateProperty(ElementToString(value)),
            _ => null
        };

    private static JsonObject? CreateRichTextProperty(string type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new JsonObject
        {
            [type] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = new JsonObject
                    {
                        ["content"] = value
                    }
                }
            }
        };
    }

    private static JsonObject? CreateNamedProperty(string type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new JsonObject
        {
            [type] = new JsonObject
            {
                ["name"] = value
            }
        };
    }

    private static JsonObject? CreateStringProperty(string type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new JsonObject { [type] = value };
    }

    private static JsonObject? CreateDateProperty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new JsonObject
        {
            ["date"] = new JsonObject
            {
                ["start"] = value
            }
        };
    }

    private static JsonObject? CreateMultiSelectProperty(JsonElement value)
    {
        var items = new JsonArray();
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                string? name = ElementToString(item);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    items.Add(new JsonObject { ["name"] = name });
                }
            }
        }
        else
        {
            string? name = ElementToString(value);
            if (!string.IsNullOrWhiteSpace(name))
            {
                items.Add(new JsonObject { ["name"] = name });
            }
        }

        return items.Count == 0 ? null : new JsonObject { ["multi_select"] = items };
    }

    private static string? ElementToString(JsonElement value)
        => value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
