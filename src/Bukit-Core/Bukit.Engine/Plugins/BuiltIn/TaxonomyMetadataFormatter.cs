using Bukit.Rendering;

namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyMetadataFormatter
{
    internal static (string Title, string SingularTitlePrefix) ResolveBuiltInTitles(
        string kind,
        string? language)
    {
        var isChinese = PaginationMetadataFormatter.IsChinese(language);
        return kind.ToLowerInvariant() switch
        {
            "tags" when isChinese => ("标签", "标签"),
            "categories" when isChinese => ("分类", "分类"),
            "tags" => ("Tags", "Tag"),
            "categories" => ("Categories", "Category"),
            _ => (kind, kind)
        };
    }

    internal static string FormatIndexSummary(string title, string? description, string? language)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description.Trim();
        }

        return PaginationMetadataFormatter.IsChinese(language)
            ? $"浏览全部{title.Trim()}。"
            : $"Browse all {title.Trim()}.";
    }

    internal static string FormatTermTitle(
        string singularTitlePrefix,
        string term,
        int page,
        int pageSize,
        int totalPages,
        int totalItems,
        string? language)
    {
        var baseTitle = PaginationMetadataFormatter.IsChinese(language)
            ? $"{singularTitlePrefix.Trim()}：{term.Trim()}"
            : $"{singularTitlePrefix.Trim()}: {term.Trim()}";

        return PaginationMetadataFormatter.FormatTitle(
            baseTitle,
            BuildPagination(page, pageSize, totalPages, totalItems),
            language);
    }

    internal static string FormatTermSummary(
        string kind,
        TaxonomyTerm term,
        int page,
        int pageSize,
        int totalPages,
        string? language)
    {
        var pagination = BuildPagination(page, pageSize, totalPages, term.Pages.Count);
        if (!string.IsNullOrWhiteSpace(term.Description))
        {
            return PaginationMetadataFormatter.FormatExplicitDescription(
                term.Description,
                pagination,
                language);
        }

        if (PaginationMetadataFormatter.IsChinese(language))
        {
            if (page > 1)
            {
                var generated = PaginationMetadataFormatter.FormatExplicitDescription(
                    $"浏览“{term.DisplayName}”下的内容，",
                    pagination,
                    language);
                return generated.Replace("， 第", "，第", StringComparison.Ordinal);
            }

            return term.Pages.Count > 0
                ? $"浏览“{term.DisplayName}”下的内容，共 {term.Pages.Count} 项。"
                : $"浏览“{term.DisplayName}”下的内容。";
        }

        var relation = string.Equals(kind, "tags", StringComparison.OrdinalIgnoreCase)
            ? "tagged"
            : "in";
        if (page > 1)
        {
            return FormatEnglishGeneratedPagination(term.DisplayName, relation, pagination);
        }

        return term.Pages.Count > 0
            ? $"Browse {term.Pages.Count} content items {relation} {term.DisplayName}."
            : $"Browse content {relation} {term.DisplayName}.";
    }

    private static string FormatEnglishGeneratedPagination(
        string term,
        string relation,
        ListPaginationModel pagination)
    {
        var start = ((pagination.Page - 1) * pagination.PageSize.GetValueOrDefault()) + 1;
        var end = Math.Min(pagination.TotalItems, pagination.Page * pagination.PageSize.GetValueOrDefault());
        var range = start == end
            ? $"item {start} of {pagination.TotalItems}"
            : $"items {start}-{end} of {pagination.TotalItems}";
        return $"Browse content {relation} {term}, page {pagination.Page}, showing {range}.";
    }

    private static ListPaginationModel BuildPagination(
        int page,
        int pageSize,
        int totalPages,
        int totalItems)
        => new()
        {
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalItems = totalItems
        };
}
