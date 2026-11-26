using Bukit.Theme;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Engine.Primitives;
using Bukit.Engine.ContentLinks;
using Bukit.Engine.DerivedFields;
using Bukit.Engine.Queries;
namespace Bukit.Engine;

internal sealed record RoutePipelineResult(
    IReadOnlyList<ContentDocument> ContentDocuments,
    IReadOnlyList<RoutedContentDocument> RoutedDocuments,
    IReadOnlyList<RouteInfo> ListRoutes)
{
    internal ListRouteGraph ListRouteGraph { get; init; } = ListRouteGraph.Empty;

    /// <summary>site.queries results by name, or null when none are configured.</summary>
    internal IReadOnlyDictionary<string, object>? Queries { get; init; }

    /// <summary>Runtime errors of content.derivedFields, for <see cref="DerivedFieldResolver.Report"/>.</summary>
    internal IReadOnlyList<ContentValidationIssue> DerivedFieldIssues { get; init; } = [];
}

internal sealed class RoutePipeline
{
    public RoutePipelineResult Execute(AppConfig config, IReadOnlyList<ContentDocument> documents, ThemeTemplateResolver? templateResolver = null)
    {
        var siteLanguages = I18nOutputMerger.GetLanguages(config.Site);
        if (siteLanguages.Count == 0)
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

        var contentDocuments = documents.Where(i => CollectionPagePolicy.HasPage(i, config.Site)).ToList();
        var collectionRules = RouteInventoryValidator.BuildCollectionRules(config.Site);
        var routedDocuments = contentDocuments
            .Select(i => new RoutedContentDocument(
                i,
                RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .Select(x => x with { Route = ResolveRouteTemplate(x.Document, x.Route, templateResolver) })
            .ToList();
        RouteInventoryValidator.ValidateContentRoutes(routedDocuments);
        // Links need target URLs, so they resolve after routing and before anything reads the pages.
        // Documents without a page (data, or failing routeWhen) are targets without a URL.
        IReadOnlyList<ContentDocument> withoutPage = [.. documents.Where(i => !CollectionPagePolicy.HasPage(i, config.Site))];
        var linkedDocuments = ContentLinkResolver.Apply(config.Content, routedDocuments, withoutPage);
        // Derived values may read the links, and every page consumer below reads the values.
        var derivedFieldIssues = new List<ContentValidationIssue>();
        linkedDocuments = DerivedFieldResolver.Apply(config.Content, linkedDocuments, derivedFieldIssues);
        // Queries see the final page fields, backrefs and derived values included.
        var queries = SiteQueryResolver.Resolve(config, linkedDocuments, withoutPage);

        var listRouteGraph = ListRouteGraphBuilder.Build(
            linkedDocuments,
            config.Site.Collections,
            config.Site.OutputPathEncoding,
            ContentOrdering.Resolve(config),
            templateResolver);
        var listRoutes = listRouteGraph.Routes.Select(route => route.ToRouteInfo()).ToArray();
        return new RoutePipelineResult(contentDocuments, linkedDocuments, listRoutes)
        {
            ListRouteGraph = listRouteGraph,
            Queries = queries,
            DerivedFieldIssues = derivedFieldIssues
        };
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
