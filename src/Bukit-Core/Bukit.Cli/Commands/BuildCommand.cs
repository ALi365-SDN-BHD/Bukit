using Bukit.Cli.Shared;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class BuildCommand
{
    public static Task<int> RunAsync(CliBoundCommand command, CancellationToken cancellationToken = default)
        => RunAsync(command, command.GetString("--log-format") ?? "text", cancellationToken);

    internal static async Task<int> RunAsync(CliBoundCommand command, string logFormat, CancellationToken cancellationToken = default)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var (config, logger) = CommandConfigLoader.Load(resolved.FullConfigPath, logFormat, command.GetBool("--ci"));

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

}
