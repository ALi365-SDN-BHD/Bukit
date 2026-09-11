using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record BuildReportPipelineContext(
    AppConfig Config,
    string Language,
    string OutputDir,
    string BaseUrl,
    bool SearchSnippetsEnabled,
    IContentBodyStore BodyStore,
    IReadOnlyList<(RouteInfo Route, DateTimeOffset LastModified)> DerivedRoutes,
    IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex,
    IReadOnlyDictionary<string, SeoModel> SeoModels,
    IReadOnlyList<PluginExecutionInfo> PluginExecutions,
    int RenderedCount,
    int SkippedCount,
    IReadOnlyDictionary<string, int> RenderReasons,
    BuildStageMetrics StageMetrics,
    ILogger Logger,
    string? DefaultLanguage,
    IReadOnlyList<RoutedContentDocument> RoutedDocuments,
    ListRouteGraph? ListRouteGraph = null,
    IReadOnlyList<RouteInfo>? StaticRoutes = null,
    IReadOnlyList<PluginOutputTrackingInfo>? PluginOutputs = null,
    CanonicalContentGraph? ContentGraph = null,
    IReadOnlyList<RoutedContentDocument>? DerivedDocuments = null,
    IReadOnlyList<PublishProjectionResult>? ProjectionResults = null)
{
    public IReadOnlyList<RoutedContentDocument> DerivedDocuments { get; init; } = DerivedDocuments ?? Array.Empty<RoutedContentDocument>();
    public IReadOnlyList<RouteInfo> StaticRoutes { get; init; } = StaticRoutes ?? Array.Empty<RouteInfo>();
    public IReadOnlyList<PluginOutputTrackingInfo> PluginOutputs { get; init; } = PluginOutputs ?? Array.Empty<PluginOutputTrackingInfo>();
}

internal sealed class BuildReportPipeline
{
    internal BuildVariantResult Execute(BuildReportPipelineContext ctx)
    {
        var contentGraph = ctx.ContentGraph ?? CanonicalContentGraph.Empty;
        var projectionResults = ctx.ProjectionResults ?? [];
        SeoAuditReportWriter.Write(ctx.Config, ctx.OutputDir, ctx.SeoIndex, ctx.SeoModels, contentGraph, ctx.Logger, projectionResults);
        return new BuildVariantResult(
            Language: ctx.Language,
            OutputDir: ctx.OutputDir,
            BaseUrl: ctx.BaseUrl,
            SearchSnippetsEnabled: ctx.SearchSnippetsEnabled,
            BodyStore: ctx.BodyStore,
            DerivedRoutes: ctx.DerivedRoutes,
            SeoIndex: ctx.SeoIndex,
            SeoModels: ctx.SeoModels,
            PluginExecutions: ctx.PluginExecutions,
            RenderedCount: ctx.RenderedCount,
            SkippedCount: ctx.SkippedCount,
            RenderReasons: ctx.RenderReasons,
            StageMetrics: ctx.StageMetrics,
            RoutedDocuments: ctx.RoutedDocuments,
            ContentGraph: ctx.ContentGraph,
            ListRouteGraph: ctx.ListRouteGraph,
            StaticRoutes: ctx.StaticRoutes,
            PluginOutputs: ctx.PluginOutputs,
            DerivedDocuments: ctx.DerivedDocuments,
            ProjectionResults: projectionResults);
    }
}
