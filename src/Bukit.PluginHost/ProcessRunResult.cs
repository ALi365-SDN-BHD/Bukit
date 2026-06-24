namespace Bukit.PluginHost;

public sealed record ProcessRunResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut,
    bool OutputLimitExceeded,
    ProcessOutputStream? OutputLimitStream = null);
