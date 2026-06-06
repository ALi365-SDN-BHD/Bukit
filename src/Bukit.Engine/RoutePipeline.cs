using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
namespace Bukit.Engine;

public sealed record RoutePipelineResult(
    IReadOnlyList<ContentItem> ContentItems,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> Routed,
    IReadOnlyList<RouteInfo> ListRoutes);

public sealed class RoutePipeline
{
    public RoutePipelineResult Execute(AppConfig config, IReadOnlyList<ContentItem> items, ThemeTemplateResolver? templateResolver = null)
    {
        var siteLanguages = config.Site.Languages;
        if (siteLanguages is null or { Count: 0 })
        {
            var siteLanguage = config.Site.Language;
            items = I18nOutputMerger.FilterItemsByLanguage(items, siteLanguage, siteLanguage);
        }
        else
        {
            var defaultLang = I18nOutputMerger.GetDefaultLanguage(config.Site, siteLanguages);
            var currentLang = string.IsNullOrWhiteSpace(config.Site.Language) ? defaultLang : config.Site.Language;
            items = I18nOutputMerger.FilterItemsByLanguage(items, currentLang, defaultLang);
        }

        var contentItems = items.Where(i => !ContentFieldReader.IsDataItem(i)).ToList();
        var collectionRules = RouteInventoryValidator.BuildCollectionRules(config.Site);
        var routed = contentItems
            .Select(i => (Item: i, Route: RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .Select(x => (x.Item, Route: ResolveRouteTemplate(x.Item, x.Route, templateResolver)))
            .ToList();

        RouteInventoryValidator.ValidateContentRoutes(routed);
        var listRoutes = SeoAlternatesService.BuildListRoutes(config.Site.Collections, templateResolver);
        return new RoutePipelineResult(contentItems, routed, listRoutes);
    }

    private static RouteInfo ResolveRouteTemplate(ContentItem item, RouteInfo route, ThemeTemplateResolver? templateResolver)
    {
        if (!string.IsNullOrWhiteSpace(route.Template))
        {
            return route;
        }

        if (templateResolver is null)
        {
            throw new ConfigException(
                $"No template was configured for content item '{item.Id}'. Add route.template, site.collections.*.template, or a matching theme.yaml templates entry.");
        }

        return route with { Template = templateResolver.ResolveContentTemplate(item, "detail") };
    }
}
