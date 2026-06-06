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
    BuildContext? PluginContext = null,
    CanonicalContentGraph? ContentGraph = null,
    IReadOnlyList<RoutedContentDocument>? DerivedDocuments = null)
{
    public IReadOnlyList<RoutedContentDocument> DerivedDocuments { get; init; } = DerivedDocuments ?? Array.Empty<RoutedContentDocument>();
}

internal sealed class BuildReportPipeline
{
    private readonly IContentProjectionWriter _contentProjectionWriter;

    internal BuildReportPipeline()
        : this(new DefaultContentProjectionWriter())
    {
    }

    internal BuildReportPipeline(IContentProjectionWriter contentProjectionWriter)
    {
        _contentProjectionWriter = contentProjectionWriter;
    }

    internal BuildVariantResult Execute(BuildReportPipelineContext ctx)
    {
        if (ctx.DefaultLanguage is null)
        {
            ctx.Logger.Info($"Build completed: {Path.GetFullPath(ctx.OutputDir)}");
        }
        else
        {
            ctx.Logger.Info($"Build completed: {Path.GetFullPath(ctx.OutputDir)} (lang={ctx.Config.Site.Language})");
        }

        var contentGraph = ctx.ContentGraph ?? CanonicalContentGraph.Empty;
        var projectionResults = _contentProjectionWriter.Write(new PublishProjectionContext(
            Config: ctx.Config,
            OutputDir: ctx.OutputDir,
            ContentGraph: contentGraph,
            SeoIndex: ctx.SeoIndex,
            SeoModels: ctx.SeoModels,
            BodyStore: ctx.BodyStore,
            BaseUrl: ctx.BaseUrl,
            SearchSnippetsEnabled: ctx.SearchSnippetsEnabled,
            Logger: ctx.Logger,
            PluginContext: ctx.PluginContext,
            RoutedDocuments: ctx.RoutedDocuments,
            DerivedDocuments: ctx.DerivedDocuments));
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
            DerivedDocuments: ctx.DerivedDocuments,
            ProjectionResults: projectionResults);
    }
}
