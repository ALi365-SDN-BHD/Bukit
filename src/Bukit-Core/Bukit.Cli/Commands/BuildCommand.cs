using Bukit.Cli.Shared;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class BuildCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command, CancellationToken cancellationToken = default)
        => await RunCoreAsync(command, useWorktreeIsolation: true, cancellationToken).ConfigureAwait(false);

    internal static async Task<int> RunInProcessAsync(CliBoundCommand command, CancellationToken cancellationToken = default)
        => await RunCoreAsync(command, useWorktreeIsolation: false, cancellationToken).ConfigureAwait(false);

    private static async Task<int> RunCoreAsync(
        CliBoundCommand command,
        bool useWorktreeIsolation,
        CancellationToken cancellationToken)
    {
        if (useWorktreeIsolation && WorktreeBuildCoordinator.IsWorker)
        {
            return await WorktreeBuildCoordinator.RunWorkerAsync(cancellationToken).ConfigureAwait(false);
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var config = ConfigLoader.Load(resolved.FullConfigPath);

        var siteUrl = command.GetString("--site-url");
        if (!string.IsNullOrWhiteSpace(siteUrl))
        {
            config = config with { Site = config.Site with { Url = siteUrl } };
        }

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

        var logFormat = command.GetString("--log-format") ?? "text";
        var logger = new ConsoleLogger(ParseLogLevel(config.Logging.Level, overrides.IsCI), logFormat);

        if (useWorktreeIsolation && I18nOutputMerger.GetLanguages(ConfigApplier.Apply(config, overrides).Site).Count > 0)
        {
            return await WorktreeBuildCoordinator.RunAsync(
                config,
                resolved,
                overrides,
                logFormat,
                logger,
                cancellationToken).ConfigureAwait(false);
        }

        var engine = new SiteEngine(logger);
        await engine.BuildAsync(config, resolved.RootDir, overrides, cancellationToken);
        return 0;
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

    internal static LogLevel ParseLogLevel(string? level, bool isCi)
    {
        if (isCi)
        {
            return LogLevel.Warn;
        }

        return (level ?? "info").Trim().ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "info" => LogLevel.Info,
            "warn" => LogLevel.Warn,
            "error" => LogLevel.Error,
            _ => LogLevel.Info
        };
    }
}
