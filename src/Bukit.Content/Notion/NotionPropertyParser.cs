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

            if (TryParseNotionPropertyToField(prop.Value, out var field, out _))
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

            if (TryParseNotionPropertyToField(prop.Value, out var field, out _))
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

internal static bool TryParseNotionPropertyToField(JsonElement property, out ContentField field, out string notionType)
    {
        field = default!;
        notionType = string.Empty;
        if (property.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        if (!property.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var type = typeEl.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        notionType = type;
        switch (type)
        {
            case "title":
                {
                    var text = ExtractPlainTextArray(property, "title");
                    field = new ContentField("text", text);
                    return !string.IsNullOrWhiteSpace(text);
                }
            case "rich_text":
                {
                    var text = ExtractPlainTextArray(property, "rich_text");
                    field = new ContentField("text", text);
                    return !string.IsNullOrWhiteSpace(text);
                }
            case "url":
                {
                    if (property.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
                    {
                        var text = u.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "email":
                {
                    if (property.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String)
                    {
                        var text = e.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "phone_number":
                {
                    if (property.TryGetProperty("phone_number", out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        var text = p.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "number":
                {
                    if (property.TryGetProperty("number", out var n) && n.ValueKind is JsonValueKind.Number)
                    {
                        field = new ContentField("number", n.GetDouble());
                        return true;
                    }
                    return false;
                }
            case "checkbox":
                {
                    if (property.TryGetProperty("checkbox", out var b) && b.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        field = new ContentField("bool", b.GetBoolean());
                        return true;
                    }
                    return false;
                }
            case "date":
                {
                    if (property.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.Object &&
                        d.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String)
                    {
                        var text = start.GetString();
                        if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out var dto))
                        {
                            field = new ContentField("date", dto);
                            return true;
                        }
                    }
                    return false;
                }
            case "created_time":
                {
                    if (property.TryGetProperty("created_time", out var ct) && ct.ValueKind == JsonValueKind.String)
                    {
                        var text = ct.GetString() ?? string.Empty;
                        if (NotionContentProvider.TryParseDateTimeOffset(text, out var dto))
                        {
                            field = new ContentField("date", dto);
                            return true;
                        }
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "last_edited_time":
                {
                    if (property.TryGetProperty("last_edited_time", out var lt) && lt.ValueKind == JsonValueKind.String)
                    {
                        var text = lt.GetString() ?? string.Empty;
                        if (NotionContentProvider.TryParseDateTimeOffset(text, out var dto))
                        {
                            field = new ContentField("date", dto);
                            return true;
                        }
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "created_by":
                {
                    if (property.TryGetProperty("created_by", out var cb) && cb.ValueKind == JsonValueKind.Object)
                    {
                        var text = ExtractUserNameOrId(cb);
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "last_edited_by":
                {
                    if (property.TryGetProperty("last_edited_by", out var lb) && lb.ValueKind == JsonValueKind.Object)
                    {
                        var text = ExtractUserNameOrId(lb);
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "multi_select":
                {
                    if (property.TryGetProperty("multi_select", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        var list = arr.EnumerateArray()
                            .Select(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x!.Trim())
                            .ToList();

                        field = new ContentField("list", list);
                        return list.Count > 0;
                    }
                    return false;
                }
            case "select":
                {
                    if (property.TryGetProperty("select", out var sel) && sel.ValueKind == JsonValueKind.Object &&
                        sel.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    {
                        var text = n.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "status":
                {
                    if (property.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Object &&
                        status.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    {
                        var text = n.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "formula":
                {
                    if (property.TryGetProperty("formula", out var f) && f.ValueKind == JsonValueKind.Object)
                    {
                        return TryParseFormulaToField(f, out field);
                    }
                    return false;
                }
            case "people":
                {
                    if (property.TryGetProperty("people", out var people) && people.ValueKind == JsonValueKind.Array)
                    {
                        var list = people.EnumerateArray()
                            .Select(x => x.ValueKind == JsonValueKind.Object ? ExtractUserNameOrId(x) : string.Empty)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

                        field = new ContentField("list", list);
                        return list.Count > 0;
                    }
                    return false;
                }
            case "relation":
                {
                    if (property.TryGetProperty("relation", out var rel) && rel.ValueKind == JsonValueKind.Array)
                    {
                        var list = rel.EnumerateArray()
                            .Select(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x!.Trim())
                            .ToList();

                        field = new ContentField("list", list);
                        return list.Count > 0;
                    }
                    return false;
                }
            case "rollup":
                {
                    if (property.TryGetProperty("rollup", out var rollup) && rollup.ValueKind == JsonValueKind.Object)
                    {
                        return TryParseRollupToField(rollup, out field);
                    }
                    return false;
                }
            case "unique_id":
                {
                    if (property.TryGetProperty("unique_id", out var uid) && uid.ValueKind == JsonValueKind.Object)
                    {
                        var text = BuildUniqueIdString(uid);
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "verification":
                {
                    if (property.TryGetProperty("verification", out var ver) && ver.ValueKind == JsonValueKind.Object)
                    {
                        var state = ver.TryGetProperty("state", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                        var text = (state ?? string.Empty).Trim();
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "files":
                {
                    if (property.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
                    {
                        var urls = new List<string>();
                        foreach (var f in files.EnumerateArray())
                        {
                            if (f.ValueKind != JsonValueKind.Object)
                            {
                                continue;
                            }

                            if (!f.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String)
                            {
                                continue;
                            }

                            var ft = t.GetString();
                            string? fileUrl = null;
                            if (string.Equals(ft, "external", StringComparison.OrdinalIgnoreCase) &&
                                f.TryGetProperty("external", out var ex) && ex.ValueKind == JsonValueKind.Object &&
                                ex.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
                            {
                                fileUrl = url.GetString();
                            }
                            else if (string.Equals(ft, "file", StringComparison.OrdinalIgnoreCase) &&
                                     f.TryGetProperty("file", out var ff) && ff.ValueKind == JsonValueKind.Object &&
                                     ff.TryGetProperty("url", out var furl) && furl.ValueKind == JsonValueKind.String)
                            {
                                fileUrl = furl.GetString();
                            }

                            if (!string.IsNullOrWhiteSpace(fileUrl))
                            {
                                urls.Add(fileUrl);
                            }
                        }

                        if (urls.Count == 1)
                        {
                            field = new ContentField("file", urls[0]);
                            return true;
                        }

                        if (urls.Count > 1)
                        {
                            field = new ContentField("files", urls.AsReadOnly());
                            return true;
                        }
                    }
                    return false;
                }
            default:
                return false;
        }
    }

    internal static string ExtractPlainTextArray(JsonElement property, string key)
    {
        if (!property.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            if (item.TryGetProperty("plain_text", out var t) && t.ValueKind == JsonValueKind.String)
            {
                var s = t.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(s.Trim());
                }
            }
        }

        return sb.ToString();
    }

    internal static bool TryParseRollupToField(JsonElement rollup, out ContentField field)
    {
        field = default!;
        if (!rollup.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var type = typeEl.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        if (type == "number" && rollup.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number)
        {
            field = new ContentField("number", n.GetDouble());
            return true;
        }

        if (type == "date" && rollup.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.Object &&
            d.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String)
        {
            var text = start.GetString() ?? string.Empty;
            if (NotionContentProvider.TryParseDateTimeOffset(text, out var dto))
            {
                field = new ContentField("date", dto);
                return true;
            }
        }

        if (type == "array" && rollup.TryGetProperty("array", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            var list = new List<object>();
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                if (TryParseNotionPropertyToField(item, out var inner, out _) && inner.Value is not null)
                {
                    list.Add(inner.Value);
                }
            }

            field = new ContentField("list", list);
            return list.Count > 0;
        }

        return false;
    }

    internal static bool TryParseFormulaToField(JsonElement formula, out ContentField field)
    {
        field = default!;
        var type = NotionContentProvider.GetString(formula, "type");
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        switch (type)
        {
            case "string":
                {
                    var text = NotionContentProvider.GetString(formula, "string") ?? string.Empty;
                    field = new ContentField("text", text);
                    return !string.IsNullOrWhiteSpace(text);
                }
            case "number":
                {
                    if (formula.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number)
                    {
                        field = new ContentField("number", n.GetDouble());
                        return true;
                    }
                    return false;
                }
            case "boolean":
                {
                    if (formula.TryGetProperty("boolean", out var b) && b.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        field = new ContentField("bool", b.GetBoolean());
                        return true;
                    }
                    return false;
                }
            case "date":
                {
                    if (formula.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.Object &&
                        d.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String)
                    {
                        var text = start.GetString();
                        if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out var dto))
                        {
                            field = new ContentField("date", dto);
                            return true;
                        }
                    }
                    return false;
                }
        }

        return false;
    }

    internal static string BuildUniqueIdString(JsonElement uniqueId)
    {
        if (uniqueId.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var prefix = uniqueId.TryGetProperty("prefix", out var p) && p.ValueKind == JsonValueKind.String ? (p.GetString() ?? string.Empty).Trim() : string.Empty;
        var numberText = string.Empty;
        if (uniqueId.TryGetProperty("number", out var n))
        {
            if (n.ValueKind == JsonValueKind.Number && n.TryGetInt64(out var num))
            {
                numberText = num.ToString();
            }
            else if (n.ValueKind == JsonValueKind.String)
            {
                numberText = (n.GetString() ?? string.Empty).Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(numberText))
        {
            return prefix;
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return numberText;
        }

        return $"{prefix}-{numberText}";
    }

    internal static string ExtractUserNameOrId(JsonElement user)
    {
        if (user.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (user.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
        {
            var n = name.GetString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(n))
            {
                return n.Trim();
            }
        }

        if (user.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
        {
            var s = id.GetString() ?? string.Empty;
            return s.Trim();
        }

        return string.Empty;
    }

}
