using Bukit.Engine.Abstractions.Content;
using System.Text;

namespace Bukit.Content.Notion;

internal static class NotionDatabaseQueryBuilder
{
    internal static string Build(
        NotionContentSourceOptions options,
        string? startCursor,
        string? resolvedFilterProperty,
        string? resolvedSortProperty,
        string? resolvedIncludeSlugProperty)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"page_size\":{options.PageSize},");

        var filters = new List<string>();
        var filterType = (options.FilterType ?? "checkbox_true").Trim().ToLowerInvariant();
        if (filterType is not "none")
        {
            var prop = (resolvedFilterProperty ?? options.FilterProperty ?? "Published").Trim();
            var filterValue = options.FilterValue?.Trim() ?? string.Empty;
            var filter = filterType switch
            {
                "checkbox_true" => $"{{\"property\":\"{EscapeJson(prop)}\",\"checkbox\":{{\"equals\":true}}}}",
                "checkbox_false" => $"{{\"property\":\"{EscapeJson(prop)}\",\"checkbox\":{{\"equals\":false}}}}",
                "select_equals" => BuildEqualsFilter(prop, "select", filterValue),
                "status_equals" => BuildEqualsFilter(prop, "status", filterValue),
                "rich_text_equals" => BuildEqualsFilter(prop, "rich_text", filterValue),
                _ => null
            };

            if (filter is not null)
            {
                filters.Add(filter);
            }
        }

        if (options.IncludeSlugs is { Count: > 0 })
        {
            var prop = (resolvedIncludeSlugProperty ?? options.IncludeSlugProperty ?? "Slug").Trim();
            var ors = options.IncludeSlugs
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{{\"property\":\"{EscapeJson(prop)}\",\"rich_text\":{{\"equals\":\"{EscapeJson(x)}\"}}}}")
                .ToList();

            if (ors.Count > 0)
            {
                filters.Add($"{{\"or\":[{string.Join(",", ors)}]}}");
            }
        }

        if (filters.Count == 1)
        {
            sb.Append($"\"filter\":{filters[0]},");
        }
        else if (filters.Count > 1)
        {
            sb.Append($"\"filter\":{{\"and\":[{string.Join(",", filters)}]}},");
        }

        if (!string.IsNullOrWhiteSpace(resolvedSortProperty ?? options.SortProperty))
        {
            var prop = (resolvedSortProperty ?? options.SortProperty)!.Trim();
            var dir = (options.SortDirection ?? "ascending").Trim().ToLowerInvariant();
            if (dir is not ("ascending" or "descending"))
            {
                dir = "ascending";
            }

            sb.Append("\"sorts\":[{");
            sb.Append($"\"property\":\"{EscapeJson(prop)}\",");
            sb.Append($"\"direction\":\"{EscapeJson(dir)}\"");
            sb.Append("}],");
        }

        if (!string.IsNullOrWhiteSpace(startCursor))
        {
            sb.Append($"\"start_cursor\":\"{EscapeJson(startCursor)}\"");
        }
        else if (sb[^1] == ',')
        {
            sb.Length--;
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string BuildEqualsFilter(string property, string notionFilterKey, string value)
    {
        return $"{{\"property\":\"{EscapeJson(property)}\",\"{notionFilterKey}\":{{\"equals\":\"{EscapeJson(value)}\"}}}}";
    }
}
