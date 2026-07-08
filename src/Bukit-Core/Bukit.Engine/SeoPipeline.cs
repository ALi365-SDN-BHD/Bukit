using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record SeoPipelineResult(
    bool ShouldProvideSeoModel,
    bool ShouldInjectSeo,
    SeoIndexBuildResult SeoIndex,
    Func<ContentDocument, RouteInfo, SeoModel>? SeoBuilder,
    Func<ContentDocument, RouteInfo, PageInfo, string, string>? HtmlPostProcessor,
    Func<ContentDocument, RouteInfo, SeoModel>? ListItemSeoBuilder,
    Func<RouteInfo, PageInfo, SeoModel>? ListSeoBuilder,
    Func<RouteInfo, PageInfo, string, string>? ListHtmlPostProcessor);

internal sealed class SeoPipeline
{
    internal SeoPipelineResult Execute(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<RoutedContentDocument> renderQueue,
        IReadOnlyList<RouteInfo> listRoutes,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> seoAlternates,
        AnalyticsModel analytics,
        ILogger logger,
        ListRouteGraph? listRouteGraph = null)
    {
        var seoIndex = SeoIndexBuilder.Build(config, baseUrl, renderQueue, listRoutes, seoAlternates, listRouteGraph);
        SeoDiagnostics.AnalyzeIndex(config, seoIndex.Entries, seoIndex.Models, logger);

        var seoHtmlMode = (config.Site.Seo.RenderMode ?? "inject").Trim().ToLowerInvariant();
        var shouldProvideSeoModel = config.Site.Seo.Enabled && seoHtmlMode != "off";
        var shouldInjectSeo = shouldProvideSeoModel && seoHtmlMode == "inject";

        Func<ContentDocument, RouteInfo, SeoModel>? seoBuilder = shouldProvideSeoModel
            ? (_, route) => seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model) ? model : null!
        : null;

        Func<ContentDocument, RouteInfo, PageInfo, string, string>? htmlPostProcessor = shouldProvideSeoModel
            ? (document, route, page, html) =>
            {
                var skipSeo = SeoInjectionPolicy.ShouldSkip(document.CustomFields);
                if (shouldInjectSeo && !skipSeo)
                {
                    html = SeoHtmlRenderer.InjectIntoHead(html, page.Seo, analytics);
                }

                return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, logger);
            }
        : null;

        Func<ContentDocument, RouteInfo, SeoModel>? listItemSeoBuilder = shouldProvideSeoModel
            ? (document, route) => SeoModelBuilder.BuildForContent(
                config,
                baseUrl,
                document,
                route,
                SeoPipeline.GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildAlternateKey(document, route)))
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
                        SeoPipeline.GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildListAlternateKey(graphRoute.ToRouteInfo())));
                }

                return SeoModelBuilder.BuildForList(
                    config,
                    baseUrl,
                    page,
                    SeoPipeline.GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildListAlternateKey(route)));
            }
        : null;

        Func<RouteInfo, PageInfo, string, string>? listHtmlPostProcessor = shouldProvideSeoModel
            ? (route, page, html) =>
            {
                if (shouldInjectSeo)
                {
                    html = SeoHtmlRenderer.InjectIntoHead(html, page.Seo, analytics);
                }

                return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, logger);
            }
        : null;

        return new SeoPipelineResult(
            shouldProvideSeoModel,
            shouldInjectSeo,
            seoIndex,
            seoBuilder,
            htmlPostProcessor,
            listItemSeoBuilder,
            listSeoBuilder,
            listHtmlPostProcessor);
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
