using System.Text.Json;
using Bukit.Shared;

namespace Bukit.Content.Notion;

internal sealed record NotionResolvedDatabaseProperties(
    string? FilterProperty,
    string? SortProperty,
    string? IncludeSlugProperty);

internal static class NotionDatabaseSchemaResolver
{
    internal static async Task<NotionResolvedDatabaseProperties> ResolveAsync(
        NotionApiClient client,
        NotionProviderOptions options,
        CancellationToken cancellationToken)
    {
        var filterType = (options.FilterType ?? "checkbox_true").Trim().ToLowerInvariant();
        var filterProp = filterType == "checkbox_true" ? (options.FilterProperty ?? "Published").Trim() : null;
        var sortProp = options.SortProperty?.Trim();
        var includeSlugProp = options.IncludeSlugs is { Count: > 0 } ? (options.IncludeSlugProperty ?? "Slug").Trim() : null;

        if (string.IsNullOrWhiteSpace(filterProp) &&
            string.IsNullOrWhiteSpace(sortProp) &&
            string.IsNullOrWhiteSpace(includeSlugProp))
        {
            return new NotionResolvedDatabaseProperties(null, null, null);
        }

        using var doc = await client.GetAsync(NotionApiUrls.Database(options.DatabaseId), cancellationToken);
        var root = doc.RootElement;

        if (!root.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
        {
            throw new ContentException("Notion database schema missing properties.");
        }

        var map = BuildPropertyMap(props);

        return new NotionResolvedDatabaseProperties(
            ResolveRequired(map, filterProp, "Notion database property"),
            ResolveRequired(map, sortProp, "Notion database property"),
            ResolveRequired(map, includeSlugProp, "Notion database property"));
    }

    private static Dictionary<string, string> BuildPropertyMap(JsonElement props)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in props.EnumerateObject())
        {
            var name = prop.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (map.TryGetValue(name, out var existing))
            {
                throw new ContentException(
                    $"Notion database has conflicting property names ignoring case: '{existing}' and '{name}'. " +
                    "Rename one of them to a unique name (case-insensitive).");
            }

            map[name] = name;
        }

        return map;
    }

    private static string? ResolveRequired(
        IReadOnlyDictionary<string, string> map,
        string? propertyName,
        string messagePrefix)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        if (map.TryGetValue(propertyName, out var resolved))
        {
            return resolved;
        }

        var available = string.Join(", ", map.Values.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        throw new ContentException(
            $"{messagePrefix} '{propertyName}' not found (case-insensitive match). " +
            $"Available properties: {available}.");
    }
}
