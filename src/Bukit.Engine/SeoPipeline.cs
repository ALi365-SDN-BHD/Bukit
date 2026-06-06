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
    Func<RouteInfo, PageInfo, string, string>? ListHtmlPostProcessor,
    Func<ContentDocument, RouteInfo, SeoModel>? DocumentSeoBuilder = null,
    Func<ContentDocument, RouteInfo, PageInfo, string, string>? DocumentHtmlPostProcessor = null);

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
                var skipSeo = ShouldSkipSeo(item.Fields);
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

    private static bool ShouldSkipSeo(IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (fields is null)
        {
            return false;
        }

        return IsTruthy(fields, "seo_skip") ||
               IsTruthy(fields, "skip_seo") ||
               string.Equals(GetFieldText(fields, "seo"), "false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTruthy(IReadOnlyDictionary<string, ContentField> fields, string key)
    {
        var value = GetFieldText(fields, key);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetFieldText(IReadOnlyDictionary<string, ContentField> fields, string key)
    {
        if (!fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        var value = field.Value.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal SeoPipelineResult ExecuteDocuments(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<(ContentDocument Document, RouteInfo Route)> renderQueue,
        IReadOnlyList<RouteInfo> listRoutes,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> seoAlternates,
        AnalyticsModel analytics,
        ILogger logger,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)>? derivedRouted = null)
    {
        var seoIndex = SeoIndexBuilder.BuildDocuments(config, baseUrl, renderQueue, listRoutes, seoAlternates, derivedRouted);
        SeoDiagnostics.AnalyzeIndex(config, seoIndex.Entries, seoIndex.Models, logger);

        var seoHtmlMode = (config.Site.Seo.RenderMode ?? "inject").Trim().ToLowerInvariant();
        var shouldProvideSeoModel = config.Site.Seo.Enabled && seoHtmlMode != "off";
        var shouldInjectSeo = shouldProvideSeoModel && seoHtmlMode == "inject";

        Func<ContentDocument, RouteInfo, SeoModel>? documentSeoBuilder = shouldProvideSeoModel
            ? (_, route) => seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model) ? model : null!
            : null;

        Func<ContentItem, RouteInfo, SeoModel>? seoBuilder = shouldProvideSeoModel
            ? (item, route) => seoIndex.Models.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var model)
                ? model
                : SeoModelBuilder.BuildForContent(
                    config,
                    baseUrl,
                    item,
                    route,
                    SeoPipeline.GetSeoAlternates(seoAlternates, SeoModelBuilder.BuildAlternateKey(item, route)))
            : null;

        Func<ContentItem, RouteInfo, PageInfo, string, string>? htmlPostProcessor = shouldProvideSeoModel
            ? (item, route, page, html) =>
            {
                var skipSeo = ShouldSkipSeo(item.Fields);
                if (shouldInjectSeo && !skipSeo)
                {
                    html = SeoHtmlRenderer.InjectIntoHead(html, page.Seo, analytics);
                }

                return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, logger);
            }
            : null;

        Func<ContentDocument, RouteInfo, PageInfo, string, string>? documentHtmlPostProcessor = shouldProvideSeoModel
            ? (document, route, page, html) =>
            {
                var skipSeo = SeoInjectionPolicy.ShouldSkip(document);
                if (shouldInjectSeo && !skipSeo)
                {
                    html = SeoHtmlRenderer.InjectIntoHead(html, page.Seo, analytics);
                }

                return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, logger);
            }
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
            ShouldProvideSeoModel: shouldProvideSeoModel,
            ShouldInjectSeo: shouldInjectSeo,
            SeoIndex: seoIndex,
            SeoBuilder: seoBuilder,
            HtmlPostProcessor: htmlPostProcessor,
            ListItemSeoBuilder: seoBuilder,
            ListSeoBuilder: listSeoBuilder,
            ListHtmlPostProcessor: listHtmlPostProcessor,
            DocumentSeoBuilder: documentSeoBuilder,
            DocumentHtmlPostProcessor: documentHtmlPostProcessor);
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
