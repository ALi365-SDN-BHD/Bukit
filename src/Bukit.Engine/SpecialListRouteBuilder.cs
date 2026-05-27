using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine;

internal sealed record SpecialListDefinition(
    RouteInfo Route,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> Items,
    bool IncludeContent);

public static class SpecialListRouteBuilder
{
    internal static List<SpecialListDefinition> Build(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        string layoutsDir,
        string listPageContentMode,
        string outputPathEncoding)
    {
        var index = CollectionRouteIndex.Create(routed);
        var list = new List<SpecialListDefinition>();
        var homeRoute = new RouteInfo("/", "index.html", "pages/index.html");
        list.Add(new SpecialListDefinition(
            homeRoute,
            index.AllOrdered,
            TemplateCapabilitiesResolver.ShouldIncludeListPageContent(homeRoute.Template, layoutsDir, listPageContentMode)));

        if (collections is null || collections.Count == 0)
        {
            AddLegacyList(list, index, "/blog/", outputPathEncoding, layoutsDir, listPageContentMode);
            AddLegacyList(list, index, "/pages/", outputPathEncoding, layoutsDir, listPageContentMode);
            return list;
        }

        foreach (var (key, collection) in collections)
        {
            if (string.IsNullOrWhiteSpace(collection.ListRoute))
            {
                continue;
            }

            var url = RoutePathBuilder.NormalizeListRoute(collection.ListRoute);
            var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);
            var template = string.IsNullOrWhiteSpace(collection.ListTemplate) ? "pages/list.html" : collection.ListTemplate.Trim();
            var route = new RouteInfo(url, outputPath, template);
            var items = index.GetByCollection(key);
            list.Add(new SpecialListDefinition(
                route,
                items,
                TemplateCapabilitiesResolver.ShouldIncludeListPageContent(route.Template, layoutsDir, listPageContentMode)));

            if (collection.FilteredLists is { Count: > 0 })
            {
                foreach (var filter in collection.FilteredLists)
                {
                    var filtered = items
                        .Where(x => TryMatchFieldValue(x.Item.Fields, filter.Field, filter.Value))
                        .ToList();

                    var filterUrl = RoutePathBuilder.NormalizeListRoute(filter.ListRoute);
                    var filterOutputPath = RoutePathBuilder.BuildOutputPathFromUrl(filterUrl, outputPathEncoding);
                    var filterTemplate = string.IsNullOrWhiteSpace(filter.ListTemplate) ? template : filter.ListTemplate.Trim();
                    var filterRoute = new RouteInfo(filterUrl, filterOutputPath, filterTemplate);
                    list.Add(new SpecialListDefinition(
                        filterRoute,
                        filtered,
                        TemplateCapabilitiesResolver.ShouldIncludeListPageContent(filterRoute.Template, layoutsDir, listPageContentMode)));
                }
            }
        }

        return list;
    }

    internal static bool TryMatchFieldValue(IReadOnlyDictionary<string, ContentField>? fields, string field, string expectedValue)
    {
        if (fields is null || !fields.TryGetValue(field, out var cf) || cf.Value is null)
        {
            return false;
        }

        return string.Equals(cf.Value.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddLegacyList(
        List<SpecialListDefinition> list,
        CollectionRouteIndex index,
        string url,
        string outputPathEncoding,
        string layoutsDir,
        string listPageContentMode)
    {
        var route = new RouteInfo(url, RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding), "pages/list.html");
        var items = index.GetByRoutePrefix(url);
        list.Add(new SpecialListDefinition(
            route,
            items,
            TemplateCapabilitiesResolver.ShouldIncludeListPageContent(route.Template, layoutsDir, listPageContentMode)));
    }
}
