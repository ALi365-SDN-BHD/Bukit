using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
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
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> Routed,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> DerivedRouted,
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
    BuildContext? PluginContext = null,
    CanonicalContentGraph? ContentGraph = null);

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

        var result = new BuildVariantResult(
            ctx.Language,
            ctx.OutputDir,
            ctx.BaseUrl,
            ctx.SearchSnippetsEnabled,
            ctx.BodyStore,
            ctx.Routed,
            ctx.DerivedRouted,
            ctx.DerivedRoutes,
            ctx.SeoIndex,
            ctx.SeoModels,
            ctx.PluginExecutions,
            ctx.RenderedCount,
            ctx.SkippedCount,
            ctx.RenderReasons,
            ctx.StageMetrics,
            ctx.ContentGraph);
        var contentGraph = ctx.ContentGraph ?? CanonicalContentGraph.Empty;
        var projectionResults = _contentProjectionWriter.Write(new PublishProjectionContext(
            ctx.Config,
            ctx.OutputDir,
            contentGraph,
            ctx.Routed,
            ctx.DerivedRouted,
            ctx.SeoIndex,
            ctx.SeoModels,
            ctx.BodyStore,
            ctx.BaseUrl,
            ctx.SearchSnippetsEnabled,
            ctx.Logger,
            ctx.PluginContext));
        SeoAuditReportWriter.Write(ctx.Config, ctx.OutputDir, ctx.SeoIndex, ctx.SeoModels, contentGraph, ctx.Logger, projectionResults);
        return result;
    }
}
