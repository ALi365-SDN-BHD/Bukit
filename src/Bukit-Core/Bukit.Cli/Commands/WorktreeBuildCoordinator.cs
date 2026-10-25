using Bukit.Cli.Shared;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Incremental;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal sealed class WorktreeBuildException(string code, string message, Exception? innerException = null)
    : Exception($"[{code}] {message}", innerException)
{
    internal string Code { get; } = code;
}

internal static class WorktreeBuildCoordinator
{
    private const string WorkerEnvironmentVariable = "BUKIT_INTERNAL_I18N_WORKER_REQUEST";
    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromHours(1);

    internal static bool IsWorker => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(WorkerEnvironmentVariable));

    internal static async Task<int> RunWorkerAsync(CancellationToken cancellationToken)
    {
        var requestPath = Environment.GetEnvironmentVariable(WorkerEnvironmentVariable)
            ?? throw new WorktreeBuildException("i18n-worktree-result-invalid", "Internal worker request path is missing.");
        try
        {
            var request = await I18nWorktreeProtocol.ReadRequestAsync(requestPath, cancellationToken).ConfigureAwait(false);
            var snapshot = await I18nWorktreeProtocol.ReadContentAsync(request.SnapshotPath, cancellationToken).ConfigureAwait(false);
            var contentHash = await I18nWorktreeProtocol.Sha256Async(request.SnapshotPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(request.ContentHash, contentHash, StringComparison.Ordinal) ||
                !string.Equals(request.ConfigHash, I18nWorktreeProtocol.ComputeConfigHash(request.Config, request.Overrides), StringComparison.Ordinal) ||
                !string.Equals(request.Head, snapshot.Head, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Content snapshot hash or HEAD does not match the worker request.");
            }

            var level = request.Overrides.IsCI
                ? LogLevel.Warn
                : ParseLogLevel(request.Config.Logging.Level);
            var logger = new ConsoleLogger(level, request.LogFormat);
            var engine = new SiteEngine(logger);
            var completed = await engine.ExecuteI18nWorktreeVariantAsync(
                request,
                snapshot,
                logger,
                cancellationToken).ConfigureAwait(false);
            var result = await I18nWorktreeProtocol.CaptureVariantAsync(
                request.Head,
                request.ConfigHash,
                request.ContentHash,
                completed.Result,
                completed.WarningCount,
                completed.ErrorCount,
                cancellationToken).ConfigureAwait(false);
            await I18nWorktreeProtocol.WriteResultAsync(request.ResultPath, result, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex) when (ex is not WorktreeBuildException)
        {
            throw new WorktreeBuildException(
                "i18n-worktree-worker-failed",
                $"Internal multilingual build worker failed: {ex.Message}",
                ex);
        }
    }

    internal static async Task<int> RunAsync(
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
        using var runLock = WorktreeBuildPaths.AcquireRunLock(repository.Root);
        await WorktreeBuildRepository.RecoverOwnedWorktreesAsync(repository.Root, runsRoot, logger, cancellationToken).ConfigureAwait(false);

        var runRoot = Path.Combine(runsRoot, runId);
        Directory.CreateDirectory(runRoot);
        WorktreeBuildPaths.RestrictDirectory(runRoot);
        var registryPath = Path.Combine(runRoot, "owned-worktrees.txt");
        var stagingOutputDir = WorktreeBuildPaths.Sibling(finalOutputDir, $"bukit-staging-{runId}");
        var stagingCacheDir = WorktreeBuildPaths.Sibling(finalCacheDir, $"bukit-staging-{runId}");
        var backupOutputDir = WorktreeBuildPaths.Sibling(finalOutputDir, $"bukit-backup-{runId}");
        var backupCacheDir = WorktreeBuildPaths.Sibling(finalCacheDir, $"bukit-backup-{runId}");
        var ownedWorktrees = new List<string>();
        I18nPreparedBuild? prepared = null;
        var worktreesCleaned = false;
        var committed = false;

        try
        {
            WorktreeBuildPaths.PrepareStaging(finalOutputDir, stagingOutputDir, copyExisting: !effectiveConfig.Build.Clean);
            WorktreeBuildPaths.PrepareStaging(finalCacheDir, stagingCacheDir, copyExisting: true);
            WorktreeBuildPaths.RewriteManifestRoots(stagingCacheDir, finalOutputDir, stagingOutputDir, languages);

            var contentWorktree = Path.Combine(runRoot, "content");
            await WorktreeBuildRepository.AddWorktreeAsync(repository, contentWorktree, registryPath, ownedWorktrees, logger, cancellationToken).ConfigureAwait(false);
            var contentSiteRoot = WorktreeBuildPaths.MapIntoWorktree(repository, resolved.RootDir, contentWorktree);
            var metrics = WorktreeBuildPaths.ResolveMetricsPaths(overrides.MetricsPath, resolved.RootDir, finalOutputDir, stagingOutputDir, runRoot);
            var stagingOverrides = overrides with
            {
                Output = stagingOutputDir,
                CacheDir = stagingCacheDir,
                Clean = false,
                MetricsPath = metrics.StagingPath
            };
            var engine = new SiteEngine(logger);
            prepared = await engine.PrepareI18nWorktreeBuildAsync(
                effectiveConfig,
                contentSiteRoot,
                resolved.RootDir,
                finalOutputDir,
                stagingOverrides,
                cancellationToken).ConfigureAwait(false);

            var contentSnapshot = await I18nWorktreeProtocol.CaptureContentAsync(
                repository.Head,
                prepared.Plan.StartedAt,
                prepared.Content,
                contentSiteRoot,
                resolved.RootDir,
                cancellationToken).ConfigureAwait(false);
            var snapshotPath = Path.Combine(runRoot, "content-snapshot.v1.json");
            await I18nWorktreeProtocol.WriteContentAsync(snapshotPath, contentSnapshot, cancellationToken).ConfigureAwait(false);
            var contentHash = await I18nWorktreeProtocol.Sha256Async(snapshotPath, cancellationToken).ConfigureAwait(false);

            var workerPlans = new List<WorkerPlan>(languages.Count);
            foreach (var language in languages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var segment = BuildPathUtils.SanitizeFileSegment(language);
                var workerWorktree = Path.Combine(runRoot, "workers", segment);
                await WorktreeBuildRepository.AddWorktreeAsync(repository, workerWorktree, registryPath, ownedWorktrees, logger, cancellationToken).ConfigureAwait(false);
                var workerSiteRoot = WorktreeBuildPaths.MapIntoWorktree(repository, resolved.RootDir, workerWorktree);
                var workerConfigPath = WorktreeBuildPaths.MapIntoWorktree(repository, resolved.FullConfigPath, workerWorktree);
                var outputDir = Path.Combine(stagingOutputDir, language);
                var workerCache = Path.Combine(runRoot, "worker-cache", segment);
                Directory.CreateDirectory(workerCache);
                WorktreeBuildPaths.CopyVariantManifest(stagingCacheDir, workerCache, language, outputDir);
                var protocolDir = Path.Combine(runRoot, "protocol", segment);
                var requestPath = Path.Combine(protocolDir, "request.v1.json");
                var resultPath = Path.Combine(protocolDir, "result.v1.json");
                var request = new I18nWorkerRequest(
                    I18nWorktreeProtocol.RequestSchema,
                    repository.Head,
                    configHash,
                    contentHash,
                    language,
                    logFormat,
                    workerConfigPath,
                    workerSiteRoot,
                    resolved.RootDir,
                    effectiveConfig,
                    overrides with { Output = outputDir, CacheDir = workerCache, Clean = false, MetricsPath = null },
                    outputDir,
                    workerCache,
                    prepared.AssetsDir,
                    prepared.ScssOutputDir,
                    prepared.Plan.MediaCacheDir,
                    snapshotPath,
                    resultPath);
                await I18nWorktreeProtocol.WriteRequestAsync(requestPath, request, cancellationToken).ConfigureAwait(false);
                workerPlans.Add(new WorkerPlan(language, requestPath, resultPath, outputDir));
            }

            var snapshots = new I18nVariantSnapshot[workerPlans.Count];
            var languageJobs = Math.Min(Math.Max(1, effectiveConfig.Build.LanguageJobs), Environment.ProcessorCount);
            logger.Info($"event=build.i18n.worktree.start head={repository.Head} languages={languages.Count} concurrent_jobs={languageJobs}");
            using var workersCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await Parallel.ForEachAsync(
                    workerPlans.Select((plan, index) => (plan, index)),
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = languageJobs,
                        CancellationToken = workersCancellation.Token
                    },
                    async (item, ct) =>
                    {
                        await RunWorkerProcessAsync(item.plan, logger, ct).ConfigureAwait(false);
                        try
                        {
                            snapshots[item.index] = await I18nWorktreeProtocol.ReadResultAsync(item.plan.ResultPath, ct).ConfigureAwait(false);
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
                workersCancellation.Cancel();
                throw;
            }

            var contentGraph = prepared.Content.ContentGraph
                ?? CanonicalContentGraphBuilder.BuildFromDocuments(prepared.Content.Documents);
            var variants = new BuildVariantResult[workerPlans.Count];
            var warningCount = 0;
            var errorCount = 0;
            for (var index = 0; index < workerPlans.Count; index++)
            {
                var plan = workerPlans[index];
                try
                {
                    var manifestPath = Path.Combine(stagingCacheDir, $"build-manifest.{BuildPathUtils.SanitizeFileSegment(plan.Language)}.json");
                    variants[index] = I18nWorktreeProtocol.RestoreVariant(
                        snapshots[index],
                        contentGraph,
                        repository.Head,
                        configHash,
                        contentHash,
                        plan.Language,
                        plan.OutputDir,
                        manifestPath);
                    warningCount += snapshots[index].WarningCount;
                    errorCount += snapshots[index].ErrorCount;
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

            _ = await engine.FinalizeI18nWorktreeBuildAsync(
                prepared,
                variants,
                warningCount,
                errorCount,
                cancellationToken).ConfigureAwait(false);
            WorktreeBuildPaths.RewriteManifestRoots(stagingCacheDir, stagingOutputDir, finalOutputDir, languages);

            await prepared.DisposeAsync().ConfigureAwait(false);
            prepared = null;
            try
            {
                await WorktreeBuildRepository.CleanupOwnedWorktreesAsync(repository.Root, ownedWorktrees, registryPath, logger, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new WorktreeBuildException(
                    "i18n-worktree-cleanup-failed",
                    $"Failed to remove owned multilingual build Worktrees: {ex.Message}",
                    ex);
            }
            worktreesCleaned = true;
            WorktreeBuildTransaction.CommitDirectories(
                [(stagingOutputDir, finalOutputDir, backupOutputDir), (stagingCacheDir, finalCacheDir, backupCacheDir)],
                metrics.ExternalCommit);
            committed = true;
            WorktreeBuildPaths.TryDeleteDirectory(runRoot);
            logger.Info($"event=build.i18n.worktree.done head={repository.Head} languages={languages.Count}");
            return 0;
        }
        catch (WorktreeBuildException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new WorktreeBuildException("i18n-worktree-worker-failed", $"Multilingual Worktree build failed: {ex.Message}", ex);
        }
        finally
        {
            if (prepared is not null)
            {
                await prepared.DisposeAsync().ConfigureAwait(false);
            }
            if (!worktreesCleaned)
            {
                try
                {
                    await WorktreeBuildRepository.CleanupOwnedWorktreesAsync(repository.Root, ownedWorktrees, registryPath, logger, CancellationToken.None).ConfigureAwait(false);
                    worktreesCleaned = true;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[i18n-worktree-cleanup-failed] {ex.Message}");
                }
            }
            if (!committed)
            {
                WorktreeBuildPaths.TryDeleteDirectory(stagingOutputDir);
                WorktreeBuildPaths.TryDeleteDirectory(stagingCacheDir);
            }
            if (worktreesCleaned)
            {
                WorktreeBuildPaths.TryDeleteDirectory(runRoot);
            }
        }
    }

    private static async Task RunWorkerProcessAsync(WorkerPlan plan, ILogger logger, CancellationToken cancellationToken)
    {
        var invocation = SelfInvocation();
        var startInfo = WorktreeBuildRepository.NewProcessStartInfo(invocation.FileName);
        foreach (var argument in invocation.PrefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.ArgumentList.Add("build");
        startInfo.Environment[WorkerEnvironmentVariable] = plan.RequestPath;
        var result = await I18nWorktreeGitRunner.RunAsync(startInfo, WorkerTimeout, logger, cancellationToken).ConfigureAwait(false);
        if (result.StandardOutput.Length > 0)
        {
            Console.Out.Write(result.StandardOutput);
        }
        if (result.StandardError.Length > 0)
        {
            Console.Error.Write(result.StandardError);
        }
        if (result.ExitCode != 0)
        {
            throw new WorktreeBuildException(
                "i18n-worktree-worker-failed",
                $"Worker for language '{plan.Language}' exited with code {result.ExitCode}.");
        }
    }

    private static (string FileName, IReadOnlyList<string> PrefixArguments) SelfInvocation()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Current Bukit executable path is unavailable.");
        if (!string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return (processPath, []);
        }
        var assemblyPath = Environment.GetCommandLineArgs().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new InvalidOperationException("Current Bukit managed entry assembly path is unavailable.");
        }
        return (processPath, [assemblyPath]);
    }

    private static LogLevel ParseLogLevel(string? level)
        => (level ?? "info").Trim().ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "warn" => LogLevel.Warn,
            "error" => LogLevel.Error,
            _ => LogLevel.Info
        };

    private sealed record WorkerPlan(string Language, string RequestPath, string ResultPath, string OutputDir);
}
