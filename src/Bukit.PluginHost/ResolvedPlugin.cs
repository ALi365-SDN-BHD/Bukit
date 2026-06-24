using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Protocol;

namespace Bukit.PluginHost;

public sealed record ResolvedPlugin(
    string Id,
    string Version,
    string Platform,
    string ExecutablePath,
    string WorkingDirectory,
    PluginHostInfo Host,
    IReadOnlyList<string>? Arguments = null,
    PluginTimeoutOptions? Timeout = null,
    PluginOutputLimitOptions? Output = null,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null)
{
    public IReadOnlyList<string> Arguments { get; init; } = Arguments ?? [];
    public PluginTimeoutOptions Timeout { get; init; } = Timeout ?? new PluginTimeoutOptions();
    public PluginOutputLimitOptions Output { get; init; } = Output ?? new PluginOutputLimitOptions();
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
        EnvironmentVariables ?? new Dictionary<string, string?>(StringComparer.Ordinal);
}
