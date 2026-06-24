namespace Bukit.PluginHost;

public sealed record PluginExecutionReport(
    string PluginId,
    string Operation,
    string RequestId,
    int ProcessExitCode,
    bool Success,
    bool TimedOut,
    bool OutputLimitExceeded,
    int StdoutBytes,
    int StderrBytes,
    string Stderr,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        Environment ?? new Dictionary<string, string>(StringComparer.Ordinal);
}
