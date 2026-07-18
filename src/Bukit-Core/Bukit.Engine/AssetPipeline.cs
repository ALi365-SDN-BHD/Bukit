using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Theme;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record AssetPipelineContext(
    string? StaticDir,
    string? ParentStaticDir,
    string? AssetsDir,
    string? ParentAssetsDir,
    string? MediaDownloadDir,
    string? ThemeRoot,
    string? ParentThemeRoot,
    string OutputDir,
    BuildManifest Manifest,
    bool IncrementalEnabled,
    ScssConfig? ScssConfig,
    ImageOptimizationConfig? ImageConfig,
    ILogger Logger,
    bool PublishDotFiles,
    bool FollowSymlinks,
    string? FingerprintMode = null);

internal sealed record AssetPipelineResult(
    BuildStageMetrics StageMetrics);

internal sealed class AssetPipeline
{
    public Task<AssetPipelineResult> ExecuteAsync(AssetPipelineContext ctx, CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(ctx, cancellationToken);

    private static async Task<AssetPipelineResult> ExecuteCoreAsync(AssetPipelineContext ctx, CancellationToken cancellationToken)
    {
        var metricsCollector = new BuildStageMetricsCollector();
        cancellationToken.ThrowIfCancellationRequested();

        var hasStaticDir = ctx.StaticDir is not null && Directory.Exists(ctx.StaticDir);
        var hasParentStaticDir = ctx.ParentStaticDir is not null && Directory.Exists(ctx.ParentStaticDir);
        var hasAssetsDir = ctx.AssetsDir is not null && Directory.Exists(ctx.AssetsDir);
        var hasParentAssetsDir = ctx.ParentAssetsDir is not null && Directory.Exists(ctx.ParentAssetsDir);
        var hasMediaDir = ctx.MediaDownloadDir is not null && Directory.Exists(ctx.MediaDownloadDir);

        var copyOptions = BuildCopyOptions(ctx);
        await PrepareAssetSourcesAsync(ctx, hasAssetsDir, cancellationToken);
        var tokens = ctx.ThemeRoot is null
            ? null
            : new ThemeTokensLoader().LoadWithInheritance(ctx.ThemeRoot, ctx.ParentThemeRoot);
        var outputPlan = AssetOutputPlan.Create(ctx, copyOptions, tokens, cancellationToken);
        BuildManifestTracker.PrepareAssetPlanOutputs(
            outputPlan.Items,
            ctx.OutputDir,
            ctx.Manifest,
            ctx.IncrementalEnabled,
            cancellationToken);
        var parallelTasks = new List<Task<BuildStageMetrics>>();

        if (hasStaticDir || hasParentStaticDir)
        {
            parallelTasks.Add(CopyPlannedFilesAsync(
                ctx,
                outputPlan.ForCategory(AssetOutputCategory.Static),
                "staticSync",
                copyOptions.HashMode,
                cancellationToken));
        }

        if (hasAssetsDir || hasParentAssetsDir)
        {
            parallelTasks.Add(CopyPlannedFilesAsync(
                ctx,
                outputPlan.ForCategory(AssetOutputCategory.Assets),
                "assetsSync",
                copyOptions.HashMode,
                cancellationToken));
        }

        if (ctx.ThemeRoot is not null)
        {
            parallelTasks.Add(GenerateTokensAsync(ctx, tokens, cancellationToken));
        }

        if (hasMediaDir)
        {
            parallelTasks.Add(CopyPlannedFilesAsync(
                ctx,
                outputPlan.ForCategory(AssetOutputCategory.Media),
                "mediaCopy",
                "size-time",
                cancellationToken));
        }

        var results = await Task.WhenAll(parallelTasks);
        foreach (var result in results)
        {
            metricsCollector.Merge(result);
        }

        BuildManifestTracker.TrackAssetPlanOutputs(
            outputPlan.Items,
            ctx.OutputDir,
            ctx.Manifest,
            ctx.IncrementalEnabled,
            ctx.Logger,
            ctx.FingerprintMode);

        return new AssetPipelineResult(metricsCollector.Snapshot());
    }

    private static Task<BuildStageMetrics> CopyPlannedFilesAsync(
        AssetPipelineContext ctx,
        IReadOnlyList<AssetOutputItem> items,
        string metricName,
        string hashMode,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var mc = new BuildStageMetricsCollector();
            var sw = Stopwatch.StartNew();

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.Combine(
                    ctx.OutputDir,
                    item.Destination.Replace('/', Path.DirectorySeparatorChar));
                DirectoryCopy.SyncPlannedFile(
                    item.Source,
                    destination,
                    hashMode,
                    ctx.OutputDir,
                    item.PhysicalSourceRoot ?? throw new InvalidOperationException("Planned asset source root is missing."),
                    item.CopyOptions ?? throw new InvalidOperationException("Planned asset copy options are missing."));
            }

            sw.Stop();
            mc.AddDuration(metricName, sw.ElapsedMilliseconds);
            return mc.Snapshot();
        }, cancellationToken);
    }

    private static Task<BuildStageMetrics> GenerateTokensAsync(
        AssetPipelineContext ctx,
        ThemeTokens? tokens,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mc = new BuildStageMetricsCollector();
            var sw = Stopwatch.StartNew();

            if (tokens is not null)
            {
                var tokensOutputPath = Path.Combine(ctx.OutputDir, "assets", "css", "theme-tokens.css");
                Directory.CreateDirectory(Path.GetDirectoryName(tokensOutputPath)!);
                ThemeTokensProcessor.WriteToFile(tokens, tokensOutputPath);
                ctx.Logger.Info($"event=tokens.generated output={tokensOutputPath}");
            }

            sw.Stop();
            mc.AddDuration("tokensGen", sw.ElapsedMilliseconds);
            return mc.Snapshot();
        }, cancellationToken);
    }

    private static DirectoryCopyOptions BuildCopyOptions(AssetPipelineContext ctx)
    {
        return new DirectoryCopyOptions
        {
            IgnoreDotPrefixedFiles = !ctx.PublishDotFiles,
            FollowSymlinks = ctx.FollowSymlinks
        };
    }

    private static async Task PrepareAssetSourcesAsync(
        AssetPipelineContext ctx,
        bool hasAssetsDir,
        CancellationToken cancellationToken)
    {
        if (!hasAssetsDir)
        {
            return;
        }

        if (ctx.ScssConfig is not null)
        {
            await ScssCompiler.CompileIfEnabled(ctx.AssetsDir!, ctx.ScssConfig, ctx.Logger, cancellationToken);
        }

        if (ctx.ImageConfig is not null)
        {
            await ImageOptimizer.OptimizeIfEnabled(ctx.AssetsDir!, ctx.ImageConfig, ctx.Logger, cancellationToken);
        }
    }
}
