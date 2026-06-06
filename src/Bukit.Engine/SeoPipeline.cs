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
    Func<ContentItem, RouteInfo, SeoModel>? SeoBuilder,
    Func<ContentItem, RouteInfo, PageInfo, string, string>? HtmlPostProcessor,
    Func<ContentItem, RouteInfo, SeoModel>? ListItemSeoBuilder,
    Func<RouteInfo, PageInfo, SeoModel>? ListSeoBuilder,
    Func<RouteInfo, PageInfo, string, string>? ListHtmlPostProcessor);

internal sealed class SeoPipeline
{
    internal SeoPipelineResult Execute(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> renderQueue,
        IReadOnlyList<RouteInfo> listRoutes,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> seoAlternates,
        AnalyticsModel analytics,
        ILogger logger)
    {
        var seoIndex = SeoIndexBuilder.Build(config, baseUrl, renderQueue, listRoutes, seoAlternates);
        SeoDiagnostics.AnalyzeIndex(config, seoIndex.Entries, seoIndex.Models, logger);

        var seoHtmlMode = (config.Site.Seo.RenderMode ?? "inject").Trim().ToLowerInvariant();
        var shouldProvideSeoModel = config.Site.Seo.Enabled && seoHtmlMode != "off";
        var shouldInjectSeo = shouldProvideSeoModel && seoHtmlMode == "inject";

        Func<ContentItem, RouteInfo, SeoModel>? seoBuilder = shouldProvideSeoModel
            ? (_, route) => seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model) ? model : null!
            : null;

        Func<ContentItem, RouteInfo, PageInfo, string, string>? htmlPostProcessor = shouldProvideSeoModel
            ? (item, route, page, html) =>
            {
                var skipSeo = SeoInjectionPolicy.ShouldSkip(item.Fields);
                if (shouldInjectSeo && !skipSeo)
                {
                    html = SeoHtmlRenderer.InjectIntoHead(html, page.Seo, analytics);
                }

                return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, logger);
            }
        : null;

        Func<ContentItem, RouteInfo, SeoModel>? listItemSeoBuilder = shouldProvideSeoModel
            ? (item, route) => SeoModelBuilder.BuildForContent(
                config,
                baseUrl,
                item,
                route,
                SeoPipeline.GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildAlternateKey(item, route)))
            : null;

        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder = shouldProvideSeoModel
            ? (route, page) => seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model)
                ? model
                : SeoModelBuilder.BuildForList(
                    config,
                    baseUrl,
                    page,
                    SeoPipeline.GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildListAlternateKey(route)))
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
}
