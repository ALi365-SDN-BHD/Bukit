using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class ListRouteRenderPlanBuilder
{
    internal static IReadOnlyList<SpecialListDefinition> Build(
        ListRouteGraph graph,
        IReadOnlyList<RoutedContentDocument> routed,
        string layoutsDir,
        string listPageContentMode)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(routed);

        if (graph.Routes.Count == 0)
        {
            return Array.Empty<SpecialListDefinition>();
        }

        var byId = routed.ToDictionary(x => x.Document.Id, StringComparer.OrdinalIgnoreCase);
        var definitions = new List<SpecialListDefinition>(graph.Routes.Count);
        foreach (var route in graph.Routes)
        {
            if (route.Kind is ListRouteKind.TaxonomyIndex or ListRouteKind.TaxonomyTermPage)
            {
                continue;
            }

            definitions.Add(new SpecialListDefinition(
                route.ToRouteInfo(),
                ResolveItems(route, byId),
                TemplateCapabilitiesResolver.ShouldIncludeListPageContent(route.Template, layoutsDir, listPageContentMode),
                BuildPageFields(route),
                BuildPageContext(route)));
        }

        return definitions;
    }

    private static IReadOnlyList<RoutedContentDocument> ResolveItems(
        ListRoutePlan route,
        IReadOnlyDictionary<string, RoutedContentDocument> byId)
    {
        var items = new List<RoutedContentDocument>(route.Items.Count);
        foreach (var item in route.Items)
        {
            if (!byId.TryGetValue(item.Id, out var routed))
            {
                throw new ConfigException(
                    $"List route graph item '{item.Id}' was not found in routed content for route '{route.RouteId}'.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            items.Add(routed);
        }

        return items;
    }

    internal static IReadOnlyDictionary<string, ContentField> BuildPageFields(ListRoutePlan route)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["items"] = new("list", BuildItems(route.Items))
        };

        if (route.PageSize is not null)
        {
            fields["pagination"] = new("object", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["page"] = route.PageNumber ?? 1,
                ["page_size"] = route.PageSize.Value,
                ["total_pages"] = route.TotalPages ?? 1,
                ["total_items"] = route.TotalItems,
                ["total"] = route.TotalItems,
                ["has_prev"] = !string.IsNullOrWhiteSpace(route.PrevUrl),
                ["has_next"] = !string.IsNullOrWhiteSpace(route.NextUrl),
                ["prev_url"] = route.PrevUrl,
                ["next_url"] = route.NextUrl
            });
        }

        if (!string.IsNullOrWhiteSpace(route.Collection))
        {
            fields["collection"] = new("object", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["key"] = route.Collection
            });
        }

        if (route.TaxonomyContext is not null)
        {
            fields["taxonomy"] = new("object", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = route.TaxonomyContext.Kind,
                ["term"] = route.TaxonomyContext.Term,
                ["slug"] = route.TaxonomyContext.Slug,
                ["route_prefix"] = route.TaxonomyContext.RoutePrefix,
                ["routePrefix"] = route.TaxonomyContext.RoutePrefix,
                ["url"] = route.TaxonomyContext.Url,
                ["is_index"] = route.TaxonomyContext.IsIndex
            });

            if (route.TaxonomyContext.IsIndex)
            {
                fields["terms"] = new("list", BuildItems(route.Items));
            }
        }

        if (route.FilterContext is not null)
        {
            fields["filter"] = new("object", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["field"] = route.FilterContext.Field,
                ["operator"] = route.FilterContext.Operator,
                ["value"] = route.FilterContext.Value,
                ["values"] = route.FilterContext.Values
            });
        }

        return fields;
    }

    internal static ListPageContext BuildPageContext(ListRoutePlan route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new ListPageContext
        {
            Pagination = route.PageSize is null
                ? null
                : new ListPaginationModel
                {
                    Page = route.PageNumber ?? 1,
                    PageSize = route.PageSize.Value,
                    TotalPages = route.TotalPages ?? 1,
                    TotalItems = route.TotalItems,
                    HasPrev = !string.IsNullOrWhiteSpace(route.PrevUrl),
                    HasNext = !string.IsNullOrWhiteSpace(route.NextUrl),
                    PrevUrl = route.PrevUrl,
                    NextUrl = route.NextUrl
                },
            Collection = string.IsNullOrWhiteSpace(route.Collection)
                ? null
                : new ListCollectionModel
                {
                    Key = route.Collection
                },
            Taxonomy = route.TaxonomyContext is null
                ? null
                : new ListTaxonomyModel
                {
                    Kind = route.TaxonomyContext.Kind,
                    Term = route.TaxonomyContext.Term,
                    Slug = route.TaxonomyContext.Slug,
                    RoutePrefix = route.TaxonomyContext.RoutePrefix,
                    Url = route.TaxonomyContext.Url,
                    IsIndex = route.TaxonomyContext.IsIndex
                },
            Filter = route.FilterContext is null
                ? null
                : new ListFilterModel
                {
                    Field = route.FilterContext.Field,
                    Operator = route.FilterContext.Operator,
                    Value = route.FilterContext.Value,
                    Values = route.FilterContext.Values
                }
        };
    }

    private static List<object> BuildItems(IReadOnlyList<ListRouteItem> source)
    {
        var items = new List<object>(source.Count);
        foreach (var item in source)
        {
            var entry = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = item.Title,
                ["url"] = item.Url,
                ["publish_date"] = item.PublishDate?.DateTime
            };

            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                entry["summary"] = item.Summary;
            }

            if (item.Fields is { Count: > 0 })
            {
                var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, field) in item.Fields)
                {
                    fields[key] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = field.Type,
                        ["value"] = field.Value
                    };
                }

                entry["fields"] = fields;
            }

            items.Add(entry);
        }

        return items;
    }
}
