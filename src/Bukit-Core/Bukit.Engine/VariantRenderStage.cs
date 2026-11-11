using Bukit.Theme;
using System.Diagnostics;
using System.Security.Cryptography;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Analytics;
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
        var dependencySnapshot = manifestSetup.IncrementalEnabled
            ? RenderDependencyHasher.ComputeSnapshot(
                context.Config,
                siteModel,
                context.Overrides.ExecutionMode,
                seoAlternates: seoStage.SeoAlternates,
                imageSourceFingerprint: imageSourceFingerprint,
                pagesFingerprint: () => ComputePagesFingerprint(renderAssetPlan.RenderDocuments, routeMetadata))
            : null;
        var renderDependencyHash = dependencySnapshot?.BaseHash ?? string.Empty;
        renderDependencyHashStopwatch.Stop();
        metrics.AddDuration(
            "renderDependencyHash",
            renderDependencyHashStopwatch.ElapsedMilliseconds);
        IReadOnlyList<SignedLinkFinding> signedLinks = [];
        if (dependencySnapshot is not null)
        {
            var signedLinkContext = new RenderDependencyContext(context.Config, siteModel, context.Overrides.ExecutionMode, AnalyticsRendererContract.Version);
            var pageDocuments = renderAssetPlan.RenderDocuments.Select(document => document.Document);
            if (context.DefaultLanguage is null)
            {
                SignedLinkDiagnostics.Report(signedLinkContext, pageDocuments, logger);
            }
            else
            {
                // Language variants usually read the same shared data; the parent logs them once.
                signedLinks = SignedLinkDiagnostics.FindFindings(signedLinkContext, pageDocuments);
            }
        }
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
            PrecomputedEntries: renderAssetPlan.RenderEntries,
            DependencySnapshot: dependencySnapshot),
            cancellationToken);
        metrics.Merge(result.StageMetrics);
        return result with { SignedLinks = signedLinks };
    }

    /// <summary>
    /// Fingerprint of the <c>pages</c> collection: the all-pages list of page models and the pages
    /// section sources read. Covers each document's route, template and metadata (titles, fields,
    /// record) in list order, and the route metadata that can override titles and summaries.
    /// </summary>
    internal static string ComputePagesFingerprint(
        IReadOnlyList<RoutedContentDocument> documents,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var writer = new RenderDependencyHashWriter(hasher);
        foreach (var document in documents)
        {
            writer.AppendFramedValue("url", document.Route.Url);
            writer.AppendFramedValue("outputPath", document.Route.OutputPath);
            writer.AppendFramedValue("template", document.Route.Template);
            writer.AppendFramedValue("metadata", IncrementalBuildEngine.ComputeMetadataHash(document.Document));
        }

        if (routeMetadata is not null)
        {
            foreach (var (url, entry) in routeMetadata.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                writer.AppendFramedValue("routeMetadata", url);
                writer.AppendFramedValue("route", entry.Route);
                writer.AppendFramedValue("title", entry.Title);
                writer.AppendFramedValue("summary", entry.Summary);
                writer.AppendFramedValue("seoTitle", entry.SeoTitle);
                writer.AppendFramedValue("seoDescription", entry.SeoDescription);
            }
        }

        return HashUtil.ToHexLower(hasher.GetHashAndReset());
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
