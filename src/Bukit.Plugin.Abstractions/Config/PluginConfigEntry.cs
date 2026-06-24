using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.Abstractions.Config;

public sealed record PluginConfigEntry(
    bool Enabled,
    string Source,
    IReadOnlyList<string>? ExposeCommands = null,
    PluginPermissionSet? Permissions = null,
    PluginTimeoutOptions? Timeout = null,
    PluginOutputLimitOptions? Output = null,
    string FailMode = "strict",
    bool AllowInCi = false,
    string? Description = null,
    bool PermissionsExplicit = false,
    bool ExposeCommandsDeclared = false,
    string ManifestPolicy = "static")
{
    public IReadOnlyList<string> ExposeCommands { get; init; } = ExposeCommands ?? [];
    public PluginPermissionSet Permissions { get; init; } = Permissions ?? new PluginPermissionSet();
    public PluginTimeoutOptions Timeout { get; init; } = Timeout ?? new PluginTimeoutOptions();
    public PluginOutputLimitOptions Output { get; init; } = Output ?? new PluginOutputLimitOptions();
}
