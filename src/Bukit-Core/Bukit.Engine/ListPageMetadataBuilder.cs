using Bukit.Config;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static class ListPageMetadataBuilder
{
    internal static string BuildTitle(SiteModel siteModel, RouteInfo route, ListPaginationModel? pagination = null)
        => BuildTitle(route.Url, ResolveSiteTitle(siteModel.Title, siteModel.Name), pagination);

    internal static string BuildTitle(SiteConfig site, RouteInfo route, ListPaginationModel? pagination = null)
        => BuildTitle(route.Url, ResolveSiteTitle(site.Title, site.Name), pagination);

    internal static string BuildTitle(SiteConfig site, ListRoutePlan route, ListPaginationModel? pagination = null)
        => BuildTitle(route.Url, ResolveSiteTitle(site.Title, site.Name), pagination, route.Title);

    internal static string BuildTitle(ListRoutePlan route, ListPaginationModel? pagination = null)
        => BuildTitle(route.Url, "Site", pagination, route.Title);

    internal static string BuildSummary(SiteModel siteModel, RouteInfo route, int? itemCount = null, ListPaginationModel? pagination = null)
        => BuildSummary(
            siteModel.Title,
            siteModel.Name,
            siteModel.Description,
            route.Url,
            itemCount,
            pagination);

    internal static string BuildSummary(SiteConfig site, RouteInfo route, int? itemCount = null, ListPaginationModel? pagination = null)
        => BuildSummary(
            site.Title,
            site.Name,
            site.Description,
            route.Url,
            itemCount,
            pagination);

    internal static string BuildSummary(SiteConfig site, ListRoutePlan route, ListPaginationModel? pagination = null)
        => BuildSummary(
            site.Title,
            site.Name,
            site.Description,
            route.Url,
            route.TotalItems,
            pagination,
            route.Summary);

    internal static string BuildSummary(ListRoutePlan route, ListPaginationModel? pagination = null)
        => BuildSummary(null, null, null, route.Url, route.TotalItems, pagination, route.Summary);

    internal static ListPaginationModel? BuildPagination(ListRoutePlan route)
    {
        if (route.PageSize is null)
        {
            return null;
        }

        return new ListPaginationModel
        {
            Page = route.PageNumber ?? 1,
            PageSize = route.PageSize.Value,
            TotalPages = route.TotalPages ?? 1,
            TotalItems = route.TotalItems,
            HasPrev = !string.IsNullOrWhiteSpace(route.PrevUrl),
            HasNext = !string.IsNullOrWhiteSpace(route.NextUrl),
            PrevUrl = route.PrevUrl,
            NextUrl = route.NextUrl
        };
    }

    private static string BuildTitle(string url, string siteTitle, ListPaginationModel? pagination, string? titleOverride = null)
    {
        if (url == "/")
        {
            return string.IsNullOrWhiteSpace(titleOverride) ? siteTitle : titleOverride.Trim();
        }

        var baseTitle = string.IsNullOrWhiteSpace(titleOverride)
            ? BuildBaseListTitle(url, pagination)
            : titleOverride.Trim();
        return pagination?.Page > 1
            ? $"{baseTitle} - Page {pagination.Page}"
            : baseTitle;
    }

    private static string BuildSummary(
        string? siteTitle,
        string? siteName,
        string? siteDescription,
        string url,
        int? itemCount,
        ListPaginationModel? pagination,
        string? summaryOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(summaryOverride) && pagination?.Page is not > 1)
        {
            return summaryOverride.Trim();
        }

        if (!string.IsNullOrWhiteSpace(summaryOverride) && pagination?.Page > 1)
        {
            var range = BuildVisibleRange(pagination);
            return range is null
                ? $"{summaryOverride.Trim()} Browse page {pagination.Page}."
                : $"{summaryOverride.Trim()} Browse page {pagination.Page}, showing {range}.";
        }

        if (!string.IsNullOrWhiteSpace(siteDescription) && url == "/")
        {
            return siteDescription!;
        }

        var resolvedSiteTitle = ResolveSiteTitle(siteTitle, siteName);
        if (url == "/")
        {
            return itemCount is > 0
                ? $"Browse {itemCount} content items from {resolvedSiteTitle}."
                : $"Browse the latest content from {resolvedSiteTitle}.";
        }

        var baseTitle = BuildBaseListTitle(url, pagination);
        if (pagination?.Page > 1)
        {
            var range = BuildVisibleRange(pagination);
            return range is null
                ? $"Browse page {pagination.Page} of {baseTitle} from {resolvedSiteTitle}."
                : $"Browse page {pagination.Page} of {baseTitle} from {resolvedSiteTitle}, showing {range}.";
        }

        var count = itemCount ?? pagination?.TotalItems;
        return count is > 0
            ? $"Browse {count} items in {baseTitle} from {resolvedSiteTitle}."
            : $"Browse {baseTitle} from {resolvedSiteTitle}.";
    }

    private static string? BuildVisibleRange(ListPaginationModel pagination)
    {
        var page = Math.Max(1, pagination.Page);
        var pageSize = pagination.PageSize.GetValueOrDefault();
        if (pagination.TotalItems <= 0 || pageSize <= 0)
        {
            return null;
        }

        var start = ((page - 1) * pageSize) + 1;
        if (start > pagination.TotalItems)
        {
            return null;
        }

        var end = Math.Min(pagination.TotalItems, page * pageSize);
        return start == end
            ? $"item {start} of {pagination.TotalItems}"
            : $"items {start}-{end} of {pagination.TotalItems}";
    }

    private static string BuildBaseListTitle(string url, ListPaginationModel? pagination)
    {
        var segments = (url ?? string.Empty)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (pagination is not null &&
            segments.Count >= 2 &&
            int.TryParse(segments[^1], out var page) &&
            page == pagination.Page)
        {
            segments.RemoveRange(segments.Count - 2, 2);
        }

        var lastSegment = segments.LastOrDefault();
        if (string.IsNullOrWhiteSpace(lastSegment))
        {
            return "Index";
        }

        return char.ToUpperInvariant(lastSegment[0]) + lastSegment[1..].Replace('-', ' ');
    }

    private static string ResolveSiteTitle(string? title, string? name)
        => string.IsNullOrWhiteSpace(title)
            ? string.IsNullOrWhiteSpace(name) ? "Site" : name
            : title;
}
