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
    string BaseUrl,
    ITemplateRenderer? Renderer,
    SiteModel SiteModel,
    string? StaticTemplate,
    BuildManifest Manifest,
    bool IncrementalEnabled,
    string? AssetHashMode,
    ScssConfig? ScssConfig,
    ImageOptimizationConfig? ImageConfig,
    ILogger Logger,
    ConcurrentDictionary<string, byte> CurrentKeys);

internal sealed record AssetPipelineResult(
    BuildStageMetrics StageMetrics);

internal sealed class AssetPipeline
{
    public Task<AssetPipelineResult> ExecuteAsync(AssetPipelineContext ctx, CancellationToken cancellationToken = default)
    {
        var metricsCollector = new BuildStageMetricsCollector();

        var hasStaticDir = ctx.StaticDir is not null && Directory.Exists(ctx.StaticDir);
        if (hasStaticDir || (ctx.ParentStaticDir is not null && Directory.Exists(ctx.ParentStaticDir)))
        {
            var staticStopwatch = Stopwatch.StartNew();
            if (ctx.ParentStaticDir is not null && Directory.Exists(ctx.ParentStaticDir))
            {
                if (!string.IsNullOrWhiteSpace(ctx.AssetHashMode))
                {
                    DirectoryCopy.Sync(ctx.ParentStaticDir, ctx.OutputDir, new DirectoryCopyOptions { HashMode = ctx.AssetHashMode });
                }
                else
                {
                    DirectoryCopy.Sync(ctx.ParentStaticDir, ctx.OutputDir);
                }
            }

            if (hasStaticDir)
            {
                if (!string.IsNullOrWhiteSpace(ctx.StaticTemplate) && ctx.Renderer is not null)
                {
                    StaticFileService.RenderStaticFiles(ctx.StaticDir!, ctx.OutputDir, ctx.Renderer, ctx.SiteModel, ctx.StaticTemplate, ctx.BaseUrl, ctx.CurrentKeys, cancellationToken, ctx.Logger.Warn);
                }
                else
                {
                    DirectoryCopy.Sync(ctx.StaticDir!, ctx.OutputDir);
                }
            }

            SiteEngine.TrackStaticOutputs(ctx.ParentStaticDir, hasStaticDir ? ctx.StaticDir : null, ctx.OutputDir, ctx.Manifest, ctx.IncrementalEnabled, ctx.Logger, !string.IsNullOrWhiteSpace(ctx.StaticTemplate));
            staticStopwatch.Stop();
            metricsCollector.AddDuration("staticSync", staticStopwatch.ElapsedMilliseconds);
        }

        if (ctx.AssetsDir is not null && Directory.Exists(ctx.AssetsDir) || (ctx.ParentAssetsDir is not null && Directory.Exists(ctx.ParentAssetsDir)))
        {
            var assetsSyncStopwatch = Stopwatch.StartNew();
            if (ctx.ParentAssetsDir is not null && Directory.Exists(ctx.ParentAssetsDir))
            {
                DirectoryCopy.Sync(ctx.ParentAssetsDir, Path.Combine(ctx.OutputDir, "assets"));
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
                    ? new DirectoryCopyOptions { HashMode = ctx.AssetHashMode }
                    : null;
                if (assetHashOptions is not null)
                {
                    DirectoryCopy.Sync(ctx.AssetsDir, Path.Combine(ctx.OutputDir, "assets"), assetHashOptions);
                }
                else
                {
                    DirectoryCopy.Sync(ctx.AssetsDir, Path.Combine(ctx.OutputDir, "assets"));
                }
            }

            SiteEngine.TrackAssetOutputs(ctx.ParentAssetsDir, ctx.AssetsDir!, ctx.OutputDir, ctx.Manifest, ctx.IncrementalEnabled, ctx.Logger);
            assetsSyncStopwatch.Stop();
            metricsCollector.AddDuration("assetsSync", assetsSyncStopwatch.ElapsedMilliseconds);
        }

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

        if (ctx.MediaDownloadDir is not null && Directory.Exists(ctx.MediaDownloadDir))
        {
            var mediaCopyStopwatch = Stopwatch.StartNew();
            SiteEngine.SyncMediaOutputs(ctx.MediaDownloadDir, ctx.OutputDir, ctx.Manifest, ctx.IncrementalEnabled, ctx.Logger);
            mediaCopyStopwatch.Stop();
            metricsCollector.AddDuration("mediaCopy", mediaCopyStopwatch.ElapsedMilliseconds);
        }

        return Task.FromResult(new AssetPipelineResult(metricsCollector.Snapshot()));
    }
}
