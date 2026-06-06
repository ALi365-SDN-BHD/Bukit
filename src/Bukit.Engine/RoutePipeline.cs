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

public sealed record ContentDocumentRoutePipelineResult(
    IReadOnlyList<ContentDocument> ContentDocuments,
    IReadOnlyList<(ContentDocument Document, RouteInfo Route)> RoutedDocuments,
    IReadOnlyList<RouteInfo> ListRoutes);

public sealed class RoutePipeline
{
    public RoutePipelineResult Execute(AppConfig config, IReadOnlyList<ContentItem> items, ThemeTemplateResolver? templateResolver = null)
    {
        var contentItems = items.Where(i => !MetaHelpers.IsDataItem(i)).ToList();
        var collectionRules = RouteInventoryValidator.BuildCollectionRules(config.Site);
        var routed = contentItems
            .Select(i => (Item: i, Route: RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .Select(x => (x.Item, Route: ResolveRouteTemplate(x.Item, x.Route, templateResolver)))
            .ToList();

        RouteInventoryValidator.ValidateContentRoutes(routed);
        var listRoutes = SeoAlternatesService.BuildListRoutes(config.Site.Collections, templateResolver);
        return new RoutePipelineResult(contentItems, routed, listRoutes);
    }

    public ContentDocumentRoutePipelineResult ExecuteDocuments(
        AppConfig config,
        IReadOnlyList<ContentDocument> documents,
        ThemeTemplateResolver? templateResolver = null)
    {
        var contentDocuments = documents.Where(i => !i.Publish.IsDataModule).ToList();
        var collectionRules = RouteInventoryValidator.BuildCollectionRules(config.Site);
        var routed = contentDocuments
            .Select(i => (Document: i, Route: RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .Select(x => (x.Document, Route: ResolveRouteTemplate(x.Document, x.Route, templateResolver)))
            .ToList();

        RouteInventoryValidator.ValidateContentDocumentRoutes(routed);
        var listRoutes = SeoAlternatesService.BuildListRoutes(config.Site.Collections, templateResolver);
        return new ContentDocumentRoutePipelineResult(contentDocuments, routed, listRoutes);
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

    private static RouteInfo ResolveRouteTemplate(ContentDocument document, RouteInfo route, ThemeTemplateResolver? templateResolver)
    {
        if (!string.IsNullOrWhiteSpace(route.Template))
        {
            return route;
        }

        if (templateResolver is null)
        {
            throw new ConfigException(
                $"No template was configured for content document '{document.Record.Identity.Id}'. Add route.template, site.collections.*.template, or a matching theme.yaml templates entry.");
        }

        return route with { Template = templateResolver.ResolveContentTemplate(document, "detail") };
    }
}
