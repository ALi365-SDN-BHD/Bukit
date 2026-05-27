using Bukit.Engine.Abstractions.Content;
using System.Text;
using System.Text.Json;
namespace Bukit.Content.Notion;

internal static class NotionPropertyTypeParser
{
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
