using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Engine.RouteMetadata;

namespace Bukit.Engine;

internal sealed record SeoPipelineResult(
    bool ShouldProvideSeoModel,
    bool ShouldInjectSeo,
    SeoIndexBuildResult SeoIndex,
    Func<ContentDocument, RouteInfo, SeoModel>? SeoBuilder,
    IHtmlTransform? HtmlTransform,
    Func<ContentDocument, RouteInfo, SeoModel>? ListItemSeoBuilder,
    Func<RouteInfo, PageInfo, SeoModel>? ListSeoBuilder);

internal sealed class SeoPipeline
{
    internal SeoPipelineResult Execute(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<RoutedContentDocument> renderQueue,
        IReadOnlyList<RouteInfo> listRoutes,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> seoAlternates,
        ILogger logger,
        ListRouteGraph? listRouteGraph = null,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata = null,
        SearchActionDescriptor? searchAction = null,
        BreadcrumbDescriptorCatalog? breadcrumbs = null,
        DateTimeOffset? now = null)
    {
        var seoIndex = SeoIndexBuilder.Build(config, baseUrl, renderQueue, listRoutes, seoAlternates, listRouteGraph, routeMetadata, searchAction, breadcrumbs, now);
        SeoDiagnostics.AnalyzeIndex(config, seoIndex.Entries, seoIndex.Models, logger);
        AnalyzeLlmsCuration(config, renderQueue, logger);

        var seoHtmlMode = (config.Site.Seo.RenderMode ?? "inject").Trim().ToLowerInvariant();
        var shouldProvideSeoModel = config.Site.Seo.Enabled;
        var shouldInjectSeo = shouldProvideSeoModel && seoHtmlMode == "inject";

        Func<ContentDocument, RouteInfo, SeoModel>? seoBuilder = shouldProvideSeoModel
            ? (_, route) => seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model) ? model : null!
        : null;

        IHtmlTransform? htmlTransform = shouldProvideSeoModel
            ? new SeoHtmlTransform(config, shouldInjectSeo, logger)
            : null;

        Func<ContentDocument, RouteInfo, SeoModel>? listItemSeoBuilder = shouldProvideSeoModel
            ? (document, route) => SeoModelBuilder.BuildForContent(
                config,
                baseUrl,
                document,
                route,
                SeoPipeline.GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildAlternateKey(document, route)),
                searchAction: searchAction,
                breadcrumb: breadcrumbs?.Find(route.Url))
            : null;

        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder = shouldProvideSeoModel
            ? (route, page) =>
            {
                if (seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model))
                {
                    return model;
                }

                var graphRoute = FindGraphRoute(listRouteGraph, route);
                if (graphRoute is not null)
                {
                    return SeoModelBuilder.BuildForList(
                        config,
                        baseUrl,
                        page,
                        graphRoute,
                        SeoPipeline.GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildListAlternateKey(graphRoute.ToRouteInfo())),
                        searchAction,
                        breadcrumbs?.Find(route.Url));
                }

                return SeoModelBuilder.BuildForList(
                    config,
                    baseUrl,
                    page,
                    SeoPipeline.GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildListAlternateKey(route)),
                    searchAction,
                    breadcrumbs?.Find(route.Url));
            }
        : null;

        return new SeoPipelineResult(
            shouldProvideSeoModel,
            shouldInjectSeo,
            seoIndex,
            seoBuilder,
            htmlTransform,
            listItemSeoBuilder,
            listSeoBuilder);
    }

    internal static IReadOnlyList<SeoAlternateModel>? GetSeoAlternates(
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> alternates,
        string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return alternates.TryGetValue(key, out var list) ? list : null;
    }

    // Invalid page-level geo.llms curation removes the page from llms output, so report it
    // here, where every other build-time SEO diagnostic runs (warn by default, fail under strict).
    private static void AnalyzeLlmsCuration(
        AppConfig config,
        IReadOnlyList<RoutedContentDocument> documents,
        ILogger logger)
    {
        var geo = config.Site.Seo.Geo;
        if (!geo.Enabled || (!geo.LlmsTxt && !geo.LlmsFullTxt))
        {
            return;
        }

        var documentsByPath = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in documents)
        {
            var key = BuildPathUtils.NormalizeRelPath(routedDocument.Route.OutputPath);
            if (key is not null)
            {
                documentsByPath[key] = routedDocument.Document;
            }
        }

        SeoDiagnostics.AnalyzeLlmsCuration(config, documentsByPath, logger);
    }

    private static ListRoutePlan? FindGraphRoute(ListRouteGraph? graph, RouteInfo route)
    {
        if (graph is null || graph.Routes.Count == 0)
        {
            return null;
        }

        var outputPath = BuildPathUtils.NormalizeRelPath(route.OutputPath);
        return graph.Routes.FirstOrDefault(candidate =>
            string.Equals(BuildPathUtils.NormalizeRelPath(candidate.OutputPath), outputPath, StringComparison.OrdinalIgnoreCase)) ??
            graph.Routes.FirstOrDefault(candidate =>
                string.Equals(candidate.Url, route.Url, StringComparison.OrdinalIgnoreCase));
    }
}
