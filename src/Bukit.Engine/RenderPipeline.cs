using System.Collections.Concurrent;
using System.Diagnostics;
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
        var pageStopwatch = Stopwatch.StartNew();
        var pageResult = await PageRenderDispatcher.RenderPagesAsync(
            context.RenderQueue,
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
            context.HtmlPostProcessor);
        pageStopwatch.Stop();

        var specialRenderReasons = new ConcurrentDictionary<string, int>(pageResult.RenderReasons, StringComparer.OrdinalIgnoreCase);
        var listStopwatch = Stopwatch.StartNew();
        var listResult = await PageRenderDispatcher.RenderSpecialListsAsync(
            context.Routed,
            context.BodyStore,
            context.Renderer,
            context.SiteModel,
            context.Collections,
            context.LayoutsDir,
            context.ListPageContentMode,
            context.OutputPathEncoding,
            context.OutputDir,
            context.TemplateHash,
            context.RenderDependencyHash,
            context.IncrementalEnabled,
            context.Manifest,
            currentKeys,
            specialRenderReasons,
            context.MaxDegreeOfParallelism,
            cancellationToken,
            context.ListItemSeoBuilder,
            context.ListSeoBuilder,
            context.ListHtmlPostProcessor);
        listStopwatch.Stop();

        if (context.IncrementalEnabled && context.ManifestEntries is not null)
        {
            foreach (var entry in context.ManifestEntries)
            {
                context.Manifest.Entries[entry.Key] = entry.Value;
            }
        }

        var metricsCollector = new BuildStageMetricsCollector();
        metricsCollector.AddDuration("renderPages", pageStopwatch.ElapsedMilliseconds);
        metricsCollector.AddDuration("renderSpecialLists", listStopwatch.ElapsedMilliseconds);
        var metrics = BuildStageMetrics.Merge(metricsCollector.Snapshot(), pageResult.StageMetrics, listResult.StageMetrics);

        return new RenderPipelineResult(
            pageResult.RenderedCount + listResult.RenderedCount,
            pageResult.SkippedCount + listResult.SkippedCount,
            new Dictionary<string, int>(specialRenderReasons, StringComparer.OrdinalIgnoreCase),
            currentKeys,
            metrics);
    }
}
