namespace Bukit.PluginHost;

public sealed record ProcessRunRequest(
    string ExecutablePath,
    IReadOnlyList<string>? Arguments,
    string StandardInput,
    string WorkingDirectory,
    TimeSpan Timeout,
    int StdoutMaxBytes,
    int StderrMaxBytes,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null)
{
    public IReadOnlyList<string> Arguments { get; init; } = Arguments ?? [];
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
        EnvironmentVariables ?? new Dictionary<string, string?>(StringComparer.Ordinal);
}
