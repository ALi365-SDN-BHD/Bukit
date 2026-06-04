using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
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
        string outputPathEncoding,
        ThemeTemplateResolver? templateResolver = null)
    {
        var index = CollectionRouteIndex.Create(routed);
        var list = new List<SpecialListDefinition>();
        var homeRoute = new RouteInfo("/", "index.html", templateResolver?.ResolveHomeTemplate() ?? "index.html");
        list.Add(new SpecialListDefinition(
            homeRoute,
            index.AllOrdered,
            TemplateCapabilitiesResolver.ShouldIncludeListPageContent(homeRoute.Template, layoutsDir, listPageContentMode)));

        if (collections is null || collections.Count == 0)
        {
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
            var template = ResolveListTemplate(collection.ListTemplate, templateResolver);
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

    private static string ResolveListTemplate(string? explicitTemplate, ThemeTemplateResolver? templateResolver)
    {
        if (!string.IsNullOrWhiteSpace(explicitTemplate))
        {
            return explicitTemplate.Trim();
        }

        if (templateResolver is null)
        {
            throw new ConfigException("No list template was configured. Add site.collections.*.listTemplate or a matching theme.yaml templates entry.");
        }

        return templateResolver.ResolveKindTemplate("list");
    }
}
