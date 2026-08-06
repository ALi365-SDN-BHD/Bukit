namespace Bukit.PluginHost;

public sealed record PluginProcessResult(
    int ExitCode,
    string StdoutJson,
    string Stderr,
    bool TimedOut,
    bool OutputLimitExceeded,
    ProcessOutputStream? OutputLimitStream = null)
{
    public string? ResourceLimitExceeded { get; init; }
}
