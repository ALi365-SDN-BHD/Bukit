using System.Diagnostics;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class VariantPluginStages
{
    internal static async Task RunDeriveAsync(
        BuildContext pluginContext,
        BuildStageMetricsCollector metrics,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var derived = await PluginRunner.RunDerivePagesAsync(pluginContext, cancellationToken);
        stopwatch.Stop();
        metrics.AddDuration("derivePages", stopwatch.ElapsedMilliseconds);
        foreach (var page in derived)
        {
            pluginContext.DerivedDocuments.Add(page);
            pluginContext.DerivedRoutes.Add((page.Route, page.LastModified ?? page.Document.PublishAt));
        }
    }

    internal static async Task RunAfterBuildAsync(
        BuildVariantContext context,
        BuildContext pluginContext,
        ManifestSetupResult manifestSetup,
        RenderPipelineResult renderPipelineResult,
        BuildStageMetricsCollector metrics,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var result = await new PluginPipeline().ExecuteAsync(new PluginPipelineContext(
            PluginContext: pluginContext,
            OutputDir: context.OutputDir,
            BaseUrl: context.BaseUrl,
            Manifest: manifestSetup.Manifest,
            ManifestPath: manifestSetup.ManifestPath,
            IncrementalEnabled: manifestSetup.IncrementalEnabled,
            CurrentKeys: renderPipelineResult.CurrentKeys,
            RenderedCount: renderPipelineResult.RenderedCount,
            SkippedCount: renderPipelineResult.SkippedCount,
            Logger: logger,
            Config: context.Config),
            cancellationToken);
        metrics.Merge(result.StageMetrics);
    }
}
