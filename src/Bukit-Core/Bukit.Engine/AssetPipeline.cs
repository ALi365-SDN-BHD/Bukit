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
    string? FingerprintMode = null,
    IReadOnlyList<RenderEntry>? RenderEntries = null,
    ConcurrentDictionary<string, BuildManifestEntry>? ManifestEntries = null,
    string? ScssOutputDir = null);

internal sealed record AssetPipelineResult(
    BuildStageMetrics StageMetrics);

internal sealed record AssetPipelinePreparation(
    AssetOutputPlan OutputPlan,
    DirectoryCopyOptions CopyOptions,
    ThemeTokens? Tokens);

internal sealed class AssetPipeline
{
    public async Task<AssetPipelineResult> ExecuteAsync(
        AssetPipelineContext ctx,
        CancellationToken cancellationToken = default)
    {
        var preparation = await PrepareAsync(ctx, cancellationToken);
        return await ExecutePreparedAsync(ctx, preparation, cancellationToken);
    }

    internal static Task<AssetPipelinePreparation> PrepareAsync(
        AssetPipelineContext ctx,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var copyOptions = BuildCopyOptions(ctx);
        var tokens = ctx.ThemeRoot is null
            ? null
            : new ThemeTokensLoader().LoadWithInheritance(ctx.ThemeRoot, ctx.ParentThemeRoot);
        var outputPlan = AssetOutputPlan.Create(ctx, copyOptions, tokens, cancellationToken);
        BuildManifestTracker.PrepareAssetPlanOutputs(
            outputPlan.Items,
            ctx.OutputDir,
            outputPlan.DestinationComparer,
            ctx.Manifest,
            ctx.IncrementalEnabled,
            cancellationToken,
            manifestEntries: ctx.ManifestEntries);

        return Task.FromResult(new AssetPipelinePreparation(outputPlan, copyOptions, tokens));
    }

    internal static async Task<AssetPipelineResult> ExecutePreparedAsync(
        AssetPipelineContext ctx,
        AssetPipelinePreparation preparation,
        CancellationToken cancellationToken = default)
    {
        var metricsCollector = new BuildStageMetricsCollector();
        cancellationToken.ThrowIfCancellationRequested();

        var outputPlan = preparation.OutputPlan;
        var copyOptions = preparation.CopyOptions;
        var tokens = preparation.Tokens;
        var parallelTasks = new List<Task<BuildStageMetrics>>();

        var staticCopyItems = outputPlan.ForCopyCategory(AssetOutputCategory.Static);
        if (staticCopyItems.Count > 0)
        {
            parallelTasks.Add(CopyPlannedFilesAsync(
                ctx,
                staticCopyItems,
                "staticSync",
                copyOptions.HashMode,
                cancellationToken));
        }

        var assetCopyItems = outputPlan.ForCopyCategory(AssetOutputCategory.Assets);
        if (assetCopyItems.Count > 0)
        {
            parallelTasks.Add(CopyPlannedFilesAsync(
                ctx,
                assetCopyItems,
                "assetsSync",
                copyOptions.HashMode,
                cancellationToken));
        }

        if (tokens is not null)
        {
            parallelTasks.Add(GenerateTokensAsync(ctx, tokens, cancellationToken));
        }

        var mediaCopyItems = outputPlan.ForCopyCategory(AssetOutputCategory.Media);
        if (mediaCopyItems.Count > 0)
        {
            parallelTasks.Add(CopyPlannedFilesAsync(
                ctx,
                mediaCopyItems,
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
            outputPlan.DestinationComparer,
            ctx.Manifest,
            ctx.IncrementalEnabled,
            ctx.Logger,
            ctx.FingerprintMode,
            cancellationToken: cancellationToken);

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

}
