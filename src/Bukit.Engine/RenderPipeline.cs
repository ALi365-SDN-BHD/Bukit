using System.Collections.Concurrent;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record RenderPipelineContext(
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> RenderQueue,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> Routed,
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
    IReadOnlyList<RenderEntry>? StaticEntries = null,
    Func<ContentItem, RouteInfo, SeoModel>? SeoBuilder = null,
    Func<ContentItem, RouteInfo, PageInfo, string, string>? HtmlPostProcessor = null,
    Func<ContentItem, RouteInfo, SeoModel>? ListItemSeoBuilder = null,
    Func<RouteInfo, PageInfo, SeoModel>? ListSeoBuilder = null,
    Func<RouteInfo, PageInfo, string, string>? ListHtmlPostProcessor = null);

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

        foreach (var (item, route) in context.RenderQueue)
        {
            entries.Add(RenderEntry.ForPage(item, route));
        }

        var specialLists = SpecialListRouteBuilder.Build(context.Routed, context.Collections, context.LayoutsDir, context.ListPageContentMode, context.OutputPathEncoding);
        foreach (var x in specialLists)
        {
            entries.Add(RenderEntry.ForList(x.Route, x.Items, x.IncludeContent));
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
