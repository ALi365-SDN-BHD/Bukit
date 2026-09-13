using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

internal sealed class I18nPreparedBuild : IAsyncDisposable
{
    internal I18nPreparedBuild(
        string rootDir,
        string projectRootDir,
        string finalOutputDir,
        ConfigOverrides overrides,
        BuildPlan plan,
        ContentPipelineResult content,
        AssetSourceWorkspace assets,
        BuildDiagnosticLogger logger)
    {
        RootDir = rootDir;
        ProjectRootDir = projectRootDir;
        FinalOutputDir = finalOutputDir;
        Overrides = overrides;
        Plan = plan;
        Content = content;
        Assets = assets;
        Logger = logger;
    }

    internal string RootDir { get; }
    internal string ProjectRootDir { get; }
    internal string FinalOutputDir { get; }
    internal ConfigOverrides Overrides { get; }
    internal BuildPlan Plan { get; }
    internal ContentPipelineResult Content { get; }
    internal AssetSourceWorkspace Assets { get; }
    internal BuildDiagnosticLogger Logger { get; }

    public async ValueTask DisposeAsync()
    {
        Assets.Dispose();
        await SiteEngine.DisposeContentBodyStoreAsync(Content.BodyStore).ConfigureAwait(false);
    }
}

public sealed partial class SiteEngine
{
    internal async Task<I18nPreparedBuild> PrepareI18nWorktreeBuildAsync(
        AppConfig config,
        string rootDir,
        string projectRootDir,
        string finalOutputDir,
        ConfigOverrides overrides,
        CancellationToken cancellationToken)
    {
        var buildLogger = new BuildDiagnosticLogger(_logger);
        var plan = BuildPlanner.Plan(
            config,
            rootDir,
            overrides,
            buildLogger,
            _timeProvider.GetUtcNow(),
            internalOutputDir: overrides.Output) with
        {
            EffectiveConfig = config
        };
        var contentPipeline = new ContentPipeline(_contentProviderFactory, buildLogger);
        var content = await contentPipeline.ExecuteAsync(
            plan.EffectiveConfig,
            rootDir,
            overrides,
            plan.MediaCacheDir,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var assets = await AssetSourceWorkspace.PrepareAsync(
                plan.AssetsDir,
                plan.EffectiveConfig.Theme.Scss,
                plan.EffectiveConfig.Theme.Images,
                buildLogger,
                plan.EffectiveConfig.Build.PublishDotFiles,
                plan.EffectiveConfig.Build.FollowSymlinks,
                cancellationToken).ConfigureAwait(false);
            return new I18nPreparedBuild(rootDir, projectRootDir, finalOutputDir, overrides, plan, content, assets, buildLogger);
        }
        catch
        {
            await DisposeContentBodyStoreAsync(content.BodyStore).ConfigureAwait(false);
            throw;
        }
    }

    internal async Task<(BuildVariantResult Result, int WarningCount, int ErrorCount)> ExecuteI18nWorktreeVariantAsync(
        I18nWorkerRequest request,
        I18nContentSnapshot snapshot,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (request.Schema != I18nWorktreeProtocol.RequestSchema ||
            snapshot.Schema != I18nWorktreeProtocol.ContentSchema ||
            !string.Equals(request.Head, snapshot.Head, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Worker request and content snapshot identities do not match.");
        }

        ConfigValidator.Validate(request.Config);
        var languages = I18nOutputMerger.GetLanguages(request.Config.Site);
        if (!languages.Contains(request.Language, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Worker language '{request.Language}' is not in the configured language set.");
        }

        var buildLogger = new BuildDiagnosticLogger(logger);
        var resolved = ThemePathResolver.Resolve(request.RootDir, request.Config.Theme, buildLogger);
        var bootstrap = ThemeBootstrapper.BootstrapRequired(request.Config, request.RootDir, buildLogger);
        var templateResolver = new ThemeTemplateResolver(bootstrap.Manifest);
        var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(request.Config.Site, languages);
        var rootBaseUrl = BuildPathUtils.NormalizeBaseUrl(request.Config.Site.BaseUrl);
        var seoAlternates = SeoAlternatesService.BuildSeoAlternates(
            request.Config,
            snapshot.Documents,
            languages,
            defaultLanguage,
            rootBaseUrl,
            templateResolver,
            request.RootDir,
            resolved.LayoutsDir);
        var baseUrl = I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, request.Language);
        var variantConfig = request.Config with
        {
            Site = request.Config.Site with
            {
                Language = request.Language,
                BaseUrl = baseUrl
            }
        };
        var bodyStore = new DictionaryContentBodyStore(snapshot.Bodies);
        var contentGraph = CanonicalContentGraphBuilder.BuildFromDocuments(snapshot.Documents);
        var variantContext = new BuildVariantContext(
            variantConfig,
            request.RootDir,
            request.Overrides with
            {
                Output = request.OutputDir,
                CacheDir = request.CacheDir,
                Clean = false
            },
            I18nOutputMerger.FilterDocumentsByLanguage(snapshot.Documents, request.Language, defaultLanguage),
            contentGraph,
            bodyStore,
            request.OutputDir,
            baseUrl,
            resolved.LayoutsDir,
            request.AssetsDir,
            resolved.StaticDir,
            request.MediaDownloadDir,
            seoAlternates,
            rootBaseUrl,
            request.Language,
            defaultLanguage,
            snapshot.BuildStartedAt,
            resolved.ParentLayoutsDir,
            resolved.ParentAssetsDir,
            resolved.ParentStaticDir,
            resolved.UserLayoutsDir,
            request.ScssOutputDir,
            request.ProjectRootDir);

        buildLogger.Info($"event=build.variant.start language={request.Language} baseUrl={baseUrl} outputDir={request.OutputDir}");
        var result = await BuildVariantAsync(
            variantContext,
            new DirectoryHashCache(),
            cancellationToken,
            buildLogger).ConfigureAwait(false);
        buildLogger.Info($"event=build.variant.done language={request.Language} baseUrl={baseUrl} outputDir={request.OutputDir}");
        return (result, buildLogger.WarningCount, buildLogger.ErrorCount);
    }

    internal async Task<BuildResult> FinalizeI18nWorktreeBuildAsync(
        I18nPreparedBuild prepared,
        IReadOnlyList<BuildVariantResult> variants,
        int workerWarningCount,
        int workerErrorCount,
        CancellationToken cancellationToken)
    {
        var config = prepared.Plan.EffectiveConfig;
        var languages = I18nOutputMerger.GetLanguages(config.Site);
        if (variants.Count != languages.Count ||
            !variants.Select(x => x.Language).Order(StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(languages.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Worker results do not contain the complete configured language set.");
        }

        var completed = await FinalizeMultiLanguageOutputsAsync(
            config,
            prepared.RootDir,
            prepared.Overrides,
            prepared.Plan.OutputDir,
            BuildPathUtils.NormalizeBaseUrl(config.Site.BaseUrl),
            prepared.Content.Documents.Count,
            variants,
            prepared.Plan.StartedAt,
            prepared.Plan.Stopwatch,
            prepared.Content.BodyCacheMetrics,
            prepared.Content.SchemaErrors,
            prepared.Logger,
            workerWarningCount,
            workerErrorCount,
            prepared.ProjectRootDir,
            prepared.FinalOutputDir,
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        var rootManifestPath = PublicOutputLifecycle.ManifestPath(prepared.RootDir, prepared.Overrides);
        foreach (var variant in variants)
        {
            if (variant.PendingManifest is { } pending && pending.ManifestPath != rootManifestPath)
            {
                pending.Manifest.Save(pending.ManifestPath);
            }
        }
        completed.Manifest.Save(rootManifestPath);
        WriteOutputMarker(prepared.Plan.OutputDir);
        BuildRecoveryTracker.MarkCompleted(prepared.Plan.OutputDir);
        prepared.Logger.Info($"Build completed: {Path.GetFullPath(prepared.Plan.OutputDir)}");
        prepared.Logger.Info("event=build.done");
        return completed.Result;
    }

    private async Task<(BuildResult Result, BuildManifest Manifest)> FinalizeMultiLanguageOutputsAsync(
        AppConfig config,
        string rootDir,
        ConfigOverrides overrides,
        string outputDir,
        string rootBaseUrl,
        int documentCount,
        IReadOnlyList<BuildVariantResult> variantResults,
        DateTimeOffset buildStartedAt,
        Stopwatch buildStopwatch,
        BodyCacheMetrics? bodyCacheMetrics,
        IReadOnlyList<ContentValidationIssue> schemaErrors,
        BuildDiagnosticLogger buildLogger,
        int additionalWarningCount,
        int additionalErrorCount,
        string? reportedRootDir,
        string? reportedOutputDir,
        CancellationToken cancellationToken)
    {
        var previous = BuildManifest.Load(PublicOutputLifecycle.ManifestPath(rootDir, overrides));
        var rootOutputs = PublicOutputLifecycle.ProjectionPlan(config, [], root: true)
            .Where(item => item.Destination != "robots.txt" || previous.OwnedOutputs.Contains("robots.txt") ||
                !File.Exists(Path.Combine(outputDir, "robots.txt"))).ToArray();
        var completedManifest = PublicOutputLifecycle.CollectAndClean(rootDir, overrides, outputDir, variantResults, rootOutputs);
        if (rootOutputs.Any(x => x.Destination == "robots.txt") && PublicOutputLifecycle.SameRoot(previous, outputDir) && previous.OwnedOutputs.Contains("robots.txt"))
        {
            PublicOutputLifecycle.DeleteOwnedFile(outputDir, "robots.txt");
        }
        var projectionResults = I18nOutputMerger.GenerateRootOutputs(config, outputDir, rootBaseUrl, variantResults, buildLogger, _searchIndexBuilder);
        SeoAuditReportWriter.WriteMerged(config, outputDir, variantResults, buildLogger, projectionResults);
        var reportVariants = reportedOutputDir is null
            ? variantResults
            : variantResults.Select(variant => variant with
            {
                OutputDir = Path.Combine(
                    reportedOutputDir,
                    Path.GetRelativePath(outputDir, variant.OutputDir))
            }).ToArray();
        MetricsWriter.WriteIfRequested(reportedRootDir ?? rootDir, overrides.MetricsPath, config, reportedOutputDir ?? outputDir, documentCount, reportVariants, bodyCacheMetrics);
        var generatedFiles = BuildOutputInventory.Create(outputDir);
        buildStopwatch.Stop();
        var buildResult = BuildResultFactory.Create(
            config,
            reportedRootDir ?? rootDir,
            outputDir,
            overrides,
            buildStartedAt,
            DateTimeOffset.UtcNow,
            buildStopwatch.ElapsedMilliseconds,
            reportVariants,
            schemaErrors,
            generatedFiles,
            warningCount: buildLogger.WarningCount + additionalWarningCount,
            errorCount: buildLogger.ErrorCount + additionalErrorCount);
        var securityData = BuildReporter.CreateSecurityReportData(config, rootDir, outputDir, variantResults);
        await BuildReporter.WriteIfEnabledAsync(config, rootDir, outputDir, buildResult, variantResults, _logger, securityData, cancellationToken).ConfigureAwait(false);
        BuildReporter.EnforceSecurityGate(config, securityData, overrides.IsCI);
        return (buildResult, completedManifest);
    }

    internal static ValueTask DisposeContentBodyStoreAsync(IContentBodyStore bodyStore)
        => DisposeBodyStoreAsync(bodyStore);
}
