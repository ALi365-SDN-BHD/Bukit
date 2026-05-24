namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyHierarchyBuilder
{
    internal sealed class HierarchyInfo
    {
        public IReadOnlyList<string> Children { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Ancestors { get; init; } = Array.Empty<string>();
    }

    internal static IReadOnlyDictionary<string, HierarchyInfo> BuildHierarchy(Dictionary<string, TaxonomyTerm> terms)
    {
        var childrenMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in terms.Values)
        {
            if (string.IsNullOrWhiteSpace(term.ParentSlug))
            {
                continue;
            }

            if (!terms.ContainsKey(term.ParentSlug))
            {
                continue;
            }

            if (!childrenMap.TryGetValue(term.ParentSlug, out var children))
            {
                children = new List<string>();
                childrenMap[term.ParentSlug] = children;
            }

            children.Add(term.Slug);
        }

        var result = new Dictionary<string, HierarchyInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in terms.Values)
        {
            var ancestors = BuildAncestors(terms, term.Slug);
            var children = childrenMap.TryGetValue(term.Slug, out var c)
                ? (IReadOnlyList<string>)c
                : Array.Empty<string>();

            result[term.Slug] = new HierarchyInfo
            {
                Children = children,
                Ancestors = ancestors
            };
        }

        return result;
    }

    private static IReadOnlyList<string> BuildAncestors(Dictionary<string, TaxonomyTerm> terms, string slug)
    {
        var ancestors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { slug };
        var current = slug;

        while (terms.TryGetValue(current, out var term) && !string.IsNullOrWhiteSpace(term.ParentSlug))
        {
            var parent = term.ParentSlug;
            if (!seen.Add(parent))
            {
                break;
            }

            if (!terms.ContainsKey(parent))
            {
                break;
            }

            ancestors.Add(parent);
            current = parent;
        }

        ancestors.Reverse();
        return ancestors;
    }
}
