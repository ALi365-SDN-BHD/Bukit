using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed record ListPageContext
{
    public ListPaginationModel? Pagination { get; init; }
    public ListCollectionModel? Collection { get; init; }
    public ListTaxonomyModel? Taxonomy { get; init; }
    public ListFilterModel? Filter { get; init; }
}
