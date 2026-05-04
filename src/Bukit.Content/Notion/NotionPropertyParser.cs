using System.Text.Json;
using Bukit.Content;

namespace Bukit.Content.Notion;

public static class NotionPropertyParser
{
    public static IReadOnlyDictionary<string, ContentField> ExtractFields(JsonElement properties)
    {
        return ExtractFields(properties, includeReservedFields: false);
    }

    public static IReadOnlyDictionary<string, ContentField> ExtractAllFields(JsonElement properties)
    {
        return ExtractFields(properties, includeReservedFields: true);
    }

    public static IReadOnlyDictionary<string, ContentField> ExtractFields(JsonElement properties, bool includeReservedFields)
    {
        var dict = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (var prop in properties.EnumerateObject())
        {
            var key = NormalizeFieldKey(prop.Name);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!includeReservedFields && IsReservedNotionField(key))
            {
                continue;
            }

            if (TryParseNotionPropertyToField(prop.Value, out var field, out _))
            {
                dict[key] = field;
            }
        }

        return dict;
    }

    public static string NormalizeFieldKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        var lastUnderscore = false;

        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9')
            {
                sb.Append(ch);
                lastUnderscore = false;
                continue;
            }

            if (!lastUnderscore && sb.Length > 0)
            {
                sb.Append('_');
                lastUnderscore = true;
            }
        }

        return sb.ToString().Trim('_');
    }

    public static bool TryParseNotionPropertyToField(JsonElement property, out ContentField field, out string notionType)
    {
        field = new ContentField("text", string.Empty);
        notionType = string.Empty;

        if (property.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!property.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        notionType = typeEl.GetString() ?? string.Empty;
        var type = notionType.Trim().ToLowerInvariant();

        switch (type)
        {
            case "title":
                return TryParseRichTextArray(property, "title", out field);
            case "rich_text":
                return TryParseRichTextArray(property, "rich_text", out field);
            case "url":
            case "email":
            case "phone_number":
            case "select":
            case "status":
            case "unique_id":
            case "verification":
                return TryParseTextLike(property, type, out field);
            case "number":
                return TryParseNumber(property, out field);
            case "checkbox":
                return TryParseCheckbox(property, out field);
            case "date":
            case "created_time":
            case "last_edited_time":
                return TryParseDate(property, type, out field);
            case "multi_select":
            case "people":
            case "relation":
                return TryParseList(property, type, out field);
            case "files":
                return TryParseFiles(property, out field);
            case "formula":
                return TryParseFormula(property, out field);
            case "rollup":
                return TryParseRollup(property, out field);
            default:
                return false;
        }
    }

    private static bool IsReservedNotionField(string key)
    {
        return key.Equals("title", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("slug", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("type", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("publishat", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("publish_date", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("language", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("categories", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("summary", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("route", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("url", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("outputpath", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("template", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseRichTextArray(JsonElement property, string key, out ContentField field)
    {
        field = new ContentField("text", string.Empty);
        if (!property.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var s = item.TryGetProperty("plain_text", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;
            if (string.IsNullOrWhiteSpace(s))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }
            sb.Append(s!.Trim());
        }

        var text = sb.ToString();
        field = new ContentField("text", text);
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool TryParseTextLike(JsonElement property, string type, out ContentField field)
    {
        field = new ContentField("text", string.Empty);

        if (!property.TryGetProperty(type, out var el))
        {
            return false;
        }

        if (type is "select" or "status")
        {
            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty("name", out var n) &&
                n.ValueKind == JsonValueKind.String)
            {
                var text = n.GetString() ?? string.Empty;
                field = new ContentField("text", text);
                return !string.IsNullOrWhiteSpace(text);
            }

            return false;
        }

        if (type == "unique_id")
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var prefix = el.TryGetProperty("prefix", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            var number = el.TryGetProperty("number", out var num) && num.ValueKind == JsonValueKind.Number ? num.GetInt64().ToString() : null;
            var text = string.IsNullOrWhiteSpace(prefix) ? number ?? string.Empty : $"{prefix}-{number}";
            field = new ContentField("text", text);
            return !string.IsNullOrWhiteSpace(text);
        }

        if (type == "verification")
        {
            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty("state", out var st) &&
                st.ValueKind == JsonValueKind.String)
            {
                var text = st.GetString() ?? string.Empty;
                field = new ContentField("text", text);
                return !string.IsNullOrWhiteSpace(text);
            }

            return false;
        }

        if (el.ValueKind == JsonValueKind.String)
        {
            var text = el.GetString() ?? string.Empty;
            field = new ContentField("text", text);
            return !string.IsNullOrWhiteSpace(text);
        }

        return false;
    }

    private static bool TryParseNumber(JsonElement property, out ContentField field)
    {
        field = new ContentField("number", 0);
        if (!property.TryGetProperty("number", out var n) || n.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (n.TryGetInt64(out var l))
        {
            field = new ContentField("number", l);
            return true;
        }

        var d = n.GetDouble();
        field = new ContentField("number", d);
        return true;
    }

    private static bool TryParseCheckbox(JsonElement property, out ContentField field)
    {
        field = new ContentField("bool", false);
        if (!property.TryGetProperty("checkbox", out var b) || (b.ValueKind != JsonValueKind.True && b.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        field = new ContentField("bool", b.ValueKind == JsonValueKind.True);
        return true;
    }

    private static bool TryParseDate(JsonElement property, string type, out ContentField field)
    {
        field = new ContentField("date", default(DateTimeOffset));

        if (!property.TryGetProperty(type, out var dateEl))
        {
            return false;
        }

        if (type is "created_time" or "last_edited_time")
        {
            if (dateEl.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(dateEl.GetString(), out var dto))
            {
                field = new ContentField("date", dto);
                return true;
            }

            return false;
        }

        if (dateEl.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var start = dateEl.TryGetProperty("start", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        if (string.IsNullOrWhiteSpace(start))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(start, out var parsed))
        {
            field = new ContentField("date", parsed);
            return true;
        }

        return false;
    }

    private static bool TryParseList(JsonElement property, string type, out ContentField field)
    {
        field = new ContentField("list", Array.Empty<string>());

        if (!property.TryGetProperty(type, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (type == "multi_select")
            {
                var name = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    list.Add(name!.Trim());
                }
                continue;
            }

            if (type == "relation")
            {
                var id = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    list.Add(id!.Trim());
                }
                continue;
            }

            if (type == "people")
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                name = string.IsNullOrWhiteSpace(name)
                    ? (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
                    : name;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    list.Add(name!.Trim());
                }
            }
        }

        field = new ContentField("list", list);
        return list.Count > 0;
    }

    private static bool TryParseFiles(JsonElement property, out ContentField field)
    {
        field = new ContentField("file", string.Empty);
        if (!property.TryGetProperty("files", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var type = item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            type = (type ?? string.Empty).Trim().ToLowerInvariant();

            if (type == "external" &&
                item.TryGetProperty("external", out var ex) && ex.ValueKind == JsonValueKind.Object &&
                ex.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
            {
                var u = url.GetString() ?? string.Empty;
                field = new ContentField("file", u);
                return !string.IsNullOrWhiteSpace(u);
            }

            if (type == "file" &&
                item.TryGetProperty("file", out var f) && f.ValueKind == JsonValueKind.Object &&
                f.TryGetProperty("url", out var fu) && fu.ValueKind == JsonValueKind.String)
            {
                var u = fu.GetString() ?? string.Empty;
                field = new ContentField("file", u);
                return !string.IsNullOrWhiteSpace(u);
            }
        }

        return false;
    }

    private static bool TryParseFormula(JsonElement property, out ContentField field)
    {
        field = new ContentField("text", string.Empty);
        if (!property.TryGetProperty("formula", out var f) || f.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!f.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var type = (t.GetString() ?? string.Empty).Trim().ToLowerInvariant();
        if (type == "string" && f.TryGetProperty("string", out var s) && s.ValueKind == JsonValueKind.String)
        {
            var text = s.GetString() ?? string.Empty;
            field = new ContentField("text", text);
            return !string.IsNullOrWhiteSpace(text);
        }

        if (type == "number" && f.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number)
        {
            field = new ContentField("number", n.GetDouble());
            return true;
        }

        if (type == "boolean" && f.TryGetProperty("boolean", out var b) && (b.ValueKind == JsonValueKind.True || b.ValueKind == JsonValueKind.False))
        {
            field = new ContentField("bool", b.ValueKind == JsonValueKind.True);
            return true;
        }

        if (type == "date" && f.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.Object &&
            d.TryGetProperty("start", out var st) && st.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(st.GetString(), out var dto))
        {
            field = new ContentField("date", dto);
            return true;
        }

        return false;
    }

    private static bool TryParseRollup(JsonElement property, out ContentField field)
    {
        field = new ContentField("text", string.Empty);
        if (!property.TryGetProperty("rollup", out var r) || r.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!r.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var type = (t.GetString() ?? string.Empty).Trim().ToLowerInvariant();
        if (type == "number" && r.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number)
        {
            field = new ContentField("number", n.GetDouble());
            return true;
        }

        if (type == "date" && r.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.Object &&
            d.TryGetProperty("start", out var st) && st.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(st.GetString(), out var dto))
        {
            field = new ContentField("date", dto);
            return true;
        }

        if (type == "array" && r.TryGetProperty("array", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            var list = arr.EnumerateArray().Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            field = new ContentField("list", list);
            return list.Count > 0;
        }

        return false;
    }
}
