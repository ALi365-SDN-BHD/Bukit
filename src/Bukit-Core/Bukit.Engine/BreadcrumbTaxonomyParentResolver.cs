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

        // Terms are identified by slug: names can repeat across a hierarchy, slugs cannot.
        var termPages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var termNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in listRouteGraph.Routes)
        {
            if (route.Kind != ListRouteKind.TaxonomyTermPage ||
                route.TaxonomyContext is not { IsIndex: false } taxonomy ||
                !string.Equals(taxonomy.Kind, kind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var slug = taxonomy.Slug ?? taxonomy.Term ?? route.RouteId;
            termNames.TryAdd(slug, taxonomy.Term ?? slug);
            if ((route.PageNumber ?? 1) == 1)
            {
                termPages.TryAdd(slug, BreadcrumbDescriptorResolver.NormalizeForComparison(route.Url));
            }

            foreach (var item in route.Items)
            {
                var itemKey = BreadcrumbDescriptorResolver.NormalizeForComparison(item.Url);
                if (!candidates.TryGetValue(itemKey, out var slugs))
                {
                    candidates[itemKey] = slugs = new List<string>();
                }

                if (!slugs.Contains(slug, StringComparer.OrdinalIgnoreCase))
                {
                    slugs.Add(slug);
                }
            }
        }

        var fieldKey = ResolveFieldKey(config, kind);
        foreach (var routedDocument in routed)
        {
            var key = BreadcrumbDescriptorResolver.NormalizeForComparison(routedDocument.Route.Url);
            if (!candidates.TryGetValue(key, out var slugs))
            {
                continue;
            }

            // Prefer the document's own term order; fall back to term-route order.
            var declared = ContentFieldReader.GetTextList(routedDocument.Document.CustomFields, fieldKey) ?? [];
            var slug = declared
                .Select(value => value.Trim())
                .Select(value => slugs.FirstOrDefault(candidate =>
                    string.Equals(termNames[candidate], value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(candidate => candidate is not null)
                ?? slugs[0];
            if (termPages.TryGetValue(slug, out var parentKey) && !string.Equals(parentKey, key, StringComparison.OrdinalIgnoreCase))
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
