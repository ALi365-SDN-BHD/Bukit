using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

/// <summary>
/// Maps content routes to the first page of one of their taxonomy terms (site.seo.schema.breadcrumbParent).
/// </summary>
internal static class BreadcrumbTaxonomyParentResolver
{
    internal static IReadOnlyDictionary<string, string> Resolve(
        AppConfig config,
        IReadOnlyList<RoutedContentDocument> routed,
        ListRouteGraph? listRouteGraph)
    {
        var kind = config.Site.Seo.Schema.BreadcrumbParent?.Trim();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(kind) || listRouteGraph is null)
        {
            return result;
        }

        // term text -> first-page route key, and content route key -> terms in term-route order.
        var termPages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in listRouteGraph.Routes)
        {
            if (route.Kind != ListRouteKind.TaxonomyTermPage ||
                route.TaxonomyContext is not { IsIndex: false } taxonomy ||
                !string.Equals(taxonomy.Kind, kind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var term = taxonomy.Term ?? taxonomy.Slug ?? route.RouteId;
            if ((route.PageNumber ?? 1) == 1)
            {
                termPages.TryAdd(term, BreadcrumbDescriptorResolver.NormalizeForComparison(route.Url));
            }

            foreach (var item in route.Items)
            {
                var itemKey = BreadcrumbDescriptorResolver.NormalizeForComparison(item.Url);
                if (!candidates.TryGetValue(itemKey, out var terms))
                {
                    candidates[itemKey] = terms = new List<string>();
                }

                if (!terms.Contains(term, StringComparer.OrdinalIgnoreCase))
                {
                    terms.Add(term);
                }
            }
        }

        var fieldKey = ResolveFieldKey(config, kind);
        foreach (var routedDocument in routed)
        {
            var key = BreadcrumbDescriptorResolver.NormalizeForComparison(routedDocument.Route.Url);
            if (!candidates.TryGetValue(key, out var terms))
            {
                continue;
            }

            // Prefer the document's own term order; fall back to term-route order.
            var declared = ContentFieldReader.GetTextList(routedDocument.Document.CustomFields, fieldKey) ?? [];
            var term = declared
                .Select(value => value.Trim())
                .FirstOrDefault(value => terms.Contains(value, StringComparer.OrdinalIgnoreCase))
                ?? terms[0];
            if (termPages.TryGetValue(term, out var parentKey) && !string.Equals(parentKey, key, StringComparison.OrdinalIgnoreCase))
            {
                result[key] = parentKey;
            }
        }

        return result;
    }

    private static string ResolveFieldKey(AppConfig config, string kind)
        => config.Taxonomy.Kinds?
               .FirstOrDefault(entry => string.Equals(
                   string.IsNullOrWhiteSpace(entry.Kind) ? entry.Key : entry.Kind.Trim(),
                   kind,
                   StringComparison.OrdinalIgnoreCase))?
               .Key ?? kind;
}
