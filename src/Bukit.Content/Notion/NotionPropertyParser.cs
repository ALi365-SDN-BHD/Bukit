using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
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

            if (IsReservedNotionField(key))
            {
                continue;
            }

            if (policyMode == "whitelist" && allowed is not null && !allowed.Contains(key))
            {
                continue;
            }

            if (NotionPropertyTypeParser.TryParseNotionPropertyToField(prop.Value, out var field, out var notionType))
            {
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

        if (NotionContentProvider.TryGetPropertyIgnoreCase(properties, "Date", out var dateProp2))
        {
            var value = ReadDateProperty(dateProp2);
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

        if (DateTimeOffset.TryParse(start, out var dto))
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

    internal static void ExtractSeoFields(
        Dictionary<string, ContentField> fields,
        JsonElement properties,
        NotionPropertyMapConfig? propertyMap)
    {
        if (propertyMap is null) return;

        if (!string.IsNullOrWhiteSpace(propertyMap.SeoTitle) &&
            NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyMap.SeoTitle, out var seoTitleProp))
        {
            var value = GetRichTextPlain(seoTitleProp);
            if (!string.IsNullOrWhiteSpace(value))
                fields["seo_title"] = new ContentField("text", value);
        }

        if (!string.IsNullOrWhiteSpace(propertyMap.SeoDescription) &&
            NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyMap.SeoDescription, out var seoDescProp))
        {
            var value = GetRichTextPlain(seoDescProp);
            if (!string.IsNullOrWhiteSpace(value))
                fields["seo_desc"] = new ContentField("text", value);
        }

        if (!string.IsNullOrWhiteSpace(propertyMap.SeoImage) &&
            NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyMap.SeoImage, out var seoImageProp))
        {
            var value = GetRichTextPlain(seoImageProp);
            if (!string.IsNullOrWhiteSpace(value))
                fields["seo_image"] = new ContentField("text", value);
        }

        if (!string.IsNullOrWhiteSpace(propertyMap.Canonical) &&
            NotionContentProvider.TryGetPropertyIgnoreCase(properties, propertyMap.Canonical, out var canonicalProp))
        {
            var value = GetRichTextPlain(canonicalProp);
            if (!string.IsNullOrWhiteSpace(value))
                fields["canonical"] = new ContentField("text", value);
        }
    }
}
