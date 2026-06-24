using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Abstractions.Protocol;

public sealed record PluginResponseEnvelope(
    string Type,
    string Protocol,
    string RequestId,
    bool Success,
    PluginError? Error = null,
    IReadOnlyList<PluginMessage>? Messages = null,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PluginMessage> Messages { get; init; } = Messages ?? [];
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}
