using System.Collections.Concurrent;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

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
    Func<ContentDocument, RouteInfo, PageInfo, string, string>? HtmlPostProcessor = null,
    Func<ContentDocument, RouteInfo, SeoModel>? ListItemSeoBuilder = null,
    Func<RouteInfo, PageInfo, SeoModel>? ListSeoBuilder = null,
    Func<RouteInfo, PageInfo, string, string>? ListHtmlPostProcessor = null,
    ThemeTemplateResolver? TemplateResolver = null)
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
        var entries = new List<RenderEntry>();

        foreach (var document in context.RenderDocuments)
        {
            entries.Add(RenderEntry.ForPage(document.Document, document.Route));
        }

        var specialLists = ListRouteRenderPlanBuilder.Build(
            context.ListRouteGraph,
            context.RoutedDocuments,
            context.LayoutsDir,
            context.ListPageContentMode);
        foreach (var x in specialLists)
        {
            entries.Add(RenderEntry.ForList(x.Route, x.Items, x.IncludeContent, x.PageFields, x.PageContext));
        }

        if (context.StaticEntries is { Count: > 0 })
        {
            entries.AddRange(context.StaticEntries);
        }

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
            context.HtmlPostProcessor,
            context.ListSeoBuilder,
            context.ListHtmlPostProcessor);

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
}
