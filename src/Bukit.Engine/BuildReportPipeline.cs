using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
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
    string? DefaultLanguage);

internal sealed class BuildReportPipeline
{
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
            ctx.StageMetrics);
        SeoAuditReportWriter.Write(ctx.Config, ctx.OutputDir, ctx.SeoIndex, ctx.SeoModels, ctx.Logger);
        return result;
    }
}
