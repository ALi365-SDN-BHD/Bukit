using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// Rewrites URL-inferred breadcrumb trails once some pages set parent_url. A page with a usable
/// parent_url gets its parent's trail plus itself. A page without one gets the trail of its nearest
/// generated URL ancestor plus itself when that ancestor's trail was rewritten, so a whole URL
/// subtree follows its root; otherwise it keeps its URL-inferred trail.
/// <para>
/// A parent whose trail contains the page, or leads back to it through URL ancestors, would repeat
/// the page; every page with a parent_url on that cycle warns <c>seo.breadcrumb_parent_cycle</c> and
/// is treated as having no parent_url, and all trails are resolved again, so no trail is kept from a
/// pass that met a cycle. Trails are resolved in ordinal key order, so the result does not depend on
/// page order.
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
        var keys = descriptors.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var parents = new Dictionary<string, BreadcrumbExplicitParent>(explicitParents, StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            // Each pass that meets a cycle drops at least one parent_url, so this ends.
            var resolver = new Resolver(items, descriptors, parents);
            var trails = keys.Select(resolver.Resolve).ToArray();
            if (resolver.Cycle is not { } cycle)
            {
                for (var index = 0; index < keys.Length; index++)
                {
                    descriptors[keys[index]] = trails[index]!.Trail;
                }

                return;
            }

            foreach (var key in cycle)
            {
                WarnCycle(logger, parents[key]);
                parents.Remove(key);
            }
        }
    }

    internal static void WarnCycle(ILogger? logger, BreadcrumbExplicitParent parent)
        => logger?.Warn($"seo.breadcrumb_parent_cycle route={parent.RouteUrl} parent={parent.Value} hint=remove-one-parent_url-in-the-cycle");

    private sealed record Resolved(BreadcrumbDescriptor Trail, bool Rewritten);

    /// <summary>
    /// One resolution pass. After the first cycle it records the pages whose parent_url must be
    /// dropped in <see cref="Cycle"/> and returns null for every trail, so a caller never keeps a
    /// trail computed around a cycle.
    /// </summary>
    private sealed class Resolver(
        IReadOnlyDictionary<string, BreadcrumbItemDescriptor> items,
        IReadOnlyDictionary<string, BreadcrumbDescriptor> inferred,
        IReadOnlyDictionary<string, BreadcrumbExplicitParent> explicitParents)
    {
        private readonly Dictionary<string, Resolved> _resolved = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _visiting = [];

        /// <summary>The pages whose parent_url closes the first cycle met, in ordinal order.</summary>
        internal IReadOnlyList<string>? Cycle { get; private set; }

        internal Resolved? Resolve(string key)
        {
            if (Cycle is not null)
            {
                return null;
            }

            if (_resolved.TryGetValue(key, out var done))
            {
                return done;
            }

            if (key == "/")
            {
                // The home page has no BreadcrumbList; as a parent it contributes itself.
                return _resolved[key] = new Resolved(new BreadcrumbDescriptor([items[key]]), Rewritten: false);
            }

            var position = _visiting.FindIndex(visiting => string.Equals(visiting, key, StringComparison.OrdinalIgnoreCase));
            if (position >= 0)
            {
                // URL ancestors are strictly shorter, so a cycle always passes through a parent_url.
                Cycle = [.. _visiting.Skip(position).Where(explicitParents.ContainsKey).Order(StringComparer.OrdinalIgnoreCase)];
                return null;
            }

            _visiting.Add(key);
            var result = explicitParents.TryGetValue(key, out var parent) ? FromParent(key, parent) : FromUrlAncestor(key);
            _visiting.RemoveAt(_visiting.Count - 1);
            return result is null ? null : _resolved[key] = result;
        }

        private Resolved? FromParent(string key, BreadcrumbExplicitParent parent)
        {
            var parentTrail = Resolve(parent.Key);
            if (parentTrail is null)
            {
                return null;
            }

            if (Contains(parentTrail.Trail, items[key]))
            {
                // The parent's own trail (for example a taxonomy parent) already holds this page.
                Cycle = [key];
                return null;
            }

            return new Resolved(Append(parentTrail.Trail, items[key]), Rewritten: true);
        }

        private Resolved? FromUrlAncestor(string key)
        {
            var ancestor = NearestUrlAncestor(key);
            if (ancestor is null)
            {
                return new Resolved(inferred[key], Rewritten: false);
            }

            var ancestorTrail = Resolve(ancestor);
            if (ancestorTrail is null)
            {
                return null;
            }

            return ancestorTrail.Rewritten && !Contains(ancestorTrail.Trail, items[key])
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
