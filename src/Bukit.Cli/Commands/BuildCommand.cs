using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class BuildCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        return RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = reader.GetOption("--config"),
                ["--site"] = reader.GetOption("--site"),
                ["--output"] = reader.GetOption("--output"),
                ["--base-url"] = reader.GetOption("--base-url"),
                ["--site-url"] = reader.GetOption("--site-url"),
                ["--cache-dir"] = reader.GetOption("--cache-dir"),
                ["--metrics"] = reader.GetOption("--metrics"),
                ["--jobs"] = reader.GetOption("--jobs"),
                ["--log-format"] = reader.GetOption("--log-format"),
            }
            .Where(x => x.Value is not null)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>()));
    }

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
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

        Environment.SetEnvironmentVariable("BUKIT_AUTO_SUMMARY", config.Site.AutoSummary ? "1" : "0");
        Environment.SetEnvironmentVariable("BUKIT_AUTO_SUMMARY_MAXLEN", config.Site.AutoSummaryMaxLength.ToString());

        var logger = new ConsoleLogger(ParseLogLevel(config.Logging.Level, overrides.IsCI), command.GetString("--log-format") ?? "text");
        var engine = new SiteEngine(logger);
        await engine.BuildAsync(config, resolved.RootDir, overrides);
        return 0;
    }

    private static int? TryParsePositiveInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (int.TryParse(text.Trim(), out var n) && n > 0)
        {
            return n;
        }

        return null;
    }

    private static LogLevel ParseLogLevel(string? level, bool isCi)
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
