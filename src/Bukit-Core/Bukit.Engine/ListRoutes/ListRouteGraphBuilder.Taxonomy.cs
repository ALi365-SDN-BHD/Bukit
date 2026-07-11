using System.Globalization;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;

namespace Bukit.Engine;

internal static partial class ListRouteGraphBuilder
{
    internal static ListRouteGraph AddDerivedTaxonomyRoutes(
        ListRouteGraph graph,
        IReadOnlyList<RoutedContentDocument> derivedDocuments)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(derivedDocuments);

        if (derivedDocuments.Count == 0)
        {
            return graph;
        }

        var routes = graph.Routes.ToList();
        foreach (var derived in derivedDocuments)
        {
            if (TryBuildTaxonomyRoute(derived, out var route))
            {
                routes.Add(route);
            }
        }

        return routes.Count == graph.Routes.Count ? graph : CreateGraph(routes);
    }

    private static bool TryBuildTaxonomyRoute(RoutedContentDocument derived, out ListRoutePlan route)
    {
        route = null!;
        var document = derived.Document;
        if (!string.Equals(ContentFieldReader.GetText(document.CustomFields, "type"), "derived", StringComparison.OrdinalIgnoreCase) ||
            !TryGetObjectField(document.CustomFields, "taxonomy", out var taxonomy))
        {
            return false;
        }

        var kind = GetString(taxonomy, "kind");
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        if (GetBool(taxonomy, "is_index"))
        {
            route = BuildTaxonomyIndexRoute(derived, kind, taxonomy);
            return true;
        }

        var slug = GetString(taxonomy, "slug");
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        route = BuildTaxonomyTermRoute(derived, kind, slug, taxonomy);
        return true;
    }

    private static ListRoutePlan BuildTaxonomyIndexRoute(
        RoutedContentDocument derived,
        string kind,
        IReadOnlyDictionary<string, object?> taxonomy)
    {
        var terms = GetObjectList(derived.Document.CustomFields, "terms")
            .Select(value => BuildTaxonomyTermItem(kind, value))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();

        return new ListRoutePlan
        {
            RouteId = $"taxonomy:{RouteIdSegment(kind)}:index",
            Kind = ListRouteKind.TaxonomyIndex,
            Url = derived.Route.Url,
            OutputPath = derived.Route.OutputPath,
            Template = derived.Route.Template,
            PageNumber = 1,
            TotalItems = terms.Length,
            Items = terms,
            CanonicalUrl = derived.Route.Url,
            MetadataRouteUrl = derived.Route.Url,
            TaxonomyContext = new ListRouteTaxonomyContext
            {
                Kind = kind,
                RoutePrefix = GetString(taxonomy, "route_prefix") ?? GetString(taxonomy, "routePrefix"),
                Url = GetString(taxonomy, "url"),
                IsIndex = true
            }
        };
    }

    private static ListRoutePlan BuildTaxonomyTermRoute(
        RoutedContentDocument derived,
        string kind,
        string slug,
        IReadOnlyDictionary<string, object?> taxonomy)
    {
        var items = GetObjectList(derived.Document.CustomFields, "items")
            .Select(BuildTaxonomyPageItem)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
        TryGetObjectField(derived.Document.CustomFields, "pagination", out var pagination);
        var page = Math.Max(1, GetInt(pagination, "page") ?? 1);
        var totalItems = Math.Max(items.Length, GetInt(pagination, "total") ?? items.Length);
        var pageSize = GetInt(pagination, "page_size");
        var metadataRouteUrl = ResolveTaxonomyMetadataRouteUrl(derived.Route.Url, page);

        return new ListRoutePlan
        {
            RouteId = $"taxonomy:{RouteIdSegment(kind)}:{RouteIdSegment(slug)}:{page}",
            Kind = ListRouteKind.TaxonomyTermPage,
            Url = derived.Route.Url,
            OutputPath = derived.Route.OutputPath,
            Template = derived.Route.Template,
            PageNumber = page,
            PageSize = pageSize is > 0 ? pageSize : null,
            TotalItems = totalItems,
            Items = items,
            CanonicalUrl = derived.Route.Url,
            MetadataRouteUrl = metadataRouteUrl,
            PrevUrl = NormalizeOptionalUrl(GetString(pagination, "prev_url")),
            NextUrl = NormalizeOptionalUrl(GetString(pagination, "next_url")),
            TaxonomyContext = new ListRouteTaxonomyContext
            {
                Kind = kind,
                Term = GetString(taxonomy, "term"),
                Slug = slug,
                RoutePrefix = GetString(taxonomy, "route_prefix") ?? GetString(taxonomy, "routePrefix"),
                Url = GetString(taxonomy, "url"),
                IsIndex = false
            }
        };
    }

    private static ListRouteItem? BuildTaxonomyTermItem(string kind, object? value)
    {
        if (!TryAsObjectMap(value, out var map))
        {
            return null;
        }

        var slug = GetString(map, "slug");
        var title = GetString(map, "title") ?? slug;
        var url = NormalizeOptionalUrl(GetString(map, "url"));
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return new ListRouteItem
        {
            Id = $"taxonomy:{RouteIdSegment(kind)}:{RouteIdSegment(slug)}",
            Title = title,
            Url = url,
            Summary = GetString(map, "description"),
            Fields = BuildFieldsFromRawMap(map, "title", "url")
        };
    }

    private static ListRouteItem? BuildTaxonomyPageItem(object? value)
    {
        if (!TryAsObjectMap(value, out var map))
        {
            return null;
        }

        var id = GetString(map, "id") ?? GetString(map, "url");
        var title = GetString(map, "title");
        var url = NormalizeOptionalUrl(GetString(map, "url"));
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return new ListRouteItem
        {
            Id = id,
            Title = title,
            Url = url,
            Summary = GetString(map, "summary"),
            PublishDate = GetDateTimeOffset(map, "publish_date"),
            Fields = BuildFieldsFromFieldMap(map)
        };
    }

    private static bool TryGetObjectField(
        IReadOnlyDictionary<string, ContentField>? fields,
        string key,
        out IReadOnlyDictionary<string, object?> value)
    {
        value = null!;
        if (!ContentFieldReader.TryGetField(fields, key, out var field) ||
            !TryAsObjectMap(field.Value, out var map))
        {
            return false;
        }

        value = map;
        return true;
    }

    private static IReadOnlyList<object?> GetObjectList(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (!ContentFieldReader.TryGetField(fields, key, out var field) || field.Value is null)
        {
            return Array.Empty<object?>();
        }

        return field.Value is IEnumerable<object?> values && field.Value is not string
            ? values.ToArray()
            : Array.Empty<object?>();
    }

    private static bool TryAsObjectMap(object? value, out IReadOnlyDictionary<string, object?> map)
    {
        map = null!;
        if (value is IReadOnlyDictionary<string, object?> readOnly)
        {
            map = readOnly;
            return true;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            map = new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);
            return true;
        }

        return false;
    }

    private static string? GetString(IReadOnlyDictionary<string, object?>? map, string key)
    {
        if (map is null || !map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        var text = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool GetBool(IReadOnlyDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        if (value is bool flag)
        {
            return flag;
        }

        return bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    private static int? GetInt(IReadOnlyDictionary<string, object?>? map, string key)
    {
        if (map is null || !map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l when l <= int.MaxValue && l >= int.MinValue => (int)l,
            double d => (int)d,
            decimal d => (int)d,
            _ => int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null
        };
    }

    private static DateTimeOffset? GetDateTimeOffset(IReadOnlyDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, dt.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : dt.Kind)),
            _ => DateTimeOffset.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : null
        };
    }

    private static string? NormalizeOptionalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return RoutePathBuilder.NormalizeUrl(url);
    }

    private static string ResolveTaxonomyMetadataRouteUrl(string routeUrl, int page)
    {
        var normalized = RoutePathBuilder.NormalizeUrl(routeUrl);
        if (page <= 1)
        {
            return normalized;
        }

        var segments = normalized.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 &&
            string.Equals(segments[^2], "page", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(segments[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var routePage) &&
            routePage == page)
        {
            return "/" + string.Join('/', segments[..^2]) + "/";
        }

        return normalized;
    }

    private static IReadOnlyDictionary<string, ContentField>? BuildFieldsFromFieldMap(IReadOnlyDictionary<string, object?> map)
    {
        if (!map.TryGetValue("fields", out var rawFields) || !TryAsObjectMap(rawFields, out var fieldMap))
        {
            return null;
        }

        var result = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in fieldMap)
        {
            if (!TryAsObjectMap(value, out var fieldValue))
            {
                continue;
            }

            var type = GetString(fieldValue, "type") ?? "object";
            fieldValue.TryGetValue("value", out var rawValue);
            result[key] = new ContentField(type, rawValue);
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyDictionary<string, ContentField>? BuildFieldsFromRawMap(
        IReadOnlyDictionary<string, object?> map,
        params string[] excludeKeys)
    {
        var exclude = excludeKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in map)
        {
            if (exclude.Contains(key))
            {
                continue;
            }

            result[key] = new ContentField(InferContentFieldType(value), value);
        }

        return result.Count == 0 ? null : result;
    }

    private static string InferContentFieldType(object? value)
    {
        return value switch
        {
            null => "object",
            string => "text",
            bool => "boolean",
            int or long or float or double or decimal => "number",
            DateTime or DateTimeOffset => "date",
            IReadOnlyDictionary<string, object?> or IDictionary<string, object?> => "object",
            IEnumerable<object?> => "list",
            _ => "object"
        };
    }
}
