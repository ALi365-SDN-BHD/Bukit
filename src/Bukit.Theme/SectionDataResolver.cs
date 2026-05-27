using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Theme;

public static class SectionDataResolver
{
    public static IReadOnlyList<(ContentItem Item, string? Url)> Resolve(
        PageSectionDefinition section,
        IReadOnlyList<(ContentItem Item, RouteInfo? Route)> allPages)
    {
        var items = new List<(ContentItem, string?)>();

        if (string.IsNullOrWhiteSpace(section.Source)) return items;

        var source = section.Source.Trim();
        var sourceSet = new HashSet<string>(source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);

        foreach (var (item, route) in allPages)
        {
            if (!MatchesSource(item, sourceSet)) continue;
            if (!MatchesFilters(item, section.Filter)) continue;

            items.Add((item, route?.Url));
        }

        if (!string.IsNullOrWhiteSpace(section.Sort))
        {
            items = ApplySort(items, section.Sort);
        }

        if (section.Limit is { } limit && limit > 0)
        {
            items = items.Take(limit).ToList();
        }

        return items;
    }

    private static bool MatchesSource(ContentItem item, HashSet<string> sourceSet)
    {
        if (sourceSet.Contains("*") || sourceSet.Contains("all")) return true;

        var itemType = GetMetaString(item.Meta, "type") ?? "page";
        var itemCollections = GetMetaStringList(item.Meta, "collections");

        foreach (var source in sourceSet)
        {
            if (string.IsNullOrEmpty(source)) continue;

            if (source.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            {
                var typeValue = source[5..];
                if (string.Equals(itemType, typeValue, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (source.StartsWith("collection:", StringComparison.OrdinalIgnoreCase))
            {
                var collValue = source[11..];
                if (itemCollections is not null &&
                    itemCollections.Contains(collValue, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                if (string.Equals(itemType, source, StringComparison.OrdinalIgnoreCase)) return true;
                if (itemCollections is not null &&
                    itemCollections.Contains(source, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? GetMetaString(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (meta.TryGetValue(key, out var v) && v is not null) return v.ToString();
        return null;
    }

    private static List<string>? GetMetaStringList(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var v) || v is null) return null;

        if (v is List<string> stringList) return stringList;
        if (v is List<object> objList)
        {
            return objList.Select(o => o.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        if (v is string s) return [s];

        return null;
    }

    private static bool MatchesFilters(ContentItem item, IReadOnlyDictionary<string, object?>? filters)
    {
        if (filters is null || filters.Count == 0) return true;

        foreach (var (key, value) in filters)
        {
            if (value is null) continue;

            var itemValue = GetFieldValue(item.Fields, key);
            if (itemValue is null) return false;

            if (value is bool boolVal)
            {
                if (itemValue is bool itemBool && itemBool != boolVal) return false;
                if (itemValue is string itemStr && !string.Equals(itemStr, boolVal.ToString(), StringComparison.OrdinalIgnoreCase)) return false;
            }
            else if (!string.Equals(itemValue?.ToString(), value.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static object? GetFieldValue(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null) return null;
        return fields.TryGetValue(key, out var field) ? field.Value : null;
    }

    private static List<(ContentItem, string?)> ApplySort(List<(ContentItem, string?)> items, string sort)
    {
        var parts = sort.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var desc = parts.Length > 1 && string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase);
        var field = parts[0];

        return field switch
        {
            "publishAt" or "publish_at" or "date" => desc
                ? [.. items.OrderByDescending(x => x.Item1.PublishAt)]
                : [.. items.OrderBy(x => x.Item1.PublishAt)],
            "title" => desc
                ? [.. items.OrderByDescending(x => x.Item1.Title, StringComparer.OrdinalIgnoreCase)]
                : [.. items.OrderBy(x => x.Item1.Title, StringComparer.OrdinalIgnoreCase)],
            _ => desc
                ? [.. items.OrderByDescending(x => x.Item1.PublishAt)]
                : [.. items.OrderBy(x => x.Item1.PublishAt)]
        };
    }
}
