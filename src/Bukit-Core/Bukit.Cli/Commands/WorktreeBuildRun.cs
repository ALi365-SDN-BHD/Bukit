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
    private readonly BuildResourceLease _resourceLease;
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
    private BuildTransactionJournal? _journal;
    private string? _mediaSource;
    private string? _mediaFingerprint;
    private string? _mediaStageRoot;
    private string? _mediaFinalRoot;
    private string? _mediaBackupRoot;
    private string? _stagedMediaDir;

    private WorktreeBuildRun(
        AppConfig effectiveConfig,
        ConfigOverrides overrides,
        string logFormat,
        ILogger logger,
        GitRepository repository,
        string rootDir,
        string fullConfigPath,
        FileStream runLock,
        BuildResourceLease resourceLease,
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
        _resourceLease = resourceLease;
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
        var recoveryConfig = ConfigApplier.Apply(config, overrides);
        var recoveryOutput = PathUtils.GetCanonicalFullPath(BuildPathUtils.MakeAbsolute(resolved.RootDir, recoveryConfig.Build.Output));
        OutputDirectoryCleaner.EnsureNoSourceOverlap(
            OutputDirectoryCleaner.GetConfiguredContentSources(recoveryConfig, resolved.RootDir),
            BuildTransaction.ConfiguredWriteTargets(recoveryConfig, resolved.RootDir, overrides, recoveryOutput));
        BuildTransactionJournal.RejectOtherJournals(resolved.RootDir, recoveryOutput);
        if (BuildTransactionJournal.HasPending(resolved.RootDir, recoveryOutput))
        {
            var rootResult = await WorktreeBuildRepository.RunGitAsync(resolved.RootDir,
                ["rev-parse", "--show-toplevel"], logger, cancellationToken).ConfigureAwait(false);
            if (rootResult.ExitCode != 0 || string.IsNullOrWhiteSpace(rootResult.StandardOutput))
                throw new WorktreeBuildException("i18n-worktree-git-required", "Multilingual build requires the site to be inside a Git repository.");
            var repositoryRoot = Path.GetFullPath(rootResult.StandardOutput.Trim());
            await using var recoveryLock = WorktreeBuildPaths.AcquireRunLock(repositoryRoot);
            var targets = BuildTransaction.ConfiguredWriteTargets(recoveryConfig, resolved.RootDir, overrides, recoveryOutput);
            using var lease = BuildResourceLease.Acquire([.. targets.Select(path => new BuildResourceLease.Resource(path, true, true)),
                new BuildResourceLease.Resource(PathUtils.GetCanonicalFullPath(resolved.RootDir), true, true)]);
            BuildTransactionJournal.Recover(resolved.RootDir, recoveryOutput,
                [.. targets.SelectMany(path => new[] { (path, true), (path, false) })],
                WorktreeBuildPaths.RunsRoot(repositoryRoot),
                logger);
        }
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
        var finalCacheDir = PathUtils.GetCanonicalFullPath(string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(resolved.RootDir, ".cache")
            : Path.GetFullPath(overrides.CacheDir));
        WorktreeBuildPaths.EnsureDisjoint(finalOutputDir, finalCacheDir);
        // Preflight the original project's real sources against the formal targets before
        // the run lock, run directory, staging, or workers exist. Mapped worktree copies are
        // not a substitute for the original project sources.
        var writeTargets = BuildTransaction.ConfiguredWriteTargets(
            effectiveConfig, resolved.RootDir, overrides with { CacheDir = finalCacheDir }, finalOutputDir);
        OutputDirectoryCleaner.EnsureNoSourceOverlap(
            OutputDirectoryCleaner.GetConfiguredContentSources(effectiveConfig, resolved.RootDir), writeTargets);
        var layoutChanged = LayoutChanged(finalCacheDir, finalOutputDir, effectiveConfig.Site, logger);
        OutputDirectoryCleaner.EnsureCanClean(
            resolved.RootDir,
            finalOutputDir,
            effectiveConfig.Build.Clean || layoutChanged);

        var runId = Guid.NewGuid().ToString("N");
        var runsRoot = WorktreeBuildPaths.RunsRoot(repository.Root);
        var runLock = WorktreeBuildPaths.AcquireRunLock(repository.Root);
        BuildResourceLease? resourceLease = null;
        try
        {
            resourceLease = BuildResourceLease.Acquire([.. writeTargets.Select(path => new BuildResourceLease.Resource(path, true, true))]);
            await WorktreeBuildRepository.RecoverOwnedWorktreesAsync(repository.Root, runsRoot, logger, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            resourceLease?.Dispose();
            await runLock.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        try
        {
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
            resourceLease,
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
        catch
        {
            resourceLease.Dispose();
            await runLock.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async Task PrepareAsync(CancellationToken cancellationToken)
    {
        var mediaRelative = WorktreeMediaOverlay.ResolveRelativeMediaDir(_effectiveConfig, _rootDir, _repository);
        if (mediaRelative is not null)
        {
            _mediaSource = Path.Combine(_repository.Root, mediaRelative);
            _mediaFingerprint = WorktreeMediaOverlay.Fingerprint(_mediaSource);
        }
        _journal = BuildTransactionJournal.Create(_rootDir, _finalOutputDir,
        [
            new(PathUtils.GetCanonicalFullPath(_finalOutputDir), PathUtils.GetCanonicalFullPath(_stagingOutputDir), PathUtils.GetCanonicalFullPath(_backupOutputDir), true),
            new(PathUtils.GetCanonicalFullPath(_finalCacheDir), PathUtils.GetCanonicalFullPath(_stagingCacheDir), PathUtils.GetCanonicalFullPath(_backupCacheDir), true)
        ]);
        var preparedLayoutChanged = LayoutChanged(_finalCacheDir, _finalOutputDir, _effectiveConfig.Site, _logger);
        WorktreeBuildPaths.PrepareStaging(
            _finalOutputDir, _stagingOutputDir, copyExisting: !_effectiveConfig.Build.Clean && !preparedLayoutChanged);
        WorktreeBuildPaths.PrepareStaging(_finalCacheDir, _stagingCacheDir, copyExisting: true);
        if (preparedLayoutChanged)
        {
            WorktreeBuildPaths.DiscardManifests(_stagingCacheDir);
        }
        else
        {
            WorktreeBuildPaths.RewriteManifestRoots(_stagingCacheDir, _finalOutputDir, _stagingOutputDir, _languages, _effectiveConfig.Site);
        }

        var contentWorktree = Path.Combine(_runRoot, "content");
        await WorktreeBuildRepository.AddWorktreeAsync(
            _repository, contentWorktree, _registryPath, _ownedWorktrees, _logger, cancellationToken).ConfigureAwait(false);
        var contentSiteRoot = WorktreeBuildPaths.MapIntoWorktree(_repository, _rootDir, contentWorktree);
        if (_mediaSource is not null)
        {
            if (WorktreeBuildPaths.IsSameOrChild(_finalCacheDir, _mediaSource))
            {
                _stagedMediaDir = Path.Combine(_stagingCacheDir, Path.GetRelativePath(_finalCacheDir, _mediaSource));
            }
            else if (_effectiveConfig.Content.Media.DownloadToLocal)
            {
                WorktreeBuildPaths.EnsureDisjoint(_mediaSource, _finalOutputDir);
                WorktreeBuildPaths.EnsureDisjoint(_mediaSource, _finalCacheDir);
                _mediaFinalRoot = _mediaSource;
                while (!Directory.Exists(Path.GetDirectoryName(_mediaFinalRoot)!))
                    _mediaFinalRoot = Path.GetDirectoryName(_mediaFinalRoot)!;
                _mediaStageRoot = WorktreeBuildPaths.Sibling(_mediaFinalRoot, "bukit-staging-" + Guid.NewGuid().ToString("N"));
                _mediaBackupRoot = WorktreeBuildPaths.Sibling(_mediaFinalRoot, "bukit-backup-" + Guid.NewGuid().ToString("N"));
                _journal.Add(new(PathUtils.GetCanonicalFullPath(_mediaFinalRoot), PathUtils.GetCanonicalFullPath(_mediaStageRoot), PathUtils.GetCanonicalFullPath(_mediaBackupRoot), true));
                WorktreeBuildPaths.PrepareStaging(_mediaFinalRoot, _mediaStageRoot, copyExisting: true);
                _stagedMediaDir = Path.Combine(_mediaStageRoot, Path.GetRelativePath(_mediaFinalRoot, _mediaSource));
            }
            else
            {
                _stagedMediaDir = Path.Combine(_runRoot, "media-snapshot");
                WorktreeBuildPaths.PrepareStaging(_mediaSource, _stagedMediaDir, copyExisting: true);
            }
            if (WorktreeMediaOverlay.Fingerprint(_mediaSource) != _mediaFingerprint)
                throw new IOException($"Media directory changed while staging: {_mediaSource}");
        }
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
            cancellationToken,
            _stagedMediaDir).ConfigureAwait(false);

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
        await CreateWorkerPlansAsync(_prepared, snapshotPath, _contentHash, cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateWorkerPlansAsync(
        I18nPreparedBuild prepared,
        string snapshotPath,
        string contentHash,
        CancellationToken cancellationToken)
    {
        _workerPlans = new List<WorkerPlan>(_languages.Count);
        var layout = I18nOutputLayout.For(_effectiveConfig.Site);
        foreach (var language in _languages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segment = BuildPathUtils.SanitizeFileSegment(language);
            var workerWorktree = Path.Combine(_runRoot, "workers", segment);
            await WorktreeBuildRepository.AddWorktreeAsync(
                _repository, workerWorktree, _registryPath, _ownedWorktrees, _logger, cancellationToken).ConfigureAwait(false);
            var workerSiteRoot = WorktreeBuildPaths.MapIntoWorktree(_repository, _rootDir, workerWorktree);
            var workerConfigPath = WorktreeBuildPaths.MapIntoWorktree(_repository, _fullConfigPath, workerWorktree);
            var outputDir = layout.OutputDir(_stagingOutputDir, language);
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
                contentHash,
                language,
                _logFormat,
                workerConfigPath,
                workerSiteRoot,
                _rootDir,
                _effectiveConfig,
                _overrides with { Output = outputDir, CacheDir = workerCache, Clean = false, MetricsPath = null },
                outputDir,
                workerCache,
                prepared.AssetsDir,
                prepared.ScssOutputDir,
                _stagedMediaDir ?? prepared.Plan.MediaCacheDir,
                prepared.SeoAlternates,
                snapshotPath,
                resultPath);
            await I18nWorktreeProtocolStore.WriteRequestAsync(requestPath, request, cancellationToken).ConfigureAwait(false);
            _workerPlans.Add(new WorkerPlan(language, requestPath, resultPath, outputDir));
        }
    }

    private async Task RunWorkersAsync(I18nVariantSnapshot[] snapshots, int languageJobs, CancellationToken cancellationToken)
    {
        using var workersCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // After any failure the other workers stop early; the default language worker always runs
        // to completion so its root-cause failure can still be reported first.
        using var stopOtherWorkers = new CancellationTokenSource();
        var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(_effectiveConfig.Site, _languages);
        var failures = new System.Collections.Concurrent.ConcurrentDictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await Parallel.ForEachAsync(
                _workerPlans.Select((plan, index) => (plan, index)),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = languageJobs,
                    CancellationToken = workersCancellation.Token
                },
                async (item, parallelToken) =>
                {
                    using var workerCancellation = string.Equals(item.plan.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : CancellationTokenSource.CreateLinkedTokenSource(parallelToken, stopOtherWorkers.Token);
                    var ct = workerCancellation?.Token ?? parallelToken;
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        await WorktreeBuildCoordinator.RunWorkerProcessAsync(item.plan, _logger, ct).ConfigureAwait(false);
                        snapshots[item.index] = await I18nWorktreeProtocolStore.ReadResultAsync(item.plan.ResultPath, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!parallelToken.IsCancellationRequested && stopOtherWorkers.IsCancellationRequested)
                    {
                        // Stopped because another worker already failed.
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        failures[item.plan.Language] = ex is WorktreeBuildException
                            ? ex
                            : new WorktreeBuildException(
                                "i18n-worktree-result-invalid",
                                $"Worker result for language '{item.plan.Language}' is invalid: {ex.Message}",
                                ex);
                        await stopOtherWorkers.CancelAsync().ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
        }
        catch
        {
            await workersCancellation.CancelAsync().ConfigureAwait(false);
            throw;
        }

        ThrowIfWorkersFailed(failures);
    }

    private void ThrowIfWorkersFailed(System.Collections.Concurrent.ConcurrentDictionary<string, Exception> failures)
    {
        if (!failures.IsEmpty)
        {
            throw I18nReservedPrefixGuard.SelectPrimaryFailure(
                failures,
                I18nOutputMerger.GetDefaultLanguage(_effectiveConfig.Site, _languages));
        }
    }

    /// <summary>
    /// A layout switch is only known from a previous manifest; without one (fresh or cleared
    /// cache) the build is not a switch, matching the in-process migration check.
    /// </summary>
    // Mirrors BuildPlanner's migration rule: a Bukit-owned output (marker present) without a
    // root manifest is treated like a layout switch, while unowned output is left alone.
    private static bool LayoutChanged(string cacheDir, string outputDir, SiteConfig site, ILogger logger)
    {
        var manifestPath = Path.Combine(cacheDir, "build-manifest.json");
        if (!File.Exists(manifestPath))
        {
            return File.Exists(Path.Combine(outputDir, ".bukit-output-marker"));
        }

        var prior = BuildManifest.Load(manifestPath, logger);
        return !string.Equals(prior.Layout, I18nOutputLayout.ManifestLayout(site), StringComparison.Ordinal);
    }

    internal async Task ExecuteWorkersAsync(CancellationToken cancellationToken)
    {
        var snapshots = new I18nVariantSnapshot[_workerPlans.Count];
        var languageJobs = Math.Min(Math.Max(1, _effectiveConfig.Build.LanguageJobs), Environment.ProcessorCount);
        _logger.Info($"event=build.i18n.worktree.start head={_repository.Head} languages={_languages.Count} concurrent_jobs={languageJobs}");
        await RunWorkersAsync(snapshots, languageJobs, cancellationToken).ConfigureAwait(false);

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
                    manifestPath,
                    _effectiveConfig.Site);
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
        WorktreeBuildPaths.RewriteManifestRoots(_stagingCacheDir, _stagingOutputDir, _finalOutputDir, _languages, _effectiveConfig.Site);

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
        if (_mediaSource is not null && WorktreeMediaOverlay.Fingerprint(_mediaSource) != _mediaFingerprint)
            throw new IOException($"Media directory changed during build: {_mediaSource}");
        var metrics = _metrics ?? throw new InvalidOperationException("Worktree build metrics paths are unavailable.");
        var directories = new List<(string Staging, string Final, string Backup)>
        {
            (_stagingOutputDir, _finalOutputDir, _backupOutputDir),
            (_stagingCacheDir, _finalCacheDir, _backupCacheDir)
        };
        if (_mediaStageRoot is not null)
            directories.Add((_mediaStageRoot, _mediaFinalRoot!, _mediaBackupRoot!));
        WorktreeBuildTransaction.CommitDirectories(
            directories,
            metrics.ExternalCommit,
            (final, backup, hadOriginal) =>
            {
                if (_mediaSource is null || !WorktreeBuildPaths.IsSameOrChild(final, _mediaSource)) return;
                var backupMedia = Path.Combine(backup, Path.GetRelativePath(final, _mediaSource));
                var actual = hadOriginal ? WorktreeMediaOverlay.Fingerprint(backupMedia) : "missing";
                if (actual != _mediaFingerprint)
                    throw new IOException($"Media directory changed during commit: {_mediaSource}");
            },
            _rootDir,
            _journal);
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
                if (_mediaStageRoot is not null) WorktreeBuildPaths.TryDeleteDirectory(_mediaStageRoot);
                if (_journal is { IsUnstarted: true } && !Directory.Exists(_stagingOutputDir) &&
                    !Directory.Exists(_stagingCacheDir) && (_mediaStageRoot is null || !Directory.Exists(_mediaStageRoot)))
                    _journal.Remove();
            }
            if (_worktreesCleaned)
            {
                WorktreeBuildPaths.TryDeleteDirectory(_runRoot);
            }
        }
        finally
        {
            try { _resourceLease.Dispose(); }
            finally { await _runLock.DisposeAsync().ConfigureAwait(false); }
        }
    }
}
