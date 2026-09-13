using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);
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
            repository = await ValidateRepositoryAsync(
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
        EnsureDisjoint(finalOutputDir, finalCacheDir);
        OutputDirectoryCleaner.EnsureCanClean(resolved.RootDir, finalOutputDir, effectiveConfig.Build.Clean);

        var runId = Guid.NewGuid().ToString("N");
        var runsRoot = RunsRoot(repository.Root);
        using var runLock = AcquireRunLock(repository.Root);
        await RecoverOwnedWorktreesAsync(repository.Root, runsRoot, logger, cancellationToken).ConfigureAwait(false);

        var runRoot = Path.Combine(runsRoot, runId);
        Directory.CreateDirectory(runRoot);
        RestrictDirectory(runRoot);
        var registryPath = Path.Combine(runRoot, "owned-worktrees.txt");
        var stagingOutputDir = Sibling(finalOutputDir, $"bukit-staging-{runId}");
        var stagingCacheDir = Sibling(finalCacheDir, $"bukit-staging-{runId}");
        var backupOutputDir = Sibling(finalOutputDir, $"bukit-backup-{runId}");
        var backupCacheDir = Sibling(finalCacheDir, $"bukit-backup-{runId}");
        var ownedWorktrees = new List<string>();
        I18nPreparedBuild? prepared = null;
        var worktreesCleaned = false;
        var committed = false;

        try
        {
            PrepareStaging(finalOutputDir, stagingOutputDir, copyExisting: !effectiveConfig.Build.Clean);
            PrepareStaging(finalCacheDir, stagingCacheDir, copyExisting: true);
            RewriteManifestRoots(stagingCacheDir, finalOutputDir, stagingOutputDir, languages);

            var contentWorktree = Path.Combine(runRoot, "content");
            await AddWorktreeAsync(repository, contentWorktree, registryPath, ownedWorktrees, cancellationToken).ConfigureAwait(false);
            var contentSiteRoot = MapIntoWorktree(repository, resolved.RootDir, contentWorktree);
            var metrics = ResolveMetricsPaths(overrides.MetricsPath, resolved.RootDir, finalOutputDir, stagingOutputDir, runRoot);
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
                await AddWorktreeAsync(repository, workerWorktree, registryPath, ownedWorktrees, cancellationToken).ConfigureAwait(false);
                var workerSiteRoot = MapIntoWorktree(repository, resolved.RootDir, workerWorktree);
                var workerConfigPath = MapIntoWorktree(repository, resolved.FullConfigPath, workerWorktree);
                var outputDir = Path.Combine(stagingOutputDir, language);
                var workerCache = Path.Combine(runRoot, "worker-cache", segment);
                Directory.CreateDirectory(workerCache);
                CopyVariantManifest(stagingCacheDir, workerCache, language, outputDir);
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
                    prepared.Assets.AssetsDir,
                    prepared.Assets.ScssOutputDir,
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
                        await RunWorkerProcessAsync(item.plan, ct).ConfigureAwait(false);
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
            RewriteManifestRoots(stagingCacheDir, stagingOutputDir, finalOutputDir, languages);

            await prepared.DisposeAsync().ConfigureAwait(false);
            prepared = null;
            try
            {
                await CleanupOwnedWorktreesAsync(repository.Root, ownedWorktrees, registryPath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new WorktreeBuildException(
                    "i18n-worktree-cleanup-failed",
                    $"Failed to remove owned multilingual build Worktrees: {ex.Message}",
                    ex);
            }
            worktreesCleaned = true;
            CommitDirectories(
                [(stagingOutputDir, finalOutputDir, backupOutputDir), (stagingCacheDir, finalCacheDir, backupCacheDir)],
                metrics.ExternalCommit);
            committed = true;
            TryDeleteDirectory(runRoot);
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
                    await CleanupOwnedWorktreesAsync(repository.Root, ownedWorktrees, registryPath, CancellationToken.None).ConfigureAwait(false);
                    worktreesCleaned = true;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[i18n-worktree-cleanup-failed] {ex.Message}");
                }
            }
            if (!committed)
            {
                TryDeleteDirectory(stagingOutputDir);
                TryDeleteDirectory(stagingCacheDir);
            }
            if (worktreesCleaned)
            {
                TryDeleteDirectory(runRoot);
            }
        }
    }

    private static async Task<GitRepository> ValidateRepositoryAsync(
        AppConfig config,
        ResolvedConfigPath resolved,
        ConfigOverrides overrides,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var rootResult = await RunGitAsync(resolved.RootDir, ["rev-parse", "--show-toplevel"], cancellationToken).ConfigureAwait(false);
        if (rootResult.ExitCode != 0 || string.IsNullOrWhiteSpace(rootResult.StandardOutput))
        {
            throw new WorktreeBuildException("i18n-worktree-git-required", "Multilingual build requires the site to be inside a Git repository.");
        }
        var repositoryRoot = Path.GetFullPath(rootResult.StandardOutput.Trim());
        var head = await RequireGitOutputAsync(repositoryRoot, ["rev-parse", "--verify", "HEAD"], "i18n-worktree-git-required", cancellationToken).ConfigureAwait(false);
        var status = await RunGitAsync(repositoryRoot, ["status", "--porcelain=v1", "--untracked-files=all"], cancellationToken).ConfigureAwait(false);
        if (status.ExitCode != 0)
        {
            throw new WorktreeBuildException("i18n-worktree-git-required", status.StandardError.Trim());
        }
        if (!string.IsNullOrEmpty(status.StandardOutput))
        {
            throw new WorktreeBuildException("i18n-worktree-dirty", "Multilingual build requires a clean Git worktree with no staged, unstaged, or untracked changes.");
        }

        var effective = ConfigApplier.Apply(config, overrides);
        IReadOnlyList<string> inputPaths;
        try
        {
            inputPaths = ResolveInputPaths(effective, resolved, repositoryRoot, logger);
        }
        catch (ConfigException ex)
        {
            throw new WorktreeBuildException("i18n-worktree-input-untracked", ex.Message, ex);
        }
        var configRelative = RelativeInside(repositoryRoot, resolved.FullConfigPath, "Configuration file");
        var trackedConfig = await RunGitAsync(repositoryRoot, ["ls-files", "--error-unmatch", "--", configRelative], cancellationToken).ConfigureAwait(false);
        if (trackedConfig.ExitCode != 0)
        {
            throw new WorktreeBuildException("i18n-worktree-input-untracked", $"Required build input is not tracked by Git: {configRelative}");
        }
        if (inputPaths.Count > 0)
        {
            var ignored = await RunGitAsync(repositoryRoot, ["ls-files", "--others", "--ignored", "--exclude-standard", "-z", "--", .. inputPaths], cancellationToken).ConfigureAwait(false);
            if (ignored.ExitCode != 0)
            {
                throw new WorktreeBuildException("i18n-worktree-git-required", ignored.StandardError.Trim());
            }
            var ignoredPath = ignored.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (ignoredPath is not null)
            {
                throw new WorktreeBuildException("i18n-worktree-input-untracked", $"Required build input is ignored and not tracked by Git: {ignoredPath}");
            }
        }
        return new GitRepository(repositoryRoot, head.Trim());
    }

    private static IReadOnlyList<string> ResolveInputPaths(
        AppConfig config,
        ResolvedConfigPath resolved,
        string repositoryRoot,
        ILogger logger)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var siteRepositoryRelative = RelativeInside(repositoryRoot, resolved.RootDir, "Site root");
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }
            paths.Add(RelativeInside(repositoryRoot, path, "Build input"));
            var lexicalRelative = Path.GetRelativePath(resolved.RootDir, path);
            if (!Path.IsPathRooted(lexicalRelative) &&
                lexicalRelative != ".." &&
                !lexicalRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                paths.Add(Path.Combine(siteRepositoryRelative, lexicalRelative));
            }
            ValidateSymlinkTargets(path, repositoryRoot);
        }

        Add(resolved.FullConfigPath);
        var theme = ThemePathResolver.Resolve(resolved.RootDir, config.Theme, logger);
        if (!string.IsNullOrWhiteSpace(config.Theme.Name))
        {
            Add(theme.ThemeRoot);
        }
        Add(theme.ParentThemeRoot);
        Add(theme.LayoutsDir);
        Add(theme.AssetsDir);
        Add(theme.StaticDir);
        Add(theme.UserLayoutsDir);
        Add(Path.Combine(resolved.RootDir, "data"));
        foreach (var source in config.Content.Sources ?? [])
        {
            if (source.Markdown is { } markdown)
            {
                Add(BuildPathUtils.MakeAbsolute(resolved.RootDir, markdown.Dir));
            }
        }
        if (config.Content.RouteMetadata?.Source is { Length: > 0 } routeMetadata)
        {
            Add(BuildPathUtils.MakeAbsolute(resolved.RootDir, routeMetadata));
        }
        Add(Path.Combine(resolved.RootDir, ".bukit", "plugins.yaml"));
        Add(Path.Combine(resolved.RootDir, "plugins"));
        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    private static void ValidateSymlinkTargets(string path, string repositoryRoot)
    {
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.TryPop(out var entry))
        {
            FileSystemInfo info = Directory.Exists(entry) ? new DirectoryInfo(entry) : new FileInfo(entry);
            if (info.LinkTarget is { } &&
                (info.ResolveLinkTarget(returnFinalTarget: true)?.FullName is not { } target ||
                 !IsSameOrChild(repositoryRoot, target)))
            {
                throw new WorktreeBuildException("i18n-worktree-input-untracked", $"Required build input symlink leaves the Git repository: {entry}");
            }
            if (info is DirectoryInfo && info.LinkTarget is null)
            {
                foreach (var child in Directory.EnumerateFileSystemEntries(entry))
                {
                    pending.Push(child);
                }
            }
        }
    }

    private static async Task RunWorkerProcessAsync(WorkerPlan plan, CancellationToken cancellationToken)
    {
        var invocation = SelfInvocation();
        var startInfo = NewProcessStartInfo(invocation.FileName);
        foreach (var argument in invocation.PrefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.ArgumentList.Add("build");
        startInfo.Environment[WorkerEnvironmentVariable] = plan.RequestPath;
        var result = await ExternalToolProcessRunner.RunAsync(startInfo, WorkerTimeout, cancellationToken).ConfigureAwait(false);
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

    private static async Task AddWorktreeAsync(
        GitRepository repository,
        string path,
        string registryPath,
        List<string> ownedWorktrees,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.AppendAllTextAsync(registryPath, path + Environment.NewLine, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        RestrictFile(registryPath);
        var result = await RunGitAsync(repository.Root, ["worktree", "add", "--detach", path, repository.Head], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new WorktreeBuildException("i18n-worktree-worker-failed", $"Unable to create detached Git worktree: {result.StandardError.Trim()}");
        }
        ownedWorktrees.Add(path);
    }

    private static async Task CleanupOwnedWorktreesAsync(
        string repositoryRoot,
        IReadOnlyList<string> ownedWorktrees,
        string registryPath,
        CancellationToken cancellationToken)
    {
        List<string>? failures = null;
        foreach (var path in ownedWorktrees.Reverse())
        {
            if (!await IsRegisteredWorktreeAsync(repositoryRoot, path, cancellationToken).ConfigureAwait(false))
            {
                TryDeleteDirectory(path);
                continue;
            }
            var result = await RunGitAsync(repositoryRoot, ["worktree", "remove", "--force", path], cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                failures ??= [];
                failures.Add(result.StandardError.Trim());
            }
        }
        if (failures is { Count: > 0 })
        {
            throw new WorktreeBuildException("i18n-worktree-cleanup-failed", string.Join("; ", failures));
        }
        File.Delete(registryPath);
    }

    private static async Task<bool> IsRegisteredWorktreeAsync(
        string repositoryRoot,
        string path,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(repositoryRoot, ["worktree", "list", "--porcelain"], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new WorktreeBuildException("i18n-worktree-cleanup-failed", result.StandardError.Trim());
        }
        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal))
            .Select(line => line["worktree ".Length..].TrimEnd('\r'))
            .Any(candidate => string.Equals(
                PathUtils.GetCanonicalFullPath(candidate),
                PathUtils.GetCanonicalFullPath(path),
                Bukit.Shared.PlatformPathHelper.PathComparison));
    }

    private static async Task RecoverOwnedWorktreesAsync(
        string repositoryRoot,
        string runsRoot,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var runRoot in Directory.EnumerateDirectories(runsRoot))
        {
            var registry = Path.Combine(runRoot, "owned-worktrees.txt");
            if (!File.Exists(registry))
            {
                TryDeleteDirectory(runRoot);
                continue;
            }
            var paths = (await File.ReadAllLinesAsync(registry, cancellationToken).ConfigureAwait(false))
                .Where(path => IsSameOrChild(runRoot, path))
                .Distinct(Bukit.Shared.PlatformPathHelper.PathComparison == StringComparison.OrdinalIgnoreCase
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
                .ToArray();
            await CleanupOwnedWorktreesAsync(repositoryRoot, paths, registry, cancellationToken).ConfigureAwait(false);
            TryDeleteDirectory(runRoot);
            logger.Warn($"event=build.i18n.worktree.recovered run={Path.GetFileName(runRoot)} worktrees={paths.Length}");
        }
    }

    private static async Task<ExternalToolProcessResult> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = NewProcessStartInfo("git");
        startInfo.WorkingDirectory = workingDirectory;
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(workingDirectory);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        try
        {
            return await ExternalToolProcessRunner.RunAsync(startInfo, GitTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new WorktreeBuildException("i18n-worktree-git-required", "Git executable is unavailable.", ex);
        }
    }

    private static async Task<string> RequireGitOutputAsync(
        string root,
        IReadOnlyList<string> arguments,
        string code,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(root, arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new WorktreeBuildException(code, result.StandardError.Trim());
        }
        return result.StandardOutput;
    }

    private static ProcessStartInfo NewProcessStartInfo(string fileName)
        => new()
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

    internal static void PrepareStaging(string source, string staging, bool copyExisting)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(staging)!);
        Directory.CreateDirectory(staging);
        if (copyExisting && Directory.Exists(source))
        {
            foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
            {
                var destination = Path.Combine(staging, entry.Name);
                if (entry.LinkTarget is { } target)
                {
                    if (entry is DirectoryInfo)
                        Directory.CreateSymbolicLink(destination, target);
                    else
                        File.CreateSymbolicLink(destination, target);
                }
                else if (entry is DirectoryInfo)
                {
                    PrepareStaging(entry.FullName, destination, copyExisting: true);
                }
                else
                {
                    File.Copy(entry.FullName, destination);
                }
            }
        }
    }

    private static void CopyVariantManifest(string stagingCache, string workerCache, string language, string outputDir)
    {
        var fileName = $"build-manifest.{BuildPathUtils.SanitizeFileSegment(language)}.json";
        var source = Path.Combine(stagingCache, fileName);
        if (!File.Exists(source))
        {
            return;
        }
        var manifest = BuildManifest.Load(source);
        manifest.OutputRoot = Path.GetFullPath(outputDir);
        manifest.Save(Path.Combine(workerCache, fileName));
    }

    private static void RewriteManifestRoots(
        string cacheDir,
        string oldOutputRoot,
        string newOutputRoot,
        IReadOnlyList<string> languages)
    {
        RewriteManifest(Path.Combine(cacheDir, "build-manifest.json"), oldOutputRoot, newOutputRoot);
        foreach (var language in languages)
        {
            var fileName = $"build-manifest.{BuildPathUtils.SanitizeFileSegment(language)}.json";
            RewriteManifest(
                Path.Combine(cacheDir, fileName),
                Path.Combine(oldOutputRoot, language),
                Path.Combine(newOutputRoot, language));
        }
    }

    private static void RewriteManifest(string path, string oldRoot, string newRoot)
    {
        if (!File.Exists(path))
        {
            return;
        }
        var manifest = BuildManifest.Load(path);
        if (string.IsNullOrEmpty(manifest.OutputRoot) ||
            string.Equals(manifest.OutputRoot, Path.GetFullPath(oldRoot), Bukit.Shared.PlatformPathHelper.PathComparison))
        {
            manifest.OutputRoot = Path.GetFullPath(newRoot);
            manifest.Save(path);
        }
    }

    internal static void CommitDirectories(
        IReadOnlyList<(string Staging, string Final, string Backup)> directories,
        IReadOnlyList<(string Staging, string Final, string Backup)> files)
    {
        foreach (var item in directories)
            if (File.Exists(item.Final)) throw new IOException($"Directory transaction target is a file: {item.Final}");
        foreach (var item in files)
            if (Directory.Exists(item.Final)) throw new IOException($"File transaction target is a directory: {item.Final}");

        var completedDirectories = new List<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)>();
        var completedFiles = new List<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)>();
        try
        {
            foreach (var item in directories)
            {
                var hadOriginal = Directory.Exists(item.Final);
                if (hadOriginal)
                {
                    Directory.Move(item.Final, item.Backup);
                }
                completedDirectories.Add((item.Staging, item.Final, item.Backup, hadOriginal, false));
                Directory.Move(item.Staging, item.Final);
                completedDirectories[^1] = (item.Staging, item.Final, item.Backup, hadOriginal, true);
            }
            foreach (var item in files.Where(item => File.Exists(item.Staging)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.Final)!);
                var hadOriginal = File.Exists(item.Final);
                if (hadOriginal)
                {
                    File.Move(item.Final, item.Backup);
                }
                completedFiles.Add((item.Staging, item.Final, item.Backup, hadOriginal, false));
                File.Move(item.Staging, item.Final);
                completedFiles[^1] = (item.Staging, item.Final, item.Backup, hadOriginal, true);
            }
        }
        catch (Exception commitError)
        {
            var rollbackErrors = new List<Exception>();
            foreach (var item in completedFiles.AsEnumerable().Reverse())
            {
                try
                {
                    if (item.Installed) File.Delete(item.Final);
                    if (item.HadOriginal) File.Move(item.Backup, item.Final);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    rollbackErrors.Add(new IOException($"Unable to restore '{item.Final}'; backup: '{item.Backup}'.", ex));
                }
            }
            foreach (var item in completedDirectories.AsEnumerable().Reverse())
            {
                try
                {
                    if (item.Installed) Directory.Delete(item.Final, recursive: true);
                    if (item.HadOriginal) Directory.Move(item.Backup, item.Final);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    rollbackErrors.Add(new IOException($"Unable to restore '{item.Final}'; backup: '{item.Backup}'.", ex));
                }
            }
            if (rollbackErrors.Count > 0)
                throw new AggregateException("Output transaction failed and rollback is incomplete.", new[] { commitError }.Concat(rollbackErrors));
            throw;
        }
        foreach (var item in completedFiles.Where(item => item.HadOriginal))
        {
            CleanupBackup(item.Backup, isDirectory: false);
        }
        foreach (var item in completedDirectories.Where(item => item.HadOriginal))
        {
            CleanupBackup(item.Backup, isDirectory: true);
        }
    }

    internal static void CleanupBackup(string path, bool isDirectory)
    {
        try
        {
            if (isDirectory)
                Directory.Delete(path, recursive: true);
            else
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[i18n-worktree-cleanup-failed] Build committed; backup retained at '{path}': {ex.Message}");
        }
    }

    private static MetricsPaths ResolveMetricsPaths(
        string? configuredPath,
        string projectRoot,
        string finalOutput,
        string stagingOutput,
        string runRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new MetricsPaths(null, []);
        }
        var finalPath = Path.GetFullPath(Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(projectRoot, configuredPath));
        if (IsSameOrChild(finalOutput, finalPath))
        {
            return new MetricsPaths(Path.Combine(stagingOutput, Path.GetRelativePath(finalOutput, finalPath)), []);
        }
        var stagingPath = Path.Combine(runRoot, "metrics", Path.GetFileName(finalPath));
        return new MetricsPaths(stagingPath,
        [
            (stagingPath, finalPath, Sibling(finalPath, "bukit-backup")),
            (Path.ChangeExtension(stagingPath, ".html"), Path.ChangeExtension(finalPath, ".html"), Sibling(Path.ChangeExtension(finalPath, ".html"), "bukit-backup"))
        ]);
    }

    private static string MapIntoWorktree(GitRepository repository, string path, string worktreeRoot)
        => Path.GetFullPath(Path.Combine(worktreeRoot, RelativeInside(repository.Root, path, "Site path")));

    private static string RelativeInside(string root, string path, string description)
    {
        var fullRoot = PathUtils.GetCanonicalFullPath(root);
        var fullPath = PathUtils.GetCanonicalFullPath(path);
        if (!IsSameOrChild(fullRoot, fullPath))
        {
            throw new WorktreeBuildException("i18n-worktree-input-untracked", $"{description} must be inside the Git repository: {fullPath}");
        }
        return Path.GetRelativePath(fullRoot, fullPath);
    }

    private static bool IsSameOrChild(string root, string path)
        => PathUtils.IsSameOrSubPathOf(path, root);

    private static void EnsureDisjoint(string left, string right)
    {
        if (IsSameOrChild(left, right) || IsSameOrChild(right, left))
        {
            throw new WorktreeBuildException("i18n-worktree-input-untracked", "Build output and cache directories must not overlap for a multilingual Worktree build.");
        }
    }

    private static string Sibling(string path, string suffix)
    {
        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new WorktreeBuildException("i18n-worktree-result-invalid", $"Path has no parent directory: {fullPath}");
        var name = Path.GetFileName(fullPath);
        if (name.Length == 0)
        {
            throw new WorktreeBuildException("i18n-worktree-result-invalid", $"Unsafe transaction path: {fullPath}");
        }
        return Path.Combine(parent, $".{name}.{suffix}");
    }

    private static string RunsRoot(string repositoryRoot)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(repositoryRoot))))[..16];
        return Path.Combine(Path.GetTempPath(), "bukit-i18n-worktrees", hash);
    }

    internal static FileStream AcquireRunLock(string repositoryRoot)
    {
        var runsRoot = RunsRoot(repositoryRoot);
        Directory.CreateDirectory(runsRoot);
        RestrictDirectory(runsRoot);
        // ponytail: serialize builds per checkout; use per-output locks only if concurrent builds become necessary.
        // Keep the lock file: deleting it could let another process lock a different inode.
        try
        {
            return new FileStream(Path.Combine(runsRoot, "build.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex)
        {
            throw new WorktreeBuildException("i18n-worktree-worker-failed", "Cannot acquire the multilingual build lock; another build may still be running.", ex);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void RestrictDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void RestrictFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static LogLevel ParseLogLevel(string? level)
        => (level ?? "info").Trim().ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "warn" => LogLevel.Warn,
            "error" => LogLevel.Error,
            _ => LogLevel.Info
        };

    private sealed record GitRepository(string Root, string Head);
    private sealed record WorkerPlan(string Language, string RequestPath, string ResultPath, string OutputDir);
    private sealed record MetricsPaths(
        string? StagingPath,
        IReadOnlyList<(string Staging, string Final, string Backup)> ExternalCommit);
}
