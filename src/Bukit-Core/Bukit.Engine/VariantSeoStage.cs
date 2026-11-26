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
        (seoResult, var socialImageFingerprint) = WithSocialImageMetadata(context, logger, seoResult);
        pluginContext.SeoIndex = seoResult.SeoIndex.Entries;
        pluginContext.Set(BuildDataKeys.SeoModels, seoResult.SeoIndex.Models);

        return Task.FromResult(new SeoStageResult(
            seoResult,
            seoAlternates,
            maxDegreeOfParallelism,
            breadcrumbs,
            socialImageFingerprint));
    }

    // Page SEO models gain og:image width, height and type when the image is a local file. The media cache
    // directory is the one the provider actually downloads into (content.media.downloadDir).
    private static SeoImageMetadataResolver CreateSocialImageResolver(BuildVariantContext context, ILogger logger)
        => new(
            context.Config.Site.Url,
            [context.BaseUrl, context.RootBaseUrl, context.Config.Site.BaseUrl],
            context.Config.Content.Media.UrlBase,
            ContentProviderFactory.BuildEffectiveMediaConfig(context.Config.Content.Media, context.RootDir, context.MediaDownloadDir).DownloadDir,
            [context.AssetsDir, context.ParentAssetsDir],
            [context.StaticDir, context.ParentStaticDir],
            logger);

    // Indexed page images plus site.seo.defaultImage, which pages outside the index can fall back to.
    private static IEnumerable<string?> SocialImageUrls(BuildVariantContext context, SeoPipelineResult result)
    {
        var images = result.SeoIndex.Models.Values.Select(model => model.Og.Image);
        return string.IsNullOrWhiteSpace(context.Config.Site.Seo.DefaultImage)
            ? images
            : images.Append(SeoModelBuilder.BuildMaybeAbsoluteUrl(context.Config.Site.Url, context.BaseUrl, context.Config.Site.Seo.DefaultImage, context.SharedAssets));
    }

    // Page SEO builders gain og:image metadata; the fingerprint covers every image they can emit.
    private static (SeoPipelineResult Result, string? Fingerprint) WithSocialImageMetadata(
        BuildVariantContext context,
        ILogger logger,
        SeoPipelineResult result)
    {
        if (result.SeoBuilder is null && result.ListSeoBuilder is null)
        {
            return (result, null);
        }

        var resolver = CreateSocialImageResolver(context, logger);
        return (WithSocialImageMetadata(result, resolver), resolver.Fingerprint(SocialImageUrls(context, result)));
    }

    private static SeoPipelineResult WithSocialImageMetadata(SeoPipelineResult result, SeoImageMetadataResolver resolver)
    {
        var seoBuilder = result.SeoBuilder;
        var listSeoBuilder = result.ListSeoBuilder;
        return result with
        {
            SeoBuilder = seoBuilder is null ? null : (document, route) => resolver.Apply(seoBuilder(document, route)),
            ListSeoBuilder = listSeoBuilder is null ? null : (route, page) => resolver.Apply(listSeoBuilder(route, page))
        };
    }
}
