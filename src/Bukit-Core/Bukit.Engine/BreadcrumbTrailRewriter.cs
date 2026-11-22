using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// Rewrites URL-inferred breadcrumb trails once some pages set parent_url. A page with a usable
/// parent_url gets its parent's trail plus itself. A page without one gets the trail of its nearest
/// generated URL ancestor plus itself when that ancestor's trail was rewritten, so a whole URL
/// subtree follows its root; otherwise it keeps its URL-inferred trail.
/// <para>
/// A parent whose trail contains the page, or leads back to it through URL ancestors, would repeat
/// the page; that page warns <c>seo.breadcrumb_parent_cycle</c> and is treated as having no parent_url.
/// Each trail is resolved once, in ordinal key order, so the result does not depend on page order.
/// </para>
/// </summary>
internal static class BreadcrumbTrailRewriter
{
    internal static void Apply(
        IReadOnlyDictionary<string, BreadcrumbItemDescriptor> items,
        Dictionary<string, BreadcrumbDescriptor> descriptors,
        IReadOnlyDictionary<string, BreadcrumbExplicitParent> explicitParents,
        ILogger? logger)
    {
        var resolver = new Resolver(items, descriptors, explicitParents, logger);
        foreach (var key in descriptors.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray())
        {
            descriptors[key] = resolver.Resolve(key)!.Trail;
        }
    }

    internal static void WarnCycle(ILogger? logger, BreadcrumbExplicitParent parent)
        => logger?.Warn($"seo.breadcrumb_parent_cycle route={parent.RouteUrl} parent={parent.Value} hint=remove-one-parent_url-in-the-cycle");

    private sealed record Resolved(BreadcrumbDescriptor Trail, bool Rewritten);

    private sealed class Resolver(
        IReadOnlyDictionary<string, BreadcrumbItemDescriptor> items,
        IReadOnlyDictionary<string, BreadcrumbDescriptor> inferred,
        IReadOnlyDictionary<string, BreadcrumbExplicitParent> explicitParents,
        ILogger? logger)
    {
        private readonly Dictionary<string, Resolved> _resolved = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _visiting = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns the page's trail, or null while that page is still being resolved (a cycle).</summary>
        internal Resolved? Resolve(string key)
        {
            if (_resolved.TryGetValue(key, out var done))
            {
                return done;
            }

            if (key == "/")
            {
                // The home page has no BreadcrumbList; as a parent it contributes itself.
                return _resolved[key] = new Resolved(new BreadcrumbDescriptor([items[key]]), Rewritten: false);
            }

            if (!_visiting.Add(key))
            {
                return null;
            }

            var result = (explicitParents.TryGetValue(key, out var parent) ? FromParent(key, parent) : null)
                ?? FromUrlAncestor(key);
            _visiting.Remove(key);
            return _resolved[key] = result;
        }

        private Resolved? FromParent(string key, BreadcrumbExplicitParent parent)
        {
            var parentTrail = Resolve(parent.Key);
            if (parentTrail is null || Contains(parentTrail.Trail, items[key]))
            {
                WarnCycle(logger, parent);
                return null;
            }

            return new Resolved(Append(parentTrail.Trail, items[key]), Rewritten: true);
        }

        private Resolved FromUrlAncestor(string key)
        {
            var ancestor = NearestUrlAncestor(key);
            var ancestorTrail = ancestor is null ? null : Resolve(ancestor);
            return ancestorTrail is { Rewritten: true } && !Contains(ancestorTrail.Trail, items[key])
                ? new Resolved(Append(ancestorTrail.Trail, items[key]), Rewritten: true)
                : new Resolved(inferred[key], Rewritten: false);
        }

        // The nearest strict URL ancestor that is a generated page; the home page never rewrites.
        private string? NearestUrlAncestor(string key)
        {
            for (var slash = key.LastIndexOf('/'); slash > 0; slash = key.LastIndexOf('/', slash - 1))
            {
                var ancestor = key[..slash];
                if (items.ContainsKey(ancestor))
                {
                    return ancestor;
                }
            }

            return null;
        }

        private static bool Contains(BreadcrumbDescriptor trail, BreadcrumbItemDescriptor item)
            => trail.Items.Any(existing => string.Equals(existing.Item, item.Item, StringComparison.OrdinalIgnoreCase));

        private static BreadcrumbDescriptor Append(BreadcrumbDescriptor trail, BreadcrumbItemDescriptor item)
            => new([.. trail.Items, item]);
    }
}
