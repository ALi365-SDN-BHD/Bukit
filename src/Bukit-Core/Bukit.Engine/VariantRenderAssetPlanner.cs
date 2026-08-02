using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record VariantRenderAssetPlan(
    IReadOnlyList<RoutedContentDocument> RenderDocuments,
    IReadOnlyList<RouteInfo> ListRoutes,
    IReadOnlyList<RenderEntry> RenderEntries,
    AssetPipelineContext AssetPipelineContext);

internal static class VariantRenderAssetPlanner
{
    internal static VariantRenderAssetPlan Create(
        BuildVariantContext context,
        RoutePipelineResult routeResult,
        IReadOnlyList<RoutedContentDocument> derivedDocuments,
        IReadOnlyList<RenderEntry>? staticEntries,
        SiteModel siteModel,
        ManifestSetupResult manifestSetup,
        string? themeRootForTokens,
        string? parentThemeRootForTokens,
        ILogger logger)
    {
        var renderDocuments = routeResult.RoutedDocuments
            .Concat(derivedDocuments)
            .ToList();
        var renderEntries = RenderPipeline.BuildEntries(
            renderDocuments,
            routeResult.RoutedDocuments,
            routeResult.ListRouteGraph,
            context.LayoutsDir,
            context.Config.Build.ListPageContentMode,
            siteModel.Language,
            staticEntries);
        var assetPipelineContext = new AssetPipelineContext(
            StaticDir: Directory.Exists(context.StaticDir) ? context.StaticDir : null,
            ParentStaticDir: context.ParentStaticDir,
            AssetsDir: context.AssetsDir,
            ParentAssetsDir: context.ParentAssetsDir,
            MediaDownloadDir: context.MediaDownloadDir,
            ThemeRoot: themeRootForTokens,
            ParentThemeRoot: parentThemeRootForTokens,
            OutputDir: context.OutputDir,
            Manifest: manifestSetup.Manifest,
            IncrementalEnabled: manifestSetup.IncrementalEnabled,
            FingerprintMode: context.Config.Build.FingerprintMode,
            ScssConfig: context.Config.Theme.Scss,
            ImageConfig: context.Config.Theme.Images,
            Logger: logger,
            PublishDotFiles: context.Config.Build.PublishDotFiles,
            FollowSymlinks: context.Config.Build.FollowSymlinks,
            RenderEntries: renderEntries,
            ManifestEntries: manifestSetup.ManifestEntries,
            ScssOutputDir: context.ScssOutputDir);

        return new VariantRenderAssetPlan(
            renderDocuments,
            routeResult.ListRoutes,
            renderEntries,
            assetPipelineContext);
    }
}
