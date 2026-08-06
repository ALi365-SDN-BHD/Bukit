using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.PluginHost;

public sealed record ResolvedPlugin(
    string Id,
    string Version,
    string Platform,
    string ExecutablePath,
    string WorkingDirectory,
    PluginHostInfo Host,
    string? ProjectRoot = null,
    IReadOnlyList<string>? Arguments = null,
    PluginTimeoutOptions? Timeout = null,
    PluginOutputLimitOptions? Output = null,
    PluginPermissionSet? GrantedPermissions = null,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null,
    bool? Sha256Verified = null)
{
    public PluginResourceLimitOptions? Resources { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Arguments ?? [];
    public PluginTimeoutOptions Timeout { get; init; } = Timeout ?? new PluginTimeoutOptions();
    public PluginOutputLimitOptions Output { get; init; } = Output ?? new PluginOutputLimitOptions();
    public PluginPermissionSet GrantedPermissions { get; init; } = GrantedPermissions ?? new PluginPermissionSet();
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
        EnvironmentVariables ?? new Dictionary<string, string?>(StringComparer.Ordinal);
}
