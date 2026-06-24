using Bukit.Plugin.Abstractions.Results;
using Bukit.Plugin.Abstractions.Security;

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
    IReadOnlyDictionary<string, string>? Environment = null,
    string? PluginVersion = null,
    string? Protocol = null,
    string? Platform = null,
    string? Command = null,
    IReadOnlyList<string>? CommandPath = null,
    string? Entry = null,
    DateTimeOffset? StartedAt = null,
    long? DurationMs = null,
    int? ResponseExitCode = null,
    bool? Sha256Verified = null,
    PluginPermissionSet? Permissions = null,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null,
    IReadOnlyList<PluginArtifact>? Artifacts = null)
{
    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        Environment ?? new Dictionary<string, string>(StringComparer.Ordinal);
    public IReadOnlyList<string> CommandPath { get; init; } = CommandPath ?? [];
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
    public IReadOnlyList<PluginArtifact> Artifacts { get; init; } = Artifacts ?? [];
}
