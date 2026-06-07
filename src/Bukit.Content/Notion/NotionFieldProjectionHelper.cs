using Bukit.Engine.Abstractions.Content;
using System.Text.Json;

namespace Bukit.Content.Notion;

internal static class NotionFieldProjectionHelper
{
    internal static void ProjectTextField(IReadOnlyDictionary<string, ContentField> fields, Dictionary<string, object> projectedValues, string fieldKey, string targetKey)
    {
        if (!fields.TryGetValue(fieldKey, out var field))
        {
            return;
        }

        if (field.Value is null)
        {
            return;
        }

        var text = field.Value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        projectedValues[targetKey] = text.Trim();
    }

    internal static void ProjectTaxonomyField(IReadOnlyDictionary<string, ContentField> fields, Dictionary<string, object> projectedValues, string fieldKey)
    {
        if (!fields.TryGetValue(fieldKey, out var field) || field.Value is null)
        {
            return;
        }

        if (field.Value is string s)
        {
            var text = s.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                projectedValues[fieldKey] = text;
            }
            return;
        }

        if (field.Value is IEnumerable<string> stringSeq)
        {
            var list = stringSeq
                .Select(x => x?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<object>()
                .ToList();

            if (list.Count > 0)
            {
                projectedValues[fieldKey] = list;
            }
            return;
        }

        if (field.Value is IEnumerable<object> objSeq)
        {
            var list = objSeq
                .Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<object>()
                .ToList();

            if (list.Count > 0)
            {
                projectedValues[fieldKey] = list;
            }
        }
    }

    internal static string NormalizePolicyMode(string? mode)
    {
        var m = (mode ?? "whitelist").Trim().ToLowerInvariant();
        return m is "all" ? "all" : "whitelist";
    }

    internal static HashSet<string>? BuildAllowedSet(IReadOnlyList<string>? allowed)
    {
        if (allowed is null || allowed.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in allowed)
        {
            var k = NotionPropertyParser.NormalizeFieldKey(a);
            if (!string.IsNullOrWhiteSpace(k))
            {
                set.Add(k);
            }
        }

        return set;
    }

    internal static IReadOnlyDictionary<string, ContentField> InjectPageCoverAndIcon(
        IReadOnlyDictionary<string, ContentField> fields, JsonElement page)
    {
        var coverUrl = ExtractPageFileUrl(page, "cover");
        var iconUrl = ExtractPageIconUrl(page);

        if (string.IsNullOrWhiteSpace(coverUrl) && string.IsNullOrWhiteSpace(iconUrl))
        {
            return fields;
        }

        var mutable = new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(coverUrl) && !mutable.ContainsKey("cover"))
        {
            mutable["cover"] = new ContentField("file", coverUrl);
        }

        if (!string.IsNullOrWhiteSpace(iconUrl) && !mutable.ContainsKey("icon"))
        {
            mutable["icon"] = new ContentField("file", iconUrl);
        }

        return mutable;
    }

    internal static string? ExtractPageFileUrl(JsonElement page, string propertyName)
    {
        if (!page.TryGetProperty(propertyName, out var container) || container.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fileType = NotionContentProvider.GetString(container, "type");
        if (fileType == "external" &&
            container.TryGetProperty("external", out var ext) &&
            ext.ValueKind == JsonValueKind.Object)
        {
            return NotionContentProvider.GetString(ext, "url");
        }

        if (fileType == "file" &&
            container.TryGetProperty("file", out var file) &&
            file.ValueKind == JsonValueKind.Object)
        {
            return NotionContentProvider.GetString(file, "url");
        }

        return null;
    }

    internal static string? ExtractPageIconUrl(JsonElement page)
    {
        if (!page.TryGetProperty("icon", out var icon) || icon.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var iconType = NotionContentProvider.GetString(icon, "type");

        if (iconType == "external" &&
            icon.TryGetProperty("external", out var ext) &&
            ext.ValueKind == JsonValueKind.Object)
        {
            return NotionContentProvider.GetString(ext, "url");
        }

        if (iconType == "file" &&
            icon.TryGetProperty("file", out var file) &&
            file.ValueKind == JsonValueKind.Object)
        {
            return NotionContentProvider.GetString(file, "url");
        }

        return null;
    }
}
