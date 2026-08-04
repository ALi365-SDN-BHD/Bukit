using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record PluginPipelineContext(
    BuildContext PluginContext,
    string OutputDir,
    string BaseUrl,
    BuildManifest Manifest,
    string ManifestPath,
    bool IncrementalEnabled,
    ConcurrentDictionary<string, byte> CurrentKeys,
    int RenderedCount,
    int SkippedCount,
    ILogger Logger,
    AppConfig Config,
    PluginExecutionSession PluginSession);

internal sealed record PluginPipelineResult(
    BuildStageMetrics StageMetrics);

internal sealed class PluginPipeline
{
    internal async Task<PluginPipelineResult> ExecuteAsync(PluginPipelineContext ctx, CancellationToken cancellationToken = default)
    {
        var metricsCollector = new BuildStageMetricsCollector();

        if (ctx.IncrementalEnabled)
        {
            BuildManifestTracker.DeleteStaleManifestOutputs(ctx.OutputDir, ctx.Manifest, ctx.CurrentKeys, ctx.Logger);
        }

        ctx.PluginContext.Data[BuildContextDataKeys.PriorPluginOutputs] = ctx.Manifest.PluginOutputs
            .Select(entry =>
            {
                var path = string.IsNullOrWhiteSpace(entry.Value.Path)
                    ? entry.Key
                    : entry.Value.Path;
                return new PluginOutputTrackingInfo(
                    entry.Value.Plugin,
                    entry.Value.Hook,
                    BuildPathUtils.NormalizeRelPath(path));
            })
            .ToHashSet();

        var afterBuildStopwatch = Stopwatch.StartNew();
        await PluginRunner.RunAfterBuildAsync(
            ctx.PluginContext,
            ctx.PluginSession,
            cancellationToken);
        BuildManifestTracker.TrackPluginOutputs(ctx.PluginContext, ctx.OutputDir, ctx.Manifest, ctx.IncrementalEnabled, ctx.Logger, ctx.Config.Build.FingerprintMode);
        afterBuildStopwatch.Stop();
        metricsCollector.AddDuration("afterBuildPlugins", afterBuildStopwatch.ElapsedMilliseconds);

        if (ctx.IncrementalEnabled)
        {
            ctx.Manifest.Save(ctx.ManifestPath);
            ctx.Logger.Info($"Incremental build: rendered={ctx.RenderedCount}, skipped={ctx.SkippedCount}, cache={Path.GetDirectoryName(ctx.ManifestPath)}");
        }

        return new PluginPipelineResult(metricsCollector.Snapshot());
    }
}
