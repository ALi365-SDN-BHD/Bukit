using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
namespace Bukit.Engine;

public sealed record RoutePipelineResult(
    IReadOnlyList<ContentDocument> ContentDocuments,
    IReadOnlyList<RoutedContentDocument> RoutedDocuments,
    IReadOnlyList<RouteInfo> ListRoutes);

public sealed class RoutePipeline
{
    public RoutePipelineResult Execute(AppConfig config, IReadOnlyList<ContentDocument> documents, ThemeTemplateResolver? templateResolver = null)
    {
        var siteLanguages = config.Site.Languages;
        if (siteLanguages is null or { Count: 0 })
        {
            var siteLanguage = config.Site.Language;
            documents = I18nOutputMerger.FilterDocumentsByLanguage(documents, siteLanguage, siteLanguage);
        }
        else
        {
            var defaultLang = I18nOutputMerger.GetDefaultLanguage(config.Site, siteLanguages);
            var currentLang = string.IsNullOrWhiteSpace(config.Site.Language) ? defaultLang : config.Site.Language;
            documents = I18nOutputMerger.FilterDocumentsByLanguage(documents, currentLang, defaultLang);
        }

        var contentDocuments = documents.Where(i => !ContentFieldReader.IsDataItem(i)).ToList();
        var collectionRules = RouteInventoryValidator.BuildCollectionRules(config.Site);
        var routedDocuments = contentDocuments
            .Select(i => new RoutedContentDocument(
                i,
                RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .Select(x => x with { Route = ResolveRouteTemplate(x.Document, x.Route, templateResolver) })
            .ToList();
        RouteInventoryValidator.ValidateContentRoutes(routedDocuments);

        var listRoutes = SeoAlternatesService.BuildListRoutes(config.Site.Collections, templateResolver);
        return new RoutePipelineResult(contentDocuments, routedDocuments, listRoutes);
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
                $"No template was configured for content document '{document.Id}'. Add route.template, site.collections.*.template, or a matching theme.yaml templates entry.",
                DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return route with { Template = templateResolver.ResolveContentTemplate(document, "detail") };
    }
}
