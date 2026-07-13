using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using System.Globalization;
using System.Text;
using System.Text.Json;
namespace Bukit.Content.Notion;

public static class NotionPropertyParser
{
    public static IReadOnlyDictionary<string, ContentField> ExtractFields(JsonElement properties)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        foreach (var prop in properties.EnumerateObject())
        {
            var key = NormalizeFieldKey(prop.Name);
            if (string.IsNullOrWhiteSpace(key) || IsReservedNotionField(key))
            {
                continue;
            }

            if (NotionPropertyTypeParser.TryParseNotionPropertyToField(prop.Value, out var field, out _))
            {
                fields[key] = field;
            }
        }

        return fields;
    }

    public static IReadOnlyDictionary<string, ContentField> ExtractAllFields(JsonElement properties)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        foreach (var prop in properties.EnumerateObject())
        {
            var key = NormalizeFieldKey(prop.Name);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (NotionPropertyTypeParser.TryParseNotionPropertyToField(prop.Value, out var field, out _))
            {
                fields[key] = field;
            }
        }

        return fields;
    }

    internal static IReadOnlyDictionary<string, ContentField> ExtractFields(
        JsonElement properties,
        string policyMode,
        HashSet<string>? allowed,
        out IReadOnlyList<string> relationKeys)
    {
        var relations = new List<string>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (properties.ValueKind != JsonValueKind.Object)
        {
            relationKeys = relations;
            return fields;
        }

        foreach (var prop in properties.EnumerateObject())
        {
            var rawName = prop.Name;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                continue;
            }

            var key = NormalizeFieldKey(rawName);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (policyMode == "whitelist" && allowed is not null && !allowed.Contains(key))
            {
                continue;
            }

            if (NotionPropertyTypeParser.TryParseNotionPropertyToField(prop.Value, out var field, out var notionType))
            {
                if (IsReservedNotionField(key) && !string.Equals(notionType, "relation", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                fields[key] = field;
                if (string.Equals(notionType, "relation", StringComparison.OrdinalIgnoreCase))
                {
                    relations.Add(key);
                }
            }
        }

        relationKeys = relations;
        return fields;
    }

    internal static string? ExtractTitle(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fieldName = propertyMap?.Title ?? "Title";
        if (NotionContentProvider.TryGetPropertyIgnoreCase(properties, fieldName, out var titleProp))
        {
            var text = ExtractTitleProperty(titleProp);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        foreach (var prop in properties.EnumerateObject())
        {
            var v = prop.Value;
            if (NotionContentProvider.GetString(v, "type") == "title")
            {
                var text = ExtractTitleProperty(v);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    internal static string? ExtractTitleProperty(JsonElement prop)
    {
        if (prop.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!prop.TryGetProperty("title", out var titleArray) || titleArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var item in titleArray.EnumerateArray())
        {
            if (item.TryGetProperty("plain_text", out var plain) && plain.ValueKind == JsonValueKind.String)
            {
                sb.Append(plain.GetString());
            }
        }

        return sb.ToString().Trim();
    }

    internal static string? ExtractSlug(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fieldName = propertyMap?.Slug ?? "Slug";
        if (!NotionContentProvider.TryGetPropertyIgnoreCase(properties, fieldName, out var slugProp))
        {
            return null;
        }

        var type = NotionContentProvider.GetString(slugProp, "type");
        if (type == "rich_text" && slugProp.TryGetProperty("rich_text", out var rt))
        {
            var text = NotionContentProvider.ExtractPlainText(rt);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        if (type == "formula" && slugProp.TryGetProperty("formula", out var f) && f.ValueKind == JsonValueKind.Object)
        {
            var value = NotionContentProvider.GetString(f, "string");
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    internal static string? ExtractType(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fieldName = propertyMap?.Type ?? "Type";
        if (!NotionContentProvider.TryGetPropertyIgnoreCase(properties, fieldName, out var typeProp))
        {
            return null;
        }

        var t = NotionContentProvider.GetString(typeProp, "type");
        if (t == "select" && typeProp.TryGetProperty("select", out var sel) && sel.ValueKind == JsonValueKind.Object)
        {
            return NotionContentProvider.GetString(sel, "name");
        }

        if (t == "multi_select" && typeProp.TryGetProperty("multi_select", out var ms) && ms.ValueKind == JsonValueKind.Array)
        {
            var first = ms.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object)
            {
                return NotionContentProvider.GetString(first, "name");
            }
        }

        return null;
    }

    internal static string? ExtractCollection(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fieldName = propertyMap?.Collection ?? "Collection";
        if (!NotionContentProvider.TryGetPropertyIgnoreCase(properties, fieldName, out var collectionProp))
        {
            return null;
        }

        var notionType = NotionContentProvider.GetString(collectionProp, "type");
        if (notionType is not ("rich_text" or "select" or "status"))
        {
            throw new ContentException(
                $"Notion Collection property '{fieldName}' must contain a single scalar value of type rich_text, select, or status; " +
                $"property type '{notionType ?? "<missing>"}' is not supported.");
        }

        if (!NotionPropertyTypeParser.TryParseNotionPropertyToField(collectionProp, out var field, out _) ||
            field.Value is not string text)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    internal static DateTimeOffset? ExtractPublishAt(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var primaryField = propertyMap?.PublishAt ?? "PublishAt";
        if (NotionContentProvider.TryGetPropertyIgnoreCase(properties, primaryField, out var dateProp))
        {
            var value = ReadDateProperty(dateProp);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    internal static DateTimeOffset? ReadDateProperty(JsonElement prop)
    {
        var type = NotionContentProvider.GetString(prop, "type");
        if (type != "date")
        {
            return null;
        }

        if (!prop.TryGetProperty("date", out var dateObj) || dateObj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var start = NotionContentProvider.GetString(dateObj, "start");
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                start,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dto))
        {
            return dto;
        }

        return null;
    }

    internal static bool IsReservedNotionField(string normalizedKey)
    {
        return normalizedKey is "published" or "title" or "slug" or "type" or "publishat" or "publish_at";
    }

    internal static string NormalizeFieldKey(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(trimmed.Length);
        var underscore = false;

        foreach (var ch in trimmed)
        {
            var lower = char.ToLowerInvariant(ch);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(lower);
                underscore = false;
                continue;
            }

            if (!underscore)
            {
                sb.Append('_');
                underscore = true;
            }
        }

        return sb.ToString().Trim('_');
    }

    internal static string? GetRichTextPlain(JsonElement property)
    {
        if (property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = NotionContentProvider.GetString(property, "type");
        if (type != "rich_text" || !property.TryGetProperty("rich_text", out var rt))
        {
            return null;
        }

        var text = NotionContentProvider.ExtractPlainText(rt);
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    internal static void ProjectSeoFields(
        Dictionary<string, object> projectedValues,
        JsonElement properties,
        NotionPropertyMapConfig? propertyMap)
    {
        if (propertyMap is null) return;

        if (!string.IsNullOrWhiteSpace(propertyMap.SeoTitle) &&
            NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyMap.SeoTitle, out var seoTitleProp))
        {
            var value = GetRichTextPlain(seoTitleProp);
            if (!string.IsNullOrWhiteSpace(value))
                projectedValues["seo_title"] = value;
        }

        if (!string.IsNullOrWhiteSpace(propertyMap.SeoDescription) &&
            NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyMap.SeoDescription, out var seoDescProp))
        {
            var value = GetRichTextPlain(seoDescProp);
            if (!string.IsNullOrWhiteSpace(value))
                projectedValues["seo_desc"] = value;
        }

        if (!string.IsNullOrWhiteSpace(propertyMap.SeoImage) &&
            NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyMap.SeoImage, out var seoImageProp))
        {
            var value = GetRichTextPlain(seoImageProp);
            if (!string.IsNullOrWhiteSpace(value))
                projectedValues["seo_image"] = value;
        }

        if (!string.IsNullOrWhiteSpace(propertyMap.Canonical) &&
            NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyMap.Canonical, out var canonicalProp))
        {
            var value = GetRichTextPlain(canonicalProp);
            if (!string.IsNullOrWhiteSpace(value))
                projectedValues["canonical"] = value;
        }
    }

    internal static void ProjectCanonicalFields(
        Dictionary<string, object> projectedValues,
        JsonElement properties,
        NotionPropertyMapConfig? propertyMap,
        string pageId)
    {
        if (propertyMap is null)
        {
            return;
        }

        ProjectMappedValue(projectedValues, properties, propertyMap.OriginalUrl, "original_url", pageId, ["url"]);
        ProjectMappedValue(projectedValues, properties, propertyMap.References, "references", pageId, ["multi_select", "rich_text"], wrapTextInList: true);
        ProjectMappedValue(projectedValues, properties, propertyMap.Cover, "cover", pageId, ["rich_text", "url", "files"], firstListItem: true);
        ProjectMappedValue(projectedValues, properties, propertyMap.CoverAlt, "cover_alt", pageId, ["rich_text"]);
        ProjectMappedValue(projectedValues, properties, propertyMap.CoverCaption, "cover_caption", pageId, ["rich_text"]);

        if (!string.IsNullOrWhiteSpace(propertyMap.EntitiesJson) &&
            NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyMap.EntitiesJson, out var entitiesProperty))
        {
            ValidateMappedPropertyType(entitiesProperty, pageId, propertyMap.EntitiesJson, ["rich_text"]);
            var json = GetRichTextPlain(entitiesProperty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                projectedValues["entities"] = ParseEntitiesJson(json, pageId, propertyMap.EntitiesJson);
            }
        }
    }

    private static void ProjectMappedValue(
        Dictionary<string, object> projectedValues,
        JsonElement properties,
        string? propertyName,
        string canonicalKey,
        string pageId,
        IReadOnlyList<string> allowedTypes,
        bool firstListItem = false,
        bool wrapTextInList = false)
    {
        if (string.IsNullOrWhiteSpace(propertyName) ||
            !NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyName, out var property))
        {
            return;
        }

        ValidateMappedPropertyType(property, pageId, propertyName, allowedTypes);
        if (!NotionPropertyTypeParser.TryParseNotionPropertyToField(property, out var field, out _))
        {
            return;
        }

        var value = field.Value;
        if (firstListItem && value is IEnumerable<string> values)
        {
            value = values.FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item));
        }
        else if (wrapTextInList && value is string listItem && !string.IsNullOrWhiteSpace(listItem))
        {
            value = new[] { listItem.Trim() };
        }

        if (value is string text && string.IsNullOrWhiteSpace(text) || value is null)
        {
            return;
        }

        projectedValues[canonicalKey] = value;
    }

    private static void ValidateMappedPropertyType(
        JsonElement property,
        string pageId,
        string propertyName,
        IReadOnlyList<string> allowedTypes)
    {
        var actualType = NotionContentProvider.GetString(property, "type") ?? "<missing>";
        if (allowedTypes.Contains(actualType, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw new ContentException(
            $"Notion page '{pageId}' property '{propertyName}' has type '{actualType}'; " +
            $"allowed types: {string.Join(", ", allowedTypes)}.");
    }

    private static List<Dictionary<string, object?>> ParseEntitiesJson(
        string json,
        string pageId,
        string propertyName)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw EntitiesJsonError(pageId, propertyName, "must contain valid JSON", ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw EntitiesJsonError(pageId, propertyName, "must be a JSON array");
            }

            var entities = new List<Dictionary<string, object?>>();
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw EntitiesJsonError(pageId, propertyName, $"item {index} must be an object");
                }

                var type = ReadRequiredEntityString(element, "type", index, pageId, propertyName);
                var name = ReadRequiredEntityString(element, "name", index, pageId, propertyName);
                var description = ReadRequiredEntityString(element, "description", index, pageId, propertyName);
                entities.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = type,
                    ["name"] = name,
                    ["description"] = description
                });
                index++;
            }

            return entities;
        }
    }

    private static string ReadRequiredEntityString(
        JsonElement entity,
        string fieldName,
        int index,
        string pageId,
        string propertyName)
    {
        if (!entity.TryGetProperty(fieldName, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw EntitiesJsonError(pageId, propertyName, $"item {index} field '{fieldName}' must be a non-empty string");
        }

        return value.GetString()!.Trim();
    }

    private static ContentException EntitiesJsonError(
        string pageId,
        string propertyName,
        string detail,
        Exception? innerException = null)
    {
        var message = $"Notion page '{pageId}' property '{propertyName}' {detail}.";
        return innerException is null ? new ContentException(message) : new ContentException(message, innerException);
    }
}
