using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.RouteMetadata;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record SeoStageResult(
    SeoPipelineResult SeoResult,
    IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> SeoAlternates,
    int MaxDegreeOfParallelism,
    BreadcrumbDescriptorCatalog? Breadcrumbs = null,
    string? SocialImageFingerprint = null);

internal static class VariantSeoStage
{
    internal static Task<SeoStageResult> ExecuteAsync(
        BuildVariantContext context,
        VariantRenderAssetPlan renderAssetPlan,
        BuildRoutePipelineResult routePipelineResult,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata,
        ILogger logger)
    {
        var routeResult = routePipelineResult.RouteResult;
        var pluginContext = routePipelineResult.PluginContext;
        var seoAlternates = context.SeoAlternates;
        var maxDegreeOfParallelism = Math.Clamp(
            context.Overrides.Jobs ?? Environment.ProcessorCount,
            1,
            Math.Max(1, Environment.ProcessorCount * 2));
        var searchAction = SearchActionDescriptorResolver.Resolve(
            context.Config,
            context.BaseUrl,
            renderAssetPlan.RenderDocuments.Select(document => document.Route)
                .Concat(renderAssetPlan.ListRoutes)
                .Concat(pluginContext.StaticHtmlRoutes));
        var breadcrumbs = BreadcrumbDescriptorResolver.Resolve(
            context.Config,
            context.BaseUrl,
            renderAssetPlan.RenderDocuments,
            routeResult.ListRouteGraph,
            routePipelineResult.StaticEntries,
            routeMetadata,
            logger);
        var seoResult = new SeoPipeline().Execute(
            context.Config,
            context.BaseUrl,
            renderAssetPlan.RenderDocuments,
            renderAssetPlan.ListRoutes,
            seoAlternates,
            logger,
            routeResult.ListRouteGraph,
            routeMetadata,
            searchAction,
            breadcrumbs,
            context.BuildStartedAt,
            context.SharedAssets);
        var socialImages = CreateSocialImageResolver(context, logger);
        var socialImageFingerprint = socialImages.Fingerprint(seoResult.SeoIndex.Models.Values.Select(model => model.Og.Image));
        seoResult = WithSocialImageMetadata(seoResult, socialImages);
        pluginContext.SeoIndex = seoResult.SeoIndex.Entries;
        pluginContext.Set(BuildDataKeys.SeoModels, seoResult.SeoIndex.Models);

        return Task.FromResult(new SeoStageResult(
            seoResult,
            seoAlternates,
            maxDegreeOfParallelism,
            breadcrumbs,
            socialImageFingerprint));
    }

    // Page SEO models gain og:image width, height and type when the image is a local file.
    private static SeoImageMetadataResolver CreateSocialImageResolver(BuildVariantContext context, ILogger logger)
        => new(
            context.Config.Site.Url,
            [context.BaseUrl, context.RootBaseUrl, context.Config.Site.BaseUrl],
            context.Config.Content.Media.UrlBase,
            // The media cache directory the provider actually downloads into (content.media.downloadDir).
            ContentProviderFactory.BuildEffectiveMediaConfig(context.Config.Content.Media, context.RootDir, context.MediaDownloadDir).DownloadDir,
            [context.AssetsDir, context.ParentAssetsDir],
            [context.StaticDir, context.ParentStaticDir],
            logger);

    private static SeoPipelineResult WithSocialImageMetadata(SeoPipelineResult result, SeoImageMetadataResolver resolver)
    {
        if (result.SeoBuilder is null && result.ListSeoBuilder is null)
        {
            return result;
        }

        var seoBuilder = result.SeoBuilder;
        var listSeoBuilder = result.ListSeoBuilder;
        return result with
        {
            SeoBuilder = seoBuilder is null ? null : (document, route) => resolver.Apply(seoBuilder(document, route)),
            ListSeoBuilder = listSeoBuilder is null ? null : (route, page) => resolver.Apply(listSeoBuilder(route, page))
        };
    }
}
