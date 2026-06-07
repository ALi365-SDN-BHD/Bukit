using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static class DataModuleBuilder
{
    internal static IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? BuildModules(IReadOnlyList<ContentDocument> dataDocuments, string language, IContentBodyStore bodyStore)
    {
        if (dataDocuments.Count == 0)
        {
            return null;
        }

        var map = new Dictionary<string, List<ModuleInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in dataDocuments)
        {
            var enabled = ContentFieldReader.GetBool(document.CustomFields, "enabled");
            if (enabled is false)
            {
                continue;
            }

            var type = ContentFieldReader.GetContentType(document).Trim();
            if (string.IsNullOrWhiteSpace(type))
            {
                type = ContentFieldReader.GetText(document.CustomFields, "sourceKey") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                type = "module";
            }

            if (!map.TryGetValue(type, out var list))
            {
                list = new List<ModuleInfo>();
                map[type] = list;
            }

            list.Add(new ModuleInfo
            {
                Id = document.Id,
                Title = document.Title,
                Slug = document.Slug,
#pragma warning disable CS0618
                Content = ContentBodyResolver.GetHtml(document, bodyStore),
#pragma warning restore CS0618
                Fields = document.CustomFields
            });
        }

        var result = new Dictionary<string, IReadOnlyList<ModuleInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map)
        {
            var ordered = kv.Value
                .OrderBy(x => ContentFieldReader.GetNumber(x.Fields, "order") ?? 0d)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result[kv.Key] = ordered;
        }

        return result;
    }

    internal static IReadOnlyDictionary<string, object>? BuildDataBySource(IReadOnlyList<ContentDocument> dataDocuments, IContentBodyStore bodyStore)
    {
        if (dataDocuments.Count == 0)
        {
            return null;
        }

        var map = new Dictionary<string, List<ModuleInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in dataDocuments)
        {
            var sourceKey = ContentFieldReader.GetText(document.CustomFields, "sourceKey") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                continue;
            }

            var enabled = ContentFieldReader.GetBool(document.CustomFields, "enabled");
            if (enabled is false)
            {
                continue;
            }

            if (!map.TryGetValue(sourceKey, out var list))
            {
                list = new List<ModuleInfo>();
                map[sourceKey] = list;
            }

            list.Add(new ModuleInfo
            {
                Id = document.Id,
                Title = document.Title,
                Slug = document.Slug,
#pragma warning disable CS0618
                Content = ContentBodyResolver.GetHtml(document, bodyStore),
#pragma warning restore CS0618
                Fields = document.CustomFields
            });
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map)
        {
            result[kv.Key] = kv.Value
                .OrderBy(x => ContentFieldReader.GetNumber(x.Fields, "order") ?? 0d)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return result.Count == 0 ? null : result;
    }

    private static string? TryGetTextField(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null || !fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        var value = field.Value.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool? TryGetBoolField(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        var value = TryGetTextField(fields, key);
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    private static double? TryGetNumberField(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        var value = TryGetTextField(fields, key);
        return double.TryParse(value, out var parsed) ? parsed : null;
    }
}
