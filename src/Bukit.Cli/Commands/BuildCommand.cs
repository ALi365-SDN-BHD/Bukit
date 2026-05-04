using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class BuildCommand
{
    public static async Task<int> RunAsync(ArgReader reader)
    {
        var resolved = ConfigPathResolver.Resolve(reader);
        var config = ConfigLoader.Load(resolved.FullConfigPath);

        var siteUrl = reader.GetOption("--site-url");
        if (!string.IsNullOrWhiteSpace(siteUrl))
        {
            config = config with { Site = config.Site with { Url = siteUrl } };
        }

        var overrides = new ConfigOverrides
        {
            Output = reader.GetOption("--output"),
            BaseUrl = reader.GetOption("--base-url"),
            Clean = reader.HasFlag("--clean") ? true : reader.HasFlag("--no-clean") ? false : null,
            Draft = reader.HasFlag("--draft") ? true : null,
            IsCI = reader.HasFlag("--ci"),
            Incremental = reader.HasFlag("--incremental") ? true : reader.HasFlag("--no-incremental") ? false : null,
            CacheDir = reader.GetOption("--cache-dir"),
            MetricsPath = reader.GetOption("--metrics"),
            Jobs = TryParsePositiveInt(reader.GetOption("--jobs"))
        };

        Environment.SetEnvironmentVariable("BUKIT_AUTO_SUMMARY", config.Site.AutoSummary ? "1" : "0");
        Environment.SetEnvironmentVariable("BUKIT_AUTO_SUMMARY_MAXLEN", config.Site.AutoSummaryMaxLength.ToString());

        var logger = new ConsoleLogger(ParseLogLevel(config.Logging.Level, overrides.IsCI), reader.GetOption("--log-format") ?? "text");
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
