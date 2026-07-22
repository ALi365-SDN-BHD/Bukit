using System.Collections.Concurrent;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Engine.RouteMetadata;

namespace Bukit.Engine;

internal sealed record RenderPipelineContext(
    IContentBodyStore BodyStore,
    ITemplateRenderer Renderer,
    SiteModel SiteModel,
    IReadOnlyDictionary<string, CollectionConfig>? Collections,
    string LayoutsDir,
    string ListPageContentMode,
    string OutputPathEncoding,
    string OutputDir,
    string TemplateHash,
    string RenderDependencyHash,
    bool IncrementalEnabled,
    BuildManifest Manifest,
    ConcurrentDictionary<string, BuildManifestEntry>? ManifestEntries,
    int MaxDegreeOfParallelism,
    ILogger Logger,
    ListRouteGraph ListRouteGraph,
    IReadOnlyList<RoutedContentDocument> RenderDocuments,
    IReadOnlyList<RoutedContentDocument> RoutedDocuments,
    IReadOnlyList<RenderEntry>? StaticEntries = null,
    Func<ContentDocument, RouteInfo, SeoModel>? SeoBuilder = null,
    Func<ContentDocument, RouteInfo, SeoModel>? ListItemSeoBuilder = null,
    Func<RouteInfo, PageInfo, SeoModel>? ListSeoBuilder = null,
    HtmlTransformPipeline? HtmlTransformPipeline = null,
    ThemeTemplateResolver? TemplateResolver = null,
    Func<RouteInfo, string>? RenderDependencyHashResolver = null,
    IReadOnlyDictionary<string, RouteMetadataEntry>? RouteMetadata = null,
    IReadOnlyList<RenderEntry>? PrecomputedEntries = null)
{
}

internal sealed record RenderPipelineResult(
    int RenderedCount,
    int SkippedCount,
    IReadOnlyDictionary<string, int> RenderReasons,
    ConcurrentDictionary<string, byte> CurrentKeys,
    BuildStageMetrics StageMetrics);

internal sealed class RenderPipeline
{
    public async Task<RenderPipelineResult> ExecuteAsync(RenderPipelineContext context, CancellationToken cancellationToken = default)
    {
        var currentKeys = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var entries = context.PrecomputedEntries ?? BuildEntries(
            context.RenderDocuments,
            context.RoutedDocuments,
            context.ListRouteGraph,
            context.LayoutsDir,
            context.ListPageContentMode,
            context.SiteModel.Language,
            context.StaticEntries);

        var dispatchResult = await PageRenderDispatcher.DispatchAsync(
            entries,
            context.BodyStore,
            context.Renderer,
            context.SiteModel,
            context.OutputDir,
            context.TemplateHash,
            context.RenderDependencyHash,
            context.IncrementalEnabled,
            context.Manifest,
            context.ManifestEntries,
            currentKeys,
            context.MaxDegreeOfParallelism,
            context.Logger,
            cancellationToken,
            context.SeoBuilder,
            context.ListSeoBuilder,
            context.RenderDependencyHashResolver,
            context.RouteMetadata,
            context.HtmlTransformPipeline);

        if (context.IncrementalEnabled && context.ManifestEntries is not null)
        {
            foreach (var entry in context.ManifestEntries)
            {
                context.Manifest.Entries[entry.Key] = entry.Value;
            }
        }

        return new RenderPipelineResult(
            dispatchResult.RenderedCount,
            dispatchResult.SkippedCount,
            dispatchResult.RenderReasons,
            currentKeys,
            dispatchResult.StageMetrics);
    }

    internal static IReadOnlyList<RenderEntry> BuildEntries(
        IReadOnlyList<RoutedContentDocument> renderDocuments,
        IReadOnlyList<RoutedContentDocument> routedDocuments,
        ListRouteGraph listRouteGraph,
        string layoutsDir,
        string listPageContentMode,
        string language,
        IReadOnlyList<RenderEntry>? staticEntries)
    {
        var entries = new List<RenderEntry>();

        foreach (var document in renderDocuments)
        {
            var graphRoute = listRouteGraph.FindByOutputPath(document.Route.OutputPath);
            var taxonomyMetadataRoute = graphRoute is
            {
                Kind: ListRouteKind.TaxonomyIndex or ListRouteKind.TaxonomyTermPage,
                RouteMetadataApplied: true
            }
                ? graphRoute
                : null;
            entries.Add(RenderEntry.ForPage(document.Document, document.Route, taxonomyMetadataRoute));
        }

        var specialLists = ListRouteRenderPlanBuilder.Build(
            listRouteGraph,
            routedDocuments,
            layoutsDir,
            listPageContentMode,
            language);
        foreach (var x in specialLists)
        {
            entries.Add(RenderEntry.ForList(x.Route, x.Items, x.IncludeContent, x.PageFields, x.PageContext));
        }

        if (staticEntries is { Count: > 0 })
        {
            entries.AddRange(staticEntries);
        }

        return entries;
    }
}
