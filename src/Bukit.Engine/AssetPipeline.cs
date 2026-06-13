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
        var parallelTasks = new List<Task<BuildStageMetrics>>();

        if (hasStaticDir || hasParentStaticDir)
        {
            parallelTasks.Add(CopyStaticFilesAsync(ctx, copyOptions, hasStaticDir, hasParentStaticDir, cancellationToken));
        }

        if (hasAssetsDir || hasParentAssetsDir)
        {
            parallelTasks.Add(ProcessAssetsAsync(ctx, copyOptions, hasAssetsDir, hasParentAssetsDir, cancellationToken));
        }

        if (ctx.ThemeRoot is not null)
        {
            parallelTasks.Add(GenerateTokensAsync(ctx, cancellationToken));
        }

        if (hasMediaDir)
        {
            parallelTasks.Add(SyncMediaAsync(ctx, cancellationToken));
        }

        var results = await Task.WhenAll(parallelTasks);
        foreach (var result in results)
        {
            metricsCollector.Merge(result);
        }

        if (hasStaticDir || hasParentStaticDir)
        {
            BuildManifestTracker.TrackStaticOutputs(
                ctx.ParentStaticDir, hasStaticDir ? ctx.StaticDir : null,
                ctx.OutputDir, ctx.Manifest, ctx.IncrementalEnabled, ctx.Logger,
                renderHtmlStaticFiles: false, fingerprintMode: ctx.FingerprintMode);
        }

        if (hasAssetsDir || hasParentAssetsDir)
        {
            BuildManifestTracker.TrackAssetOutputs(
                ctx.ParentAssetsDir, ctx.AssetsDir,
                ctx.OutputDir, ctx.Manifest, ctx.IncrementalEnabled, ctx.Logger,
                fingerprintMode: ctx.FingerprintMode);
        }

        return new AssetPipelineResult(metricsCollector.Snapshot());
    }

    private static Task<BuildStageMetrics> CopyStaticFilesAsync(
        AssetPipelineContext ctx, DirectoryCopyOptions copyOptions,
        bool hasStaticDir, bool hasParentStaticDir, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mc = new BuildStageMetricsCollector();
            var sw = Stopwatch.StartNew();

            if (hasParentStaticDir)
            {
                DirectoryCopy.Sync(ctx.ParentStaticDir!, ctx.OutputDir,
                    copyOptions,
                    outputRoot: ctx.OutputDir);
            }

            if (hasStaticDir)
            {
                DirectoryCopy.Sync(ctx.StaticDir!, ctx.OutputDir, copyOptions, outputRoot: ctx.OutputDir);
            }

            sw.Stop();
            mc.AddDuration("staticSync", sw.ElapsedMilliseconds);
            return mc.Snapshot();
        }, cancellationToken);
    }

    private static async Task<BuildStageMetrics> ProcessAssetsAsync(
        AssetPipelineContext ctx, DirectoryCopyOptions copyOptions,
        bool hasAssetsDir, bool hasParentAssetsDir, CancellationToken cancellationToken)
    {
        var mc = new BuildStageMetricsCollector();
        var sw = Stopwatch.StartNew();
        var assetsOutputDir = Path.Combine(ctx.OutputDir, "assets");

        if (hasParentAssetsDir)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(() =>
                DirectoryCopy.Sync(ctx.ParentAssetsDir!, assetsOutputDir, copyOptions, outputRoot: ctx.OutputDir),
                cancellationToken);
        }

        if (hasAssetsDir)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ctx.ScssConfig is not null)
            {
                await ScssCompiler.CompileIfEnabled(ctx.AssetsDir!, ctx.ScssConfig, ctx.Logger, cancellationToken);
            }

            if (ctx.ImageConfig is not null)
            {
                await ImageOptimizer.OptimizeIfEnabled(ctx.AssetsDir!, ctx.ImageConfig, ctx.Logger, cancellationToken);
            }

            await Task.Run(() =>
                DirectoryCopy.Sync(ctx.AssetsDir!, assetsOutputDir, copyOptions, outputRoot: ctx.OutputDir),
                cancellationToken);
        }

        sw.Stop();
        mc.AddDuration("assetsSync", sw.ElapsedMilliseconds);
        return mc.Snapshot();
    }

    private static Task<BuildStageMetrics> GenerateTokensAsync(AssetPipelineContext ctx, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mc = new BuildStageMetricsCollector();
            var sw = Stopwatch.StartNew();

            var tokensLoader = new ThemeTokensLoader();
            var tokens = tokensLoader.LoadWithInheritance(ctx.ThemeRoot!, ctx.ParentThemeRoot);
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

    private static Task<BuildStageMetrics> SyncMediaAsync(AssetPipelineContext ctx, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mc = new BuildStageMetricsCollector();
            var sw = Stopwatch.StartNew();

            BuildManifestTracker.SyncMediaOutputs(
                ctx.MediaDownloadDir!, ctx.OutputDir, ctx.Manifest,
                ctx.IncrementalEnabled, ctx.Logger, ctx.FingerprintMode);

            sw.Stop();
            mc.AddDuration("mediaCopy", sw.ElapsedMilliseconds);
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
}
