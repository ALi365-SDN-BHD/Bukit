using Bukit.Cli.Shared;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Incremental;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal sealed record WorkerPlan(string Language, string RequestPath, string ResultPath, string OutputDir);

/// <summary>
/// Holds the mutable state of one multilingual Worktree build run so the
/// coordinator can express prepare, execute and finalize as separate phases.
/// </summary>
internal sealed class WorktreeBuildRun
{
    private readonly AppConfig _effectiveConfig;
    private readonly ConfigOverrides _overrides;
    private readonly string _logFormat;
    private readonly ILogger _logger;
    private readonly GitRepository _repository;
    private readonly string _rootDir;
    private readonly string _fullConfigPath;
    private readonly FileStream _runLock;
    private readonly string _runRoot;
    private readonly string _registryPath;
    private readonly string _stagingOutputDir;
    private readonly string _stagingCacheDir;
    private readonly string _backupOutputDir;
    private readonly string _backupCacheDir;
    private readonly string _finalOutputDir;
    private readonly string _finalCacheDir;
    private readonly IReadOnlyList<string> _languages;
    private readonly string _configHash;
    private readonly SiteEngine _engine;
    private readonly List<string> _ownedWorktrees = [];
    private List<WorkerPlan> _workerPlans = [];
    private MetricsPaths? _metrics;
    private I18nPreparedBuild? _prepared;
    private BuildVariantResult[] _variants = [];
    private string? _contentHash;
    private int _warningCount;
    private int _errorCount;
    private bool _worktreesCleaned;
    private bool _committed;

    private WorktreeBuildRun(
        AppConfig effectiveConfig,
        ConfigOverrides overrides,
        string logFormat,
        ILogger logger,
        GitRepository repository,
        string rootDir,
        string fullConfigPath,
        FileStream runLock,
        string runRoot,
        string registryPath,
        string finalOutputDir,
        string finalCacheDir,
        string stagingOutputDir,
        string stagingCacheDir,
        string backupOutputDir,
        string backupCacheDir,
        IReadOnlyList<string> languages,
        string configHash,
        SiteEngine engine)
    {
        _effectiveConfig = effectiveConfig;
        _overrides = overrides;
        _logFormat = logFormat;
        _logger = logger;
        _repository = repository;
        _rootDir = rootDir;
        _fullConfigPath = fullConfigPath;
        _runLock = runLock;
        _runRoot = runRoot;
        _registryPath = registryPath;
        _finalOutputDir = finalOutputDir;
        _finalCacheDir = finalCacheDir;
        _stagingOutputDir = stagingOutputDir;
        _stagingCacheDir = stagingCacheDir;
        _backupOutputDir = backupOutputDir;
        _backupCacheDir = backupCacheDir;
        _languages = languages;
        _configHash = configHash;
        _engine = engine;
    }

    internal static async Task<WorktreeBuildRun> StartAsync(
        AppConfig config,
        ResolvedConfigPath resolved,
        ConfigOverrides overrides,
        string logFormat,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        GitRepository repository;
        try
        {
            repository = await WorktreeBuildRepository.ValidateRepositoryAsync(
                config,
                resolved,
                overrides,
                logger,
                cancellationToken).ConfigureAwait(false);
        }
        catch (WorktreeBuildException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new WorktreeBuildException("i18n-worktree-git-required", $"Multilingual build requires Git: {ex.Message}", ex);
        }

        var effectiveConfig = ConfigApplier.Apply(config, overrides);
        var configHash = I18nWorktreeProtocol.ComputeConfigHash(effectiveConfig, overrides);
        var languages = I18nOutputMerger.GetLanguages(effectiveConfig.Site);
        var finalOutputDir = BuildPathUtils.MakeAbsolute(resolved.RootDir, effectiveConfig.Build.Output);
        var finalCacheDir = string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(resolved.RootDir, ".cache")
            : Path.GetFullPath(overrides.CacheDir);
        WorktreeBuildPaths.EnsureDisjoint(finalOutputDir, finalCacheDir);
        OutputDirectoryCleaner.EnsureCanClean(resolved.RootDir, finalOutputDir, effectiveConfig.Build.Clean);

        var runId = Guid.NewGuid().ToString("N");
        var runsRoot = WorktreeBuildPaths.RunsRoot(repository.Root);
        var runLock = WorktreeBuildPaths.AcquireRunLock(repository.Root);
        try
        {
            await WorktreeBuildRepository.RecoverOwnedWorktreesAsync(repository.Root, runsRoot, logger, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await runLock.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        var runRoot = Path.Combine(runsRoot, runId);
        Directory.CreateDirectory(runRoot);
        WorktreeBuildPaths.RestrictDirectory(runRoot);

        return new WorktreeBuildRun(
            effectiveConfig,
            overrides,
            logFormat,
            logger,
            repository,
            resolved.RootDir,
            resolved.FullConfigPath,
            runLock,
            runRoot,
            Path.Combine(runRoot, "owned-worktrees.txt"),
            finalOutputDir,
            finalCacheDir,
            WorktreeBuildPaths.Sibling(finalOutputDir, $"bukit-staging-{runId}"),
            WorktreeBuildPaths.Sibling(finalCacheDir, $"bukit-staging-{runId}"),
            WorktreeBuildPaths.Sibling(finalOutputDir, $"bukit-backup-{runId}"),
            WorktreeBuildPaths.Sibling(finalCacheDir, $"bukit-backup-{runId}"),
            languages,
            configHash,
            new SiteEngine(logger));
    }

    internal async Task PrepareAsync(CancellationToken cancellationToken)
    {
        WorktreeBuildPaths.PrepareStaging(_finalOutputDir, _stagingOutputDir, copyExisting: !_effectiveConfig.Build.Clean);
        WorktreeBuildPaths.PrepareStaging(_finalCacheDir, _stagingCacheDir, copyExisting: true);
        WorktreeBuildPaths.RewriteManifestRoots(_stagingCacheDir, _finalOutputDir, _stagingOutputDir, _languages);

        var contentWorktree = Path.Combine(_runRoot, "content");
        await WorktreeBuildRepository.AddWorktreeAsync(
            _repository, contentWorktree, _registryPath, _ownedWorktrees, _logger, cancellationToken).ConfigureAwait(false);
        var contentSiteRoot = WorktreeBuildPaths.MapIntoWorktree(_repository, _rootDir, contentWorktree);
        _metrics = WorktreeBuildPaths.ResolveMetricsPaths(
            _overrides.MetricsPath, _rootDir, _finalOutputDir, _stagingOutputDir, _runRoot);
        var stagingOverrides = _overrides with
        {
            Output = _stagingOutputDir,
            CacheDir = _stagingCacheDir,
            Clean = false,
            MetricsPath = _metrics.StagingPath
        };
        _prepared = await _engine.PrepareI18nWorktreeBuildAsync(
            _effectiveConfig,
            contentSiteRoot,
            _rootDir,
            _finalOutputDir,
            stagingOverrides,
            cancellationToken).ConfigureAwait(false);

        var contentSnapshot = await I18nWorktreeProtocol.CaptureContentAsync(
            _repository.Head,
            _prepared.Plan.StartedAt,
            _prepared.Content,
            contentSiteRoot,
            _rootDir,
            cancellationToken).ConfigureAwait(false);
        var snapshotPath = Path.Combine(_runRoot, "content-snapshot.v1.json");
        await I18nWorktreeProtocolStore.WriteContentAsync(snapshotPath, contentSnapshot, cancellationToken).ConfigureAwait(false);
        _contentHash = await I18nWorktreeProtocolStore.Sha256Async(snapshotPath, cancellationToken).ConfigureAwait(false);

        _workerPlans = new List<WorkerPlan>(_languages.Count);
        foreach (var language in _languages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segment = BuildPathUtils.SanitizeFileSegment(language);
            var workerWorktree = Path.Combine(_runRoot, "workers", segment);
            await WorktreeBuildRepository.AddWorktreeAsync(
                _repository, workerWorktree, _registryPath, _ownedWorktrees, _logger, cancellationToken).ConfigureAwait(false);
            var workerSiteRoot = WorktreeBuildPaths.MapIntoWorktree(_repository, _rootDir, workerWorktree);
            var workerConfigPath = WorktreeBuildPaths.MapIntoWorktree(_repository, _fullConfigPath, workerWorktree);
            var outputDir = Path.Combine(_stagingOutputDir, language);
            var workerCache = Path.Combine(_runRoot, "worker-cache", segment);
            Directory.CreateDirectory(workerCache);
            WorktreeBuildPaths.CopyVariantManifest(_stagingCacheDir, workerCache, language, outputDir);
            var protocolDir = Path.Combine(_runRoot, "protocol", segment);
            var requestPath = Path.Combine(protocolDir, "request.v1.json");
            var resultPath = Path.Combine(protocolDir, "result.v1.json");
            var request = new I18nWorkerRequest(
                I18nWorktreeProtocol.RequestSchema,
                _repository.Head,
                _configHash,
                _contentHash,
                language,
                _logFormat,
                workerConfigPath,
                workerSiteRoot,
                _rootDir,
                _effectiveConfig,
                _overrides with { Output = outputDir, CacheDir = workerCache, Clean = false, MetricsPath = null },
                outputDir,
                workerCache,
                _prepared.AssetsDir,
                _prepared.ScssOutputDir,
                _prepared.Plan.MediaCacheDir,
                snapshotPath,
                resultPath);
            await I18nWorktreeProtocolStore.WriteRequestAsync(requestPath, request, cancellationToken).ConfigureAwait(false);
            _workerPlans.Add(new WorkerPlan(language, requestPath, resultPath, outputDir));
        }
    }

    internal async Task ExecuteWorkersAsync(CancellationToken cancellationToken)
    {
        var snapshots = new I18nVariantSnapshot[_workerPlans.Count];
        var languageJobs = Math.Min(Math.Max(1, _effectiveConfig.Build.LanguageJobs), Environment.ProcessorCount);
        _logger.Info($"event=build.i18n.worktree.start head={_repository.Head} languages={_languages.Count} concurrent_jobs={languageJobs}");
        using var workersCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await Parallel.ForEachAsync(
                _workerPlans.Select((plan, index) => (plan, index)),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = languageJobs,
                    CancellationToken = workersCancellation.Token
                },
                async (item, ct) =>
                {
                    await WorktreeBuildCoordinator.RunWorkerProcessAsync(item.plan, _logger, ct).ConfigureAwait(false);
                    try
                    {
                        snapshots[item.index] = await I18nWorktreeProtocolStore.ReadResultAsync(item.plan.ResultPath, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw new WorktreeBuildException(
                            "i18n-worktree-result-invalid",
                            $"Worker result for language '{item.plan.Language}' is invalid: {ex.Message}",
                            ex);
                    }
                }).ConfigureAwait(false);
        }
        catch
        {
            await workersCancellation.CancelAsync().ConfigureAwait(false);
            throw;
        }

        var prepared = _prepared ?? throw new InvalidOperationException("Worktree build prepare phase has not completed.");
        var contentGraph = prepared.Content.ContentGraph
            ?? CanonicalContentGraphBuilder.BuildFromDocuments(prepared.Content.Documents);
        _variants = new BuildVariantResult[_workerPlans.Count];
        _warningCount = 0;
        _errorCount = 0;
        for (var index = 0; index < _workerPlans.Count; index++)
        {
            var plan = _workerPlans[index];
            try
            {
                var manifestPath = Path.Combine(
                    _stagingCacheDir,
                    $"build-manifest.{BuildPathUtils.SanitizeFileSegment(plan.Language)}.json");
                _variants[index] = I18nWorktreeProtocol.RestoreVariant(
                    snapshots[index],
                    contentGraph,
                    _repository.Head,
                    _configHash,
                    _contentHash ?? throw new InvalidOperationException("Content snapshot hash is unavailable."),
                    plan.Language,
                    plan.OutputDir,
                    manifestPath);
                _warningCount += snapshots[index].WarningCount;
                _errorCount += snapshots[index].ErrorCount;
            }
            catch (WorktreeBuildException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new WorktreeBuildException(
                    "i18n-worktree-result-invalid",
                    $"Worker result for language '{plan.Language}' failed validation: {ex.Message}",
                    ex);
            }
        }
    }

    internal async Task FinalizeAsync(CancellationToken cancellationToken)
    {
        var prepared = _prepared ?? throw new InvalidOperationException("Worktree build prepare phase has not completed.");
        _ = await _engine.FinalizeI18nWorktreeBuildAsync(
            prepared,
            _variants,
            _warningCount,
            _errorCount,
            cancellationToken).ConfigureAwait(false);
        WorktreeBuildPaths.RewriteManifestRoots(_stagingCacheDir, _stagingOutputDir, _finalOutputDir, _languages);

        await prepared.DisposeAsync().ConfigureAwait(false);
        _prepared = null;
        try
        {
            await WorktreeBuildRepository.CleanupOwnedWorktreesAsync(
                _repository.Root, _ownedWorktrees, _registryPath, _logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorktreeBuildException(
                "i18n-worktree-cleanup-failed",
                $"Failed to remove owned multilingual build Worktrees: {ex.Message}",
                ex);
        }
        _worktreesCleaned = true;
        var metrics = _metrics ?? throw new InvalidOperationException("Worktree build metrics paths are unavailable.");
        WorktreeBuildTransaction.CommitDirectories(
            [(_stagingOutputDir, _finalOutputDir, _backupOutputDir), (_stagingCacheDir, _finalCacheDir, _backupCacheDir)],
            metrics.ExternalCommit);
        _committed = true;
        WorktreeBuildPaths.TryDeleteDirectory(_runRoot);
        _logger.Info($"event=build.i18n.worktree.done head={_repository.Head} languages={_languages.Count}");
    }

    internal async Task CleanupAsync()
    {
        try
        {
            if (_prepared is not null)
            {
                await _prepared.DisposeAsync().ConfigureAwait(false);
            }
            if (!_worktreesCleaned)
            {
                try
                {
                    await WorktreeBuildRepository.CleanupOwnedWorktreesAsync(
                        _repository.Root, _ownedWorktrees, _registryPath, _logger, CancellationToken.None).ConfigureAwait(false);
                    _worktreesCleaned = true;
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"[i18n-worktree-cleanup-failed] {ex.Message}").ConfigureAwait(false);
                }
            }
            if (!_committed)
            {
                WorktreeBuildPaths.TryDeleteDirectory(_stagingOutputDir);
                WorktreeBuildPaths.TryDeleteDirectory(_stagingCacheDir);
            }
            if (_worktreesCleaned)
            {
                WorktreeBuildPaths.TryDeleteDirectory(_runRoot);
            }
        }
        finally
        {
            await _runLock.DisposeAsync().ConfigureAwait(false);
        }
    }
}
