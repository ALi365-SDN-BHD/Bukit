using Bukit.Theme;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Engine.RouteMetadata;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class VariantRenderStage
{
    internal static async Task<RenderPipelineResult> ExecuteAsync(
        BuildVariantContext context,
        BuildRoutePipelineResult routePipelineResult,
        VariantRenderAssetPlan renderAssetPlan,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        ManifestSetupResult manifestSetup,
        SeoStageResult seoStage,
        HtmlTransformPipeline htmlTransformPipeline,
        ThemeTemplateResolver templateResolver,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata,
        BuildStageMetricsCollector metrics,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var routeResult = routePipelineResult.RouteResult;
        var renderDependencyHashStopwatch = Stopwatch.StartNew();
        var imageSourceFingerprint = manifestSetup.IncrementalEnabled
            ? ImageSourceFingerprint.Compute(
                context.Config,
                context.RootDir,
                context.MediaDownloadDir,
                context.AssetsDir,
                context.ParentAssetsDir,
                context.StaticDir,
                context.ParentStaticDir,
                logger)
            : null;
        var renderDependencyHash = manifestSetup.IncrementalEnabled
            ? RenderDependencyHasher.Compute(
                context.Config,
                siteModel,
                context.Overrides.ExecutionMode,
                seoAlternates: seoStage.SeoAlternates,
                imageSourceFingerprint: imageSourceFingerprint)
            : string.Empty;
        renderDependencyHashStopwatch.Stop();
        metrics.AddDuration(
            "renderDependencyHash",
            renderDependencyHashStopwatch.ElapsedMilliseconds);
        var renderDependencyHashResolver = BuildRenderDependencyHashResolver(
            renderAssetPlan.RenderDocuments,
            routeResult.ListRouteGraph,
            renderDependencyHash,
            routeMetadata);
        if (manifestSetup.IncrementalEnabled)
        {
            renderDependencyHashResolver = WithBreadcrumbDependencies(
                context.Config.Site.Seo.Schema,
                seoStage.Breadcrumbs,
                renderDependencyHash,
                renderDependencyHashResolver);
        }

        var result = await new RenderPipeline().ExecuteAsync(new RenderPipelineContext(
            BodyStore: context.BodyStore,
            Renderer: renderer,
            SiteModel: siteModel,
            Collections: context.Config.Site.Collections,
            LayoutsDir: context.LayoutsDir,
            ListPageContentMode: context.Config.Build.ListPageContentMode,
            OutputPathEncoding: context.Config.Site.OutputPathEncoding,
            OutputDir: context.OutputDir,
            TemplateHash: manifestSetup.TemplateHash,
            RenderDependencyHash: renderDependencyHash,
            IncrementalEnabled: manifestSetup.IncrementalEnabled,
            Manifest: manifestSetup.Manifest,
            ManifestEntries: manifestSetup.ManifestEntries,
            MaxDegreeOfParallelism: seoStage.MaxDegreeOfParallelism,
            Logger: logger,
            ListRouteGraph: routeResult.ListRouteGraph,
            StaticEntries: routePipelineResult.StaticEntries,
            SeoBuilder: seoStage.SeoResult.SeoBuilder,
            ListItemSeoBuilder: seoStage.SeoResult.ListItemSeoBuilder,
            ListSeoBuilder: seoStage.SeoResult.ListSeoBuilder,
            HtmlTransformPipeline: htmlTransformPipeline,
            TemplateResolver: templateResolver,
            RenderDocuments: renderAssetPlan.RenderDocuments,
            RoutedDocuments: routeResult.RoutedDocuments,
            RenderDependencyHashResolver: renderDependencyHashResolver,
            RouteMetadata: routeMetadata,
            PrecomputedEntries: renderAssetPlan.RenderEntries),
            cancellationToken);
        metrics.Merge(result.StageMetrics);
        return result;
    }

    /// <summary>
    /// With breadcrumb home/parent enabled, a page's BreadcrumbList depends on other pages' titles,
    /// so each page's dependency hash also covers its breadcrumb nodes.
    /// </summary>
    internal static Func<RouteInfo, string>? WithBreadcrumbDependencies(
        SeoSchemaConfig schema,
        BreadcrumbDescriptorCatalog? breadcrumbs,
        string renderDependencyHash,
        Func<RouteInfo, string>? inner)
    {
        if (breadcrumbs is null || (!schema.BreadcrumbHome && schema.BreadcrumbParent is null))
        {
            return inner;
        }

        return route =>
        {
            var hash = inner?.Invoke(route) ?? renderDependencyHash;
            return breadcrumbs.Find(route.Url) is { } descriptor
                ? RenderDependencyHasher.ComputeForBreadcrumb(hash, descriptor)
                : hash;
        };
    }

    private static Func<RouteInfo, string>? BuildRenderDependencyHashResolver(
        IReadOnlyList<RoutedContentDocument> renderDocuments,
        ListRouteGraph listRouteGraph,
        string renderDependencyHash,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
    {
        if (routeMetadata is null)
        {
            return null;
        }

        var contentByOutputPath = renderDocuments.ToDictionary(
            document => BuildPathUtils.NormalizeRelPath(document.Route.OutputPath),
            document => document.Document,
            StringComparer.OrdinalIgnoreCase);
        return route =>
        {
            var graphRoute = listRouteGraph.FindByOutputPath(route.OutputPath);
            var metadataRouteUrl = graphRoute?.MetadataRouteUrl;
            if (metadataRouteUrl is null)
            {
                contentByOutputPath.TryGetValue(
                    BuildPathUtils.NormalizeRelPath(route.OutputPath),
                    out var document);
                metadataRouteUrl = RouteMetadataApplicator.ResolveDependencyRouteUrl(
                    document,
                    route.Url);
            }

            return metadataRouteUrl is null
                ? renderDependencyHash
                : RenderDependencyHasher.ComputeForRoute(
                    renderDependencyHash,
                    metadataRouteUrl,
                    routeMetadata);
        };
    }
}
