using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.RouteMetadata;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record BreadcrumbItemDescriptor(string Name, string Item);

internal sealed record BreadcrumbDescriptor(IReadOnlyList<BreadcrumbItemDescriptor> Items);

internal sealed class BreadcrumbDescriptorCatalog
{
    private readonly IReadOnlyDictionary<string, BreadcrumbDescriptor> descriptors;

    internal BreadcrumbDescriptorCatalog(IReadOnlyDictionary<string, BreadcrumbDescriptor> descriptors, bool hasExplicitParents = false)
    {
        this.descriptors = descriptors;
        HasExplicitParents = hasExplicitParents;
    }

    /// <summary>True when some page sets parent_url, so its trail depends on other pages' titles.</summary>
    internal bool HasExplicitParents { get; }

    internal BreadcrumbDescriptor? Find(string routeUrl)
        => descriptors.TryGetValue(BreadcrumbDescriptorResolver.NormalizeForComparison(routeUrl), out var descriptor)
            ? descriptor
            : null;
}

internal static class BreadcrumbDescriptorResolver
{
    internal static BreadcrumbDescriptorCatalog Resolve(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<RoutedContentDocument> routed,
        ListRouteGraph? listRouteGraph,
        IReadOnlyList<RenderEntry>? staticEntries,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata,
        ILogger? logger = null)
    {
        var entries = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in routed)
        {
            Add(entries, routedDocument.Route.Url, routedDocument.Document.Title, priority: 1);
            var metadata = RouteMetadataApplicator.FindForContent(
                routedDocument.Document,
                routedDocument.Route.Url,
                routeMetadata);
            if (metadata is not null)
            {
                Add(entries, routedDocument.Route.Url, metadata.Title, priority: 3);
            }
        }

        if (staticEntries is not null)
        {
            foreach (var entry in staticEntries.Where(entry => entry.Kind == RenderEntryKind.Static))
            {
                Add(entries, entry.Route.Url, entry.Title, priority: 1);
                var metadata = RouteMetadataApplicator.Find(entry.Route.Url, routeMetadata);
                if (metadata is not null)
                {
                    Add(entries, entry.Route.Url, metadata.Title, priority: 3);
                }
            }
        }

        if (listRouteGraph is not null)
        {
            foreach (var route in listRouteGraph.Routes)
            {
                var key = NormalizeForComparison(route.Url);
                var pagination = ListPageMetadataBuilder.BuildPagination(route);
                var title = string.IsNullOrWhiteSpace(route.Title) &&
                            route.Kind is ListRouteKind.TaxonomyIndex or ListRouteKind.TaxonomyTermPage &&
                            entries.TryGetValue(key, out var derived)
                    ? derived.Title
                    : ListPageMetadataBuilder.BuildTitle(config.Site, route, pagination);
                Add(
                    entries,
                    route.Url,
                    title,
                    priority: route.RouteMetadataApplied ? 3 : 2);
            }
        }

        var schema = config.Site.Seo.Schema;
        var parents = BreadcrumbTaxonomyParentResolver.Resolve(config, routed, listRouteGraph);
        var home = schema.BreadcrumbHome ? ResolveHome(config, entries) : null;
        var descriptors = new Dictionary<string, BreadcrumbDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, current) in entries)
        {
            if (key == "/")
            {
                continue;
            }

            var items = new List<BreadcrumbItemDescriptor>();
            if (home is not null)
            {
                items.Add(ToItem(config, baseUrl, home));
            }

            if (parents.TryGetValue(key, out var parentKey) && entries.TryGetValue(parentKey, out var taxonomyParent))
            {
                items.Add(ToItem(config, baseUrl, taxonomyParent));
            }

            var segments = key.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var ancestor = string.Empty;
            for (var index = 0; index < segments.Length - 1; index++)
            {
                ancestor += "/" + segments[index];
                if (!string.Equals(ancestor, parentKey, StringComparison.OrdinalIgnoreCase) &&
                    entries.TryGetValue(ancestor, out var parent))
                {
                    items.Add(ToItem(config, baseUrl, parent));
                }
            }

            items.Add(ToItem(config, baseUrl, current));
            descriptors[key] = new BreadcrumbDescriptor(items);
        }

        var explicitParents = BreadcrumbExplicitParentResolver.Resolve(routed, entries.ContainsKey, logger);
        if (explicitParents.Count > 0)
        {
            ApplyExplicitParents(config, baseUrl, entries, descriptors, explicitParents);
        }

        return new BreadcrumbDescriptorCatalog(descriptors, explicitParents.Count > 0);
    }

    /// <summary>
    /// Replaces the URL-inferred trail of each page with a parent_url by its parent's trail plus the page.
    /// Parents are acyclic and exist, so the recursion ends; each trail is built once.
    /// </summary>
    private static void ApplyExplicitParents(
        AppConfig config,
        string baseUrl,
        IReadOnlyDictionary<string, CatalogEntry> entries,
        Dictionary<string, BreadcrumbDescriptor> descriptors,
        IReadOnlyDictionary<string, string> explicitParents)
    {
        var resolved = new Dictionary<string, BreadcrumbDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in explicitParents.Keys)
        {
            descriptors[key] = Trail(key);
        }

        BreadcrumbDescriptor Trail(string key)
        {
            if (resolved.TryGetValue(key, out var done))
            {
                return done;
            }

            BreadcrumbDescriptor descriptor;
            if (!explicitParents.TryGetValue(key, out var parentKey))
            {
                descriptor = key == "/"
                    ? new BreadcrumbDescriptor([ToItem(config, baseUrl, entries["/"])])
                    : descriptors[key];
            }
            else
            {
                descriptor = new BreadcrumbDescriptor([.. Trail(parentKey).Items, ToItem(config, baseUrl, entries[key])]);
            }

            resolved[key] = descriptor;
            return descriptor;
        }
    }

    private static CatalogEntry ResolveHome(AppConfig config, IReadOnlyDictionary<string, CatalogEntry> entries)
        => entries.TryGetValue("/", out var home)
            ? home
            : new CatalogEntry("/", string.IsNullOrWhiteSpace(config.Site.Title) ? "Home" : config.Site.Title.Trim(), 0);

    internal static string NormalizeForComparison(string routeUrl)
    {
        var normalized = RoutePathBuilder.NormalizeUrl(routeUrl);
        return normalized == "/" ? normalized : normalized.TrimEnd('/');
    }

    private static BreadcrumbItemDescriptor ToItem(AppConfig config, string baseUrl, CatalogEntry entry)
        => new(
            entry.Title,
            SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, entry.Url));

    private static void Add(
        IDictionary<string, CatalogEntry> entries,
        string routeUrl,
        string? title,
        int priority)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var url = RoutePathBuilder.NormalizeUrl(routeUrl);
        var key = NormalizeForComparison(url);
        if (!entries.TryGetValue(key, out var existing) || priority > existing.Priority)
        {
            entries[key] = new CatalogEntry(url, title.Trim(), priority);
        }
    }

    private sealed record CatalogEntry(string Url, string Title, int Priority);
}
