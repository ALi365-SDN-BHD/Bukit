using Bukit.Config;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static class ListPageMetadataBuilder
{
    internal static string BuildTitle(SiteModel siteModel, RouteInfo route, ListPaginationModel? pagination = null)
        => BuildTitle(route.Url, ResolveSiteTitle(siteModel.Title, siteModel.Name), pagination, language: siteModel.Language);

    internal static string BuildTitle(SiteConfig site, RouteInfo route, ListPaginationModel? pagination = null)
        => BuildTitle(route.Url, ResolveSiteTitle(site.Title, site.Name), pagination, language: site.Language);

    internal static string BuildTitle(SiteConfig site, ListRoutePlan route, ListPaginationModel? pagination = null)
        => BuildTitle(route.Url, ResolveSiteTitle(site.Title, site.Name), pagination, route.Title, site.Language);

    internal static string BuildTitle(ListRoutePlan route, ListPaginationModel? pagination = null, string? language = null)
        => BuildTitle(route.Url, "Site", pagination, route.Title, language);

    internal static string BuildSummary(SiteModel siteModel, RouteInfo route, int? itemCount = null, ListPaginationModel? pagination = null)
        => BuildSummary(
            siteModel.Title,
            siteModel.Name,
            siteModel.Description,
            route.Url,
            itemCount,
            pagination,
            language: siteModel.Language);

    internal static string BuildSummary(SiteConfig site, RouteInfo route, int? itemCount = null, ListPaginationModel? pagination = null)
        => BuildSummary(
            site.Title,
            site.Name,
            site.Description,
            route.Url,
            itemCount,
            pagination,
            language: site.Language);

    internal static string BuildSummary(SiteConfig site, ListRoutePlan route, ListPaginationModel? pagination = null)
        => BuildSummary(
            site.Title,
            site.Name,
            site.Description,
            route.Url,
            route.TotalItems,
            pagination,
            route.Summary,
            site.Language);

    internal static string BuildSummary(ListRoutePlan route, ListPaginationModel? pagination = null, string? language = null)
        => BuildSummary(null, null, null, route.Url, route.TotalItems, pagination, route.Summary, language);

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

    private static string BuildTitle(
        string url,
        string siteTitle,
        ListPaginationModel? pagination,
        string? titleOverride = null,
        string? language = null)
    {
        if (url == "/")
        {
            return string.IsNullOrWhiteSpace(titleOverride) ? siteTitle : titleOverride.Trim();
        }

        var baseTitle = string.IsNullOrWhiteSpace(titleOverride)
            ? BuildBaseListTitle(url, pagination)
            : titleOverride.Trim();
        return PaginationMetadataFormatter.FormatTitle(baseTitle, pagination, language);
    }

    private static string BuildSummary(
        string? siteTitle,
        string? siteName,
        string? siteDescription,
        string url,
        int? itemCount,
        ListPaginationModel? pagination,
        string? summaryOverride = null,
        string? language = null)
    {
        if (!string.IsNullOrWhiteSpace(summaryOverride) && pagination?.Page is not > 1)
        {
            return summaryOverride.Trim();
        }

        if (!string.IsNullOrWhiteSpace(summaryOverride) && pagination?.Page > 1)
        {
            return PaginationMetadataFormatter.FormatExplicitDescription(summaryOverride, pagination, language);
        }

        if (!string.IsNullOrWhiteSpace(siteDescription) && url == "/")
        {
            return siteDescription!;
        }

        var resolvedSiteTitle = ResolveSiteTitle(siteTitle, siteName);
        if (url == "/")
        {
            if (PaginationMetadataFormatter.IsChinese(language))
            {
                return itemCount is > 0
                    ? $"浏览 {resolvedSiteTitle} 的 {itemCount} 项内容。"
                    : $"浏览 {resolvedSiteTitle} 的最新内容。";
            }

            return itemCount is > 0
                ? $"Browse {itemCount} content items from {resolvedSiteTitle}."
                : $"Browse the latest content from {resolvedSiteTitle}.";
        }

        var baseTitle = BuildBaseListTitle(url, pagination);
        if (pagination?.Page > 1)
        {
            return PaginationMetadataFormatter.FormatGeneratedDescription(
                baseTitle, resolvedSiteTitle, pagination, language);
        }

        var count = itemCount ?? pagination?.TotalItems;
        if (PaginationMetadataFormatter.IsChinese(language))
        {
            return count is > 0
                ? $"浏览 {resolvedSiteTitle} 的 {baseTitle}，共 {count} 项。"
                : $"浏览 {resolvedSiteTitle} 的 {baseTitle}。";
        }

        return count is > 0
            ? $"Browse {count} items in {baseTitle} from {resolvedSiteTitle}."
            : $"Browse {baseTitle} from {resolvedSiteTitle}.";
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
