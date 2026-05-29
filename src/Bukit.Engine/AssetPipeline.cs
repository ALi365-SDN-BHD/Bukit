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
    string? AssetHashMode,
    ScssConfig? ScssConfig,
    ImageOptimizationConfig? ImageConfig,
    ILogger Logger,
    bool PublishDotFiles);

internal sealed record AssetPipelineResult(
    BuildStageMetrics StageMetrics);

internal sealed class AssetPipeline
{
    public Task<AssetPipelineResult> ExecuteAsync(AssetPipelineContext ctx, CancellationToken cancellationToken = default)
        => Task.Run(() => ExecuteCore(ctx, cancellationToken), cancellationToken);

    private static AssetPipelineResult ExecuteCore(AssetPipelineContext ctx, CancellationToken cancellationToken)
    {
        var metricsCollector = new BuildStageMetricsCollector();

        cancellationToken.ThrowIfCancellationRequested();
        var hasStaticDir = ctx.StaticDir is not null && Directory.Exists(ctx.StaticDir);
        if (hasStaticDir || (ctx.ParentStaticDir is not null && Directory.Exists(ctx.ParentStaticDir)))
        {
            var staticStopwatch = Stopwatch.StartNew();
            var staticCopyOptions = BuildCopyOptions(ctx);

            if (ctx.ParentStaticDir is not null && Directory.Exists(ctx.ParentStaticDir))
            {
                if (!string.IsNullOrWhiteSpace(ctx.AssetHashMode))
                {
                    var hashOptions = staticCopyOptions with { HashMode = ctx.AssetHashMode };
                    DirectoryCopy.Sync(ctx.ParentStaticDir, ctx.OutputDir, hashOptions, outputRoot: ctx.OutputDir);
                }
                else
                {
                    DirectoryCopy.Sync(ctx.ParentStaticDir, ctx.OutputDir, staticCopyOptions, outputRoot: ctx.OutputDir);
                }
            }

            if (hasStaticDir)
            {
                DirectoryCopy.Sync(ctx.StaticDir!, ctx.OutputDir, staticCopyOptions, outputRoot: ctx.OutputDir);
            }

            BuildManifestTracker.TrackStaticOutputs(ctx.ParentStaticDir, hasStaticDir ? ctx.StaticDir : null, ctx.OutputDir, ctx.Manifest, ctx.IncrementalEnabled, ctx.Logger, renderHtmlStaticFiles: false);
            staticStopwatch.Stop();
            metricsCollector.AddDuration("staticSync", staticStopwatch.ElapsedMilliseconds);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (ctx.AssetsDir is not null && Directory.Exists(ctx.AssetsDir) || (ctx.ParentAssetsDir is not null && Directory.Exists(ctx.ParentAssetsDir)))
        {
            var assetsSyncStopwatch = Stopwatch.StartNew();
            var assetsCopyOptions = BuildCopyOptions(ctx);

            if (ctx.ParentAssetsDir is not null && Directory.Exists(ctx.ParentAssetsDir))
            {
                DirectoryCopy.Sync(ctx.ParentAssetsDir, Path.Combine(ctx.OutputDir, "assets"), assetsCopyOptions, outputRoot: ctx.OutputDir);
            }

            if (ctx.AssetsDir is not null && Directory.Exists(ctx.AssetsDir))
            {
                if (ctx.ScssConfig is not null)
                {
                    ScssCompiler.CompileIfEnabled(ctx.AssetsDir, ctx.ScssConfig, ctx.Logger);
                }

                if (ctx.ImageConfig is not null)
                {
                    ImageOptimizer.OptimizeIfEnabled(ctx.AssetsDir, ctx.ImageConfig, ctx.Logger);
                }

                var assetHashOptions = !string.IsNullOrWhiteSpace(ctx.AssetHashMode)
                    ? assetsCopyOptions with { HashMode = ctx.AssetHashMode }
                    : assetsCopyOptions;
                DirectoryCopy.Sync(ctx.AssetsDir, Path.Combine(ctx.OutputDir, "assets"), assetHashOptions, outputRoot: ctx.OutputDir);
            }

            BuildManifestTracker.TrackAssetOutputs(ctx.ParentAssetsDir, ctx.AssetsDir!, ctx.OutputDir, ctx.Manifest, ctx.IncrementalEnabled, ctx.Logger);
            assetsSyncStopwatch.Stop();
            metricsCollector.AddDuration("assetsSync", assetsSyncStopwatch.ElapsedMilliseconds);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (ctx.ThemeRoot is not null)
        {
            var tokensStopwatch = Stopwatch.StartNew();
            var tokensLoader = new ThemeTokensLoader();
            var tokens = tokensLoader.LoadWithInheritance(ctx.ThemeRoot, ctx.ParentThemeRoot);
            if (tokens is not null)
            {
                var tokensOutputPath = Path.Combine(ctx.OutputDir, "assets", "css", "theme-tokens.css");
                Directory.CreateDirectory(Path.GetDirectoryName(tokensOutputPath)!);
                ThemeTokensProcessor.WriteToFile(tokens, tokensOutputPath);
                ctx.Logger.Info($"event=tokens.generated output={tokensOutputPath}");
            }
            tokensStopwatch.Stop();
            metricsCollector.AddDuration("tokensGen", tokensStopwatch.ElapsedMilliseconds);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (ctx.MediaDownloadDir is not null && Directory.Exists(ctx.MediaDownloadDir))
        {
            var mediaCopyStopwatch = Stopwatch.StartNew();
            BuildManifestTracker.SyncMediaOutputs(ctx.MediaDownloadDir, ctx.OutputDir, ctx.Manifest, ctx.IncrementalEnabled, ctx.Logger);
            mediaCopyStopwatch.Stop();
            metricsCollector.AddDuration("mediaCopy", mediaCopyStopwatch.ElapsedMilliseconds);
        }

        return new AssetPipelineResult(metricsCollector.Snapshot());
    }

    private static DirectoryCopyOptions BuildCopyOptions(AssetPipelineContext ctx)
    {
        return ctx.PublishDotFiles
            ? new DirectoryCopyOptions { IgnoreDotPrefixedFiles = false }
            : new DirectoryCopyOptions();
    }
}
