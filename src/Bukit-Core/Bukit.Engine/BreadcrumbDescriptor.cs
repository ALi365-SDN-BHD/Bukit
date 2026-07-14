using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.RouteMetadata;
using Bukit.Routing;

namespace Bukit.Engine;

internal sealed record BreadcrumbItemDescriptor(string Name, string Item);

internal sealed record BreadcrumbDescriptor(IReadOnlyList<BreadcrumbItemDescriptor> Items);

internal sealed class BreadcrumbDescriptorCatalog
{
    private readonly IReadOnlyDictionary<string, BreadcrumbDescriptor> descriptors;

    internal BreadcrumbDescriptorCatalog(IReadOnlyDictionary<string, BreadcrumbDescriptor> descriptors)
    {
        this.descriptors = descriptors;
    }

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
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
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

        var descriptors = new Dictionary<string, BreadcrumbDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, current) in entries)
        {
            if (key == "/")
            {
                continue;
            }

            var items = new List<BreadcrumbItemDescriptor>();
            var segments = key.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var ancestor = string.Empty;
            for (var index = 0; index < segments.Length - 1; index++)
            {
                ancestor += "/" + segments[index];
                if (entries.TryGetValue(ancestor, out var parent))
                {
                    items.Add(ToItem(config, baseUrl, parent));
                }
            }

            items.Add(ToItem(config, baseUrl, current));
            descriptors[key] = new BreadcrumbDescriptor(items);
        }

        return new BreadcrumbDescriptorCatalog(descriptors);
    }

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
