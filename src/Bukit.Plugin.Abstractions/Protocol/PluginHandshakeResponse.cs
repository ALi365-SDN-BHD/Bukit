using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Abstractions.Protocol;

public sealed record PluginHandshakeResponse(
    string Type,
    string Protocol,
    string RequestId,
    bool Success,
    PluginIdentity? Plugin = null,
    PluginError? Error = null,
    IReadOnlyList<PluginMessage>? Messages = null,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PluginMessage> Messages { get; init; } = Messages ?? [];
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}

public sealed record PluginIdentity(
    string Id,
    string Name,
    string Version,
    string Platform,
    IReadOnlyList<string>? Capabilities = null)
{
    public IReadOnlyList<string> Capabilities { get; init; } = Capabilities ?? [];
}
