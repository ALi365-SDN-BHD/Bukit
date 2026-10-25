using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal static class CommandConfigLoader
{
    internal static (AppConfig Config, ILogger Logger) Load(string path, string logFormat, bool isCi)
        => Load(path, logFormat, isCi, ConfigLoader.Load);

    internal static (AppConfig Config, ILogger Logger) Load(
        string path, string logFormat, bool isCi, Func<string, ILogger, AppConfig> load)
    {
        var pending = new StartupDiagnostics();
        AppConfig config;
        try
        {
            config = load(path, pending);
        }
        catch
        {
            // A failed diagnostic sink must not replace the original load exception.
            try { pending.Replay(new ConsoleLogger(LogLevel.Warn, logFormat)); }
            catch { }
            throw;
        }

        var logger = new ConsoleLogger(ParseLogLevel(config.Logging.Level, isCi), logFormat);
        pending.Replay(logger);
        return (config, logger);
    }

    internal static LogLevel ParseLogLevel(string? level, bool isCi)
    {
        if (isCi)
            return LogLevel.Warn;

        return (level ?? "info").Trim().ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "info" => LogLevel.Info,
            "warn" => LogLevel.Warn,
            "error" => LogLevel.Error,
            _ => LogLevel.Info
        };
    }

    private sealed class StartupDiagnostics : ILogger
    {
        private readonly List<(LogLevel Level, string Message)> _events = [];
        public void Debug(string message) => _events.Add((LogLevel.Debug, message));
        public void Info(string message) => _events.Add((LogLevel.Info, message));
        public void Warn(string message) => _events.Add((LogLevel.Warn, message));
        public void Error(string message) => _events.Add((LogLevel.Error, message));

        internal void Replay(ILogger logger)
        {
            try
            {
                foreach (var (level, message) in _events)
                {
                    switch (level)
                    {
                        case LogLevel.Debug: logger.Debug(message); break;
                        case LogLevel.Info: logger.Info(message); break;
                        case LogLevel.Warn: logger.Warn(message); break;
                        case LogLevel.Error: logger.Error(message); break;
                    }
                }
            }
            finally { _events.Clear(); }
        }
    }
}
