using Bukit.Rendering;

namespace Bukit.Engine;

internal static class PaginationMetadataFormatter
{
    internal static string FormatTitle(string title, ListPaginationModel? pagination, string? language)
    {
        var baseTitle = title.Trim();
        if (pagination?.Page is not > 1)
        {
            return baseTitle;
        }

        return IsChinese(language)
            ? $"{baseTitle} - 第 {pagination.Page} 页"
            : $"{baseTitle} - Page {pagination.Page}";
    }

    internal static string FormatExplicitDescription(
        string description,
        ListPaginationModel? pagination,
        string? language)
    {
        var text = description.Trim();
        if (pagination?.Page is not > 1)
        {
            return text;
        }

        var range = ResolveRange(pagination);
        if (IsChinese(language))
        {
            return range is null
                ? $"{text} 第 {pagination.Page} 页。"
                : $"{text} 第 {pagination.Page} 页，{FormatChineseRange(range.Value, pagination.TotalItems)}";
        }

        return range is null
            ? $"{text} Browse page {pagination.Page}."
            : $"{text} Browse page {pagination.Page}, showing {FormatEnglishRange(range.Value, pagination.TotalItems)}";
    }

    internal static string FormatGeneratedDescription(
        string baseTitle,
        string siteTitle,
        ListPaginationModel pagination,
        string? language)
    {
        var range = ResolveRange(pagination);
        if (IsChinese(language))
        {
            return range is null
                ? $"浏览 {siteTitle} 的 {baseTitle}，第 {pagination.Page} 页。"
                : $"浏览 {siteTitle} 的 {baseTitle}，第 {pagination.Page} 页，{FormatChineseRange(range.Value, pagination.TotalItems)}";
        }

        return range is null
            ? $"Browse page {pagination.Page} of {baseTitle} from {siteTitle}."
            : $"Browse page {pagination.Page} of {baseTitle} from {siteTitle}, showing {FormatEnglishRange(range.Value, pagination.TotalItems)}";
    }

    internal static bool IsChinese(string? language)
        => !string.IsNullOrWhiteSpace(language) &&
           language.Trim().StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    private static (int Start, int End)? ResolveRange(ListPaginationModel pagination)
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

        return (start, Math.Min(pagination.TotalItems, page * pageSize));
    }

    private static string FormatChineseRange((int Start, int End) range, int totalItems)
        => range.Start == range.End
            ? $"显示第 {range.Start} 项，共 {totalItems} 项。"
            : $"显示第 {range.Start}-{range.End} 项，共 {totalItems} 项。";

    private static string FormatEnglishRange((int Start, int End) range, int totalItems)
        => range.Start == range.End
            ? $"item {range.Start} of {totalItems}."
            : $"items {range.Start}-{range.End} of {totalItems}.";
}
