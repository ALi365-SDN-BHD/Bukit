using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.Abstractions.Protocol;

public sealed record PluginManifestResponse(
    string Type,
    string Protocol,
    string RequestId,
    bool Success,
    IReadOnlyList<string>? Capabilities = null,
    IReadOnlyList<PluginCommandSpec>? Commands = null,
    PluginPermissionSet? RequiredPermissions = null,
    PluginError? Error = null,
    IReadOnlyList<PluginMessage>? Messages = null,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<string> Capabilities { get; init; } = Capabilities ?? [];
    public IReadOnlyList<PluginCommandSpec> Commands { get; init; } = Commands ?? [];
    public PluginPermissionSet RequiredPermissions { get; init; } = RequiredPermissions ?? new PluginPermissionSet();
    public IReadOnlyList<PluginMessage> Messages { get; init; } = Messages ?? [];
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}
