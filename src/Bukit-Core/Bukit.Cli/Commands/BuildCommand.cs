using Bukit.Cli.Shared;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class BuildCommand
{
    public static Task<int> RunAsync(CliBoundCommand command, CancellationToken cancellationToken = default)
        => RunAsync(command, command.GetString("--log-format") ?? "text", useWorktreeIsolation: true, cancellationToken);

    internal static async Task<int> RunAsync(CliBoundCommand command, string logFormat, CancellationToken cancellationToken = default)
    {
        // Ctrl+C cancels the build instead of killing the process, so the build
        // transaction can roll back cleanly and keep content-source caches.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        void OnCancel(object? sender, ConsoleCancelEventArgs e)
        {
            if (cts.IsCancellationRequested)
            {
                return; // A second Ctrl+C falls through to the default hard exit.
            }

            e.Cancel = true;
            cts.Cancel();
        }

        Console.CancelKeyPress += OnCancel;
        try
        {
            return await RunAsync(command, logFormat, useWorktreeIsolation: true, cts.Token).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }
    }

    internal static Task<int> RunInProcessAsync(CliBoundCommand command, CancellationToken cancellationToken = default)
        => RunAsync(command, command.GetString("--log-format") ?? "text", useWorktreeIsolation: false, cancellationToken);

    internal static Task<int> RunLoadedInProcessAsync(
        CliBoundCommand command,
        AppConfig config,
        ILogger logger,
        ResolvedConfigPath resolved,
        string logFormat,
        CancellationToken cancellationToken = default)
        => BuildFromLoadedConfigAsync(
            command,
            config,
            logger,
            resolved,
            logFormat,
            useWorktreeIsolation: false,
            cancellationToken);

    private static async Task<int> RunAsync(
        CliBoundCommand command,
        string logFormat,
        bool useWorktreeIsolation,
        CancellationToken cancellationToken)
    {
        if (useWorktreeIsolation && WorktreeBuildCoordinator.IsWorker)
        {
            return await WorktreeBuildCoordinator.RunWorkerAsync(cancellationToken).ConfigureAwait(false);
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var (config, logger) = CommandConfigLoader.Load(resolved.FullConfigPath, logFormat, command.GetBool("--ci"));

        var siteUrl = command.GetString("--site-url");
        if (!string.IsNullOrWhiteSpace(siteUrl))
        {
            config = config with { Site = config.Site with { Url = siteUrl } };
        }

        return await BuildFromLoadedConfigAsync(
            command,
            config,
            logger,
            resolved,
            logFormat,
            useWorktreeIsolation,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> BuildFromLoadedConfigAsync(
        CliBoundCommand command,
        AppConfig config,
        ILogger logger,
        ResolvedConfigPath resolved,
        string logFormat,
        bool useWorktreeIsolation,
        CancellationToken cancellationToken)
    {
        var overrides = new ConfigOverrides
        {
            Output = command.GetString("--output"),
            BaseUrl = command.GetString("--base-url"),
            Clean = command.GetBool("--clean") ? true : command.GetBool("--no-clean") ? false : null,
            Draft = command.GetBool("--draft") ? true : null,
            IsCI = command.GetBool("--ci"),
            Incremental = command.GetBool("--incremental") ? true : command.GetBool("--no-incremental") ? false : null,
            CacheDir = command.GetString("--cache-dir"),
            MetricsPath = command.GetString("--metrics"),
            Jobs = TryParsePositiveInt(command.GetString("--jobs"))
        };

        // Recording writes a staging directory that replaces the snapshot only after the
        // whole build succeeded; any failure (or cancellation) discards it.
        using var snapshot = NotionSnapshotOptions.OpenForBuild(command, resolved.RootDir, overrides.Output ?? config.Build.Output);
        using var snapshotScope = NotionSnapshotScope.Enter(snapshot?.Handle);
        if (snapshot is not null)
        {
            overrides = overrides with { NotionSnapshotDir = snapshot.Directory, NotionOffline = snapshot.Offline };
            if (!snapshot.Offline)
            {
                // Recording must render every page: an incremental build skips unchanged pages,
                // whose bodies would then be missing from (or pruned out of) the snapshot.
                overrides = overrides with { Incremental = false };
            }

            logger.Info(snapshot.Offline
                ? $"event=notion.snapshot.offline {snapshot.DescribeFields()}"
                : "event=notion.snapshot.recording");
            if (snapshot.Offline)
            {
                snapshot.EnsureDatabases(config);
            }
        }

        await BuildCoreAsync(config, logger, resolved, logFormat, useWorktreeIsolation, overrides, cancellationToken).ConfigureAwait(false);
        if (snapshot is { Offline: false })
        {
            snapshot.Commit();
            logger.Info("event=notion.snapshot.saved");
        }

        return 0;
    }

    private static async Task BuildCoreAsync(
        AppConfig config,
        ILogger logger,
        ResolvedConfigPath resolved,
        string logFormat,
        bool useWorktreeIsolation,
        ConfigOverrides overrides,
        CancellationToken cancellationToken)
    {
        if (useWorktreeIsolation && I18nOutputMerger.GetLanguages(ConfigApplier.Apply(config, overrides).Site).Count > 1)
        {
            var exitCode = await WorktreeBuildCoordinator.RunAsync(
                config,
                resolved,
                overrides,
                logFormat,
                logger,
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Multilingual build exited with code {exitCode}.");
            }

            return;
        }

        var engine = new SiteEngine(logger);
        await engine.BuildAsync(config, resolved.RootDir, overrides, cancellationToken);
    }

    private static int? TryParsePositiveInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!int.TryParse(text.Trim(), out var n) || n <= 0)
        {
            throw new CommandArgumentException("--jobs must be a positive integer");
        }

        return n;
    }
}
