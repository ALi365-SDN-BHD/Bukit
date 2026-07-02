using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Abstractions.Protocol;

public sealed record PluginInvokeResponse(
    string Type,
    string Protocol,
    string RequestId,
    bool Success,
    int ExitCode,
    PluginError? Error = null,
    IReadOnlyList<PluginMessage>? Messages = null,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null,
    IReadOnlyList<PluginArtifact>? Artifacts = null)
{
    public IReadOnlyList<PluginMessage> Messages { get; init; } = Messages ?? [];
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
    public IReadOnlyList<PluginArtifact> Artifacts { get; init; } = Artifacts ?? [];
}
