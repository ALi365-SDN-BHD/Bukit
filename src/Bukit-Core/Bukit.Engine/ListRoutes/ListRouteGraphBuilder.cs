using System.Globalization;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class ListRouteGraphBuilder
{
    internal const string BuildContextDataKey = "__list_route_graph";

    internal static ListRouteGraph Build(
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        string outputPathEncoding,
        ThemeTemplateResolver? templateResolver = null)
    {
        ArgumentNullException.ThrowIfNull(routed);

        var index = CollectionRouteIndex.Create(routed);
        var routes = new List<ListRoutePlan>
        {
            BuildHome(index, templateResolver)
        };

        if (collections is null || collections.Count == 0)
        {
            return CreateGraph(routes);
        }

        foreach (var (collectionKey, collection) in collections)
        {
            var listRoute = collection.ListRoute;
            if (string.IsNullOrWhiteSpace(listRoute))
            {
                continue;
            }

            var collectionItems = index.GetByCollection(collectionKey);
            var collectionTemplate = ResolveListTemplate(collection.ListTemplate, templateResolver);
            routes.AddRange(BuildCollectionListRoutes(
                collectionKey,
                collection,
                listRoute,
                collectionItems,
                collectionTemplate,
                outputPathEncoding,
                collection.Pagination.Enabled));

            if (collection.FilteredLists is not { Count: > 0 })
            {
                continue;
            }

            foreach (var filter in collection.FilteredLists)
            {
                routes.AddRange(BuildFilteredListRoutes(
                    collectionKey,
                    filter,
                    collectionItems,
                    collectionTemplate,
                    outputPathEncoding,
                    collection.Pagination));
            }
        }

        return CreateGraph(routes);
    }

    private static ListRoutePlan BuildHome(CollectionRouteIndex index, ThemeTemplateResolver? templateResolver)
    {
        return new ListRoutePlan
        {
            RouteId = "home",
            Kind = ListRouteKind.Home,
            Url = "/",
            OutputPath = "index.html",
            Template = templateResolver?.ResolveHomeTemplate() ?? ThemeTemplateResolver.DefaultHomeTemplate,
            PageNumber = 1,
            TotalItems = index.AllOrdered.Count,
            Items = index.AllOrdered.Select(ListRouteItem.FromRoutedContentDocument).ToArray(),
            CanonicalUrl = "/"
        };
    }

    private static IEnumerable<ListRoutePlan> BuildCollectionListRoutes(
        string collectionKey,
        CollectionConfig collection,
        string listRoute,
        IReadOnlyList<RoutedContentDocument> collectionItems,
        string template,
        string outputPathEncoding,
        bool paginationEnabled)
    {
        var url = RoutePathBuilder.NormalizeListRoute(listRoute);
        if (!paginationEnabled)
        {
            yield return BuildCollectionList(
                collectionKey,
                url,
                collectionItems,
                template,
                outputPathEncoding,
                pageSize: null,
                totalItems: collectionItems.Count,
                nextUrl: null,
                collection.ListTitle,
                collection.ListDescription);
            yield break;
        }

        var pageSize = Math.Max(1, collection.Pagination.PageSize);
        var totalItems = collectionItems.Count;
        var totalPages = CalculateTotalPages(totalItems, pageSize);
        var firstUrl = collection.Pagination.FirstPageUsesListRoute
            ? url
            : BuildCollectionPageUrl(url, collectionKey, collection.Pagination, 1);
        yield return BuildCollectionList(
            collectionKey,
            firstUrl,
            collectionItems.Take(pageSize).ToArray(),
            template,
            outputPathEncoding,
            pageSize,
            totalItems,
            totalPages > 1 ? BuildCollectionPageUrl(url, collectionKey, collection.Pagination, 2) : null,
            collection.ListTitle,
            collection.ListDescription);

        for (var page = 2; page <= totalPages; page++)
        {
            var pageUrl = BuildCollectionPageUrl(url, collectionKey, collection.Pagination, page);
            yield return new ListRoutePlan
            {
                RouteId = $"collection:{RouteIdSegment(collectionKey)}:{page}",
                Kind = ListRouteKind.CollectionPage,
                Url = pageUrl,
                OutputPath = RoutePathBuilder.BuildOutputPathFromUrl(pageUrl, outputPathEncoding),
                Template = template,
                Title = collection.ListTitle,
                Summary = collection.ListDescription,
                Collection = collectionKey,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Items = collectionItems.Skip((page - 1) * pageSize).Take(pageSize).Select(ListRouteItem.FromRoutedContentDocument).ToArray(),
                CanonicalUrl = pageUrl,
                PrevUrl = page == 2 ? firstUrl : BuildCollectionPageUrl(url, collectionKey, collection.Pagination, page - 1),
                NextUrl = page < totalPages ? BuildCollectionPageUrl(url, collectionKey, collection.Pagination, page + 1) : null
            };
        }
    }

    private static ListRoutePlan BuildCollectionList(
        string collectionKey,
        string url,
        IReadOnlyList<RoutedContentDocument> visibleItems,
        string template,
        string outputPathEncoding,
        int? pageSize,
        int totalItems,
        string? nextUrl,
        string? title = null,
        string? summary = null)
    {
        return new ListRoutePlan
        {
            RouteId = $"collection:{RouteIdSegment(collectionKey)}:1",
            Kind = ListRouteKind.CollectionList,
            Url = url,
            OutputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding),
            Template = template,
            Title = title,
            Summary = summary,
            Collection = collectionKey,
            PageNumber = 1,
            PageSize = pageSize,
            TotalItems = totalItems,
            Items = visibleItems.Select(ListRouteItem.FromRoutedContentDocument).ToArray(),
            CanonicalUrl = url,
            NextUrl = nextUrl
        };
    }

    private static IEnumerable<ListRoutePlan> BuildFilteredListRoutes(
        string collectionKey,
        FilteredListConfig filter,
        IReadOnlyList<RoutedContentDocument> collectionItems,
        string collectionTemplate,
        string outputPathEncoding,
        CollectionPaginationConfig collectionPagination)
    {
        var filtered = collectionItems
            .Where(item => FilteredListMatcher.Matches(item.Document.CustomFields, filter))
            .ToArray();
        if (filtered.Length == 0 && IsSkipEmpty(filter.EmptyBehavior))
        {
            yield break;
        }

        var filterOperator = FilteredListMatcher.NormalizeOperator(filter.Operator);
        var filterValues = FilteredListMatcher.ResolveExpectedValues(filter);
        var filterValue = string.IsNullOrWhiteSpace(filter.Value) ? filterValues.FirstOrDefault() : filter.Value.Trim();
        var filterRouteId = BuildFilteredRouteIdPrefix(collectionKey, filter.Field, filterOperator, filterValues);
        var url = RoutePathBuilder.NormalizeListRoute(filter.ListRoute);
        var template = string.IsNullOrWhiteSpace(filter.ListTemplate) ? collectionTemplate : filter.ListTemplate.Trim();
        var pageSize = ResolveFilteredPageSize(filter, collectionPagination);
        var totalPages = CalculateTotalPages(filtered.Length, pageSize);
        var urlPattern = ResolveFilteredUrlPattern(filter, collectionPagination);

        for (var page = 1; page <= totalPages; page++)
        {
            var pageUrl = page == 1
                ? url
                : BuildFilteredPageUrl(url, collectionKey, urlPattern, page);
            var prevUrl = page switch
            {
                1 => null,
                2 => url,
                _ => BuildFilteredPageUrl(url, collectionKey, urlPattern, page - 1)
            };
            var nextUrl = page < totalPages
                ? BuildFilteredPageUrl(url, collectionKey, urlPattern, page + 1)
                : null;

            yield return new ListRoutePlan
            {
                RouteId = $"{filterRouteId}:{page}",
                Kind = ListRouteKind.FilteredListPage,
                Url = pageUrl,
                OutputPath = RoutePathBuilder.BuildOutputPathFromUrl(pageUrl, outputPathEncoding),
                Template = template,
                Title = filter.Title,
                Summary = filter.Description,
                Collection = collectionKey,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = filtered.Length,
                Items = filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(ListRouteItem.FromRoutedContentDocument).ToArray(),
                CanonicalUrl = pageUrl,
                PrevUrl = prevUrl,
                NextUrl = nextUrl,
                FilterContext = new ListRouteFilterContext
                {
                    Field = filter.Field,
                    Operator = filterOperator,
                    Value = filterValue,
                    Values = filterValues
                }
            };
        }
    }

    private static string BuildFilteredRouteIdPrefix(
        string collectionKey,
        string field,
        string filterOperator,
        IReadOnlyList<string> values)
    {
        var valueSegment = RouteIdSegment(values.Count == 0 ? "_" : string.Join("+", values));
        var basePrefix = $"filter:{RouteIdSegment(collectionKey)}:{RouteIdSegment(field)}";
        return string.Equals(filterOperator, "equals", StringComparison.OrdinalIgnoreCase)
            ? $"{basePrefix}:{valueSegment}"
            : $"{basePrefix}:{RouteIdSegment(filterOperator)}:{valueSegment}";
    }

    private static string ResolveListTemplate(string? explicitTemplate, ThemeTemplateResolver? templateResolver)
    {
        if (!string.IsNullOrWhiteSpace(explicitTemplate))
        {
            return explicitTemplate.Trim();
        }

        if (templateResolver is null)
        {
            throw new ConfigException(
                "No list template was configured. Add site.collections.*.listTemplate or a matching theme.yaml templates entry.",
                DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return templateResolver.ResolveKindTemplate("list");
    }

    private static string RouteIdSegment(string value)
    {
        var segment = value.Trim().Replace(':', '-');
        return string.IsNullOrWhiteSpace(segment) ? "_" : segment;
    }

    private static int CalculateTotalPages(int totalItems, int pageSize)
    {
        return Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    private static string BuildCollectionPageUrl(
        string listUrl,
        string collectionKey,
        CollectionPaginationConfig pagination,
        int page)
    {
        var pattern = ResolvePaginationPattern(collectionKey, pagination.UrlPattern, page);
        return $"{listUrl}{pattern}";
    }

    private static string BuildFilteredPageUrl(
        string listUrl,
        string collectionKey,
        string urlPattern,
        int page)
    {
        var pattern = ResolvePaginationPattern(collectionKey, urlPattern, page, "Filtered list pagination urlPattern");
        return $"{listUrl}{pattern}";
    }

    private static string ResolvePaginationPattern(
        string collectionKey,
        string? urlPattern,
        int page,
        string fieldName = "Collection pagination urlPattern")
    {
        if (string.IsNullOrWhiteSpace(urlPattern))
        {
            throw new ConfigException($"{fieldName} must be a non-empty relative URL pattern.", DiagnosticCode.ConfigInvalidValue);
        }

        var pattern = urlPattern.Trim();
        if (pattern.StartsWith('/') ||
            pattern.StartsWith("//", StringComparison.Ordinal) ||
            pattern.Contains("://", StringComparison.Ordinal))
        {
            throw new ConfigException($"{fieldName} must be relative.", DiagnosticCode.ConfigInvalidValue);
        }

        if (pattern.Any(char.IsControl))
        {
            throw new ConfigException($"{fieldName} must not contain control characters.", DiagnosticCode.ConfigInvalidValue);
        }

        if (pattern.Contains('\\'))
        {
            throw new ConfigException($"{fieldName} must not contain backslashes.", DiagnosticCode.ConfigInvalidValue);
        }

        if (pattern.Contains('?') || pattern.Contains('#'))
        {
            throw new ConfigException($"{fieldName} must not contain query strings or fragments.", DiagnosticCode.ConfigInvalidValue);
        }

        foreach (var segment in pattern.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                throw new ConfigException($"{fieldName} must not contain '..' path traversal segments.", DiagnosticCode.ConfigPathTraversal);
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(segment);
            }
            catch
            {
                throw new ConfigException($"{fieldName} must contain valid percent-encoding.", DiagnosticCode.ConfigInvalidValue);
            }

            if (decoded is "." or "..")
            {
                throw new ConfigException($"{fieldName} must not contain '..' path traversal segments.", DiagnosticCode.ConfigPathTraversal);
            }

            if (decoded.Contains('/') || decoded.Contains('\\'))
            {
                throw new ConfigException($"{fieldName} must not contain encoded slashes.", DiagnosticCode.ConfigInvalidValue);
            }
        }

        ValidatePaginationPlaceholders(fieldName, pattern);

        if (!ContainsPagePlaceholder(pattern))
        {
            throw new ConfigException($"{fieldName} must include :num, {{num}}, or {{page}}.", DiagnosticCode.ConfigInvalidValue);
        }

        var collectionSegment = ResolveCollectionPatternSegment(collectionKey);
        var resolved = pattern
            .Replace(":num", page.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{num}", page.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{page}", page.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{collection}", collectionSegment, StringComparison.OrdinalIgnoreCase)
            .Replace("{slug}", collectionSegment, StringComparison.OrdinalIgnoreCase)
            .TrimStart('/');

        return resolved.EndsWith('/') ? resolved : resolved + "/";
    }

    private static void ValidatePaginationPlaceholders(string fieldName, string pattern)
    {
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '}')
            {
                throw new ConfigException($"{fieldName} contains an unopened placeholder.", DiagnosticCode.ConfigInvalidValue);
            }

            if (pattern[i] != '{')
            {
                continue;
            }

            var end = pattern.IndexOf('}', i + 1);
            if (end < 0)
            {
                throw new ConfigException($"{fieldName} contains an unclosed placeholder.", DiagnosticCode.ConfigInvalidValue);
            }

            var placeholder = pattern[(i + 1)..end];
            if (!IsSupportedPaginationPlaceholder(placeholder))
            {
                throw new ConfigException($"{fieldName} contains unsupported placeholder {{{placeholder}}}. Supported placeholders: :num, {{num}}, {{page}}, {{collection}}, {{slug}}.", DiagnosticCode.ConfigInvalidValue);
            }

            i = end;
        }
    }

    private static bool IsSupportedPaginationPlaceholder(string placeholder)
        => placeholder.Equals("num", StringComparison.OrdinalIgnoreCase) ||
           placeholder.Equals("page", StringComparison.OrdinalIgnoreCase) ||
           placeholder.Equals("collection", StringComparison.OrdinalIgnoreCase) ||
           placeholder.Equals("slug", StringComparison.OrdinalIgnoreCase);

    private static int ResolveFilteredPageSize(FilteredListConfig filter, CollectionPaginationConfig collectionPagination)
    {
        if (filter.PageSize is > 0)
        {
            return filter.PageSize.Value;
        }

        return Math.Max(1, collectionPagination.PageSize);
    }

    private static string ResolveFilteredUrlPattern(FilteredListConfig filter, CollectionPaginationConfig collectionPagination)
    {
        return string.IsNullOrWhiteSpace(filter.UrlPattern)
            ? collectionPagination.UrlPattern
            : filter.UrlPattern.Trim();
    }

    private static bool IsSkipEmpty(string? emptyBehavior)
        => string.Equals(emptyBehavior?.Trim(), "skip", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPagePlaceholder(string pattern)
    {
        return pattern.Contains(":num", StringComparison.OrdinalIgnoreCase) ||
               pattern.Contains("{num}", StringComparison.OrdinalIgnoreCase) ||
               pattern.Contains("{page}", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCollectionPatternSegment(string collectionKey)
    {
        var segment = SlugHelper.Slugify(collectionKey);
        return string.IsNullOrWhiteSpace(segment) ? "collection" : segment;
    }

    private static ListRouteGraph CreateGraph(IEnumerable<ListRoutePlan> routes)
    {
        try
        {
            return ListRouteGraph.Create(routes);
        }
        catch (ArgumentException ex)
        {
            throw new ConfigException($"Invalid list route configuration: {ex.Message}", ex, DiagnosticCode.ConfigInvalidValue);
        }
    }
}
