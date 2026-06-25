namespace Bukit.Importing.Seed;

public sealed record ImportSeedResult(
    bool Success,
    int ExitCode,
    IReadOnlyList<ImportSeedDiagnostic>? Diagnostics = null,
    IReadOnlyList<ImportSeedArtifact>? Artifacts = null)
{
    public IReadOnlyList<ImportSeedDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
    public IReadOnlyList<ImportSeedArtifact> Artifacts { get; init; } = Artifacts ?? [];

    public static ImportSeedResult Succeeded(IReadOnlyList<ImportSeedArtifact> artifacts)
        => new(true, 0, Diagnostics: [], Artifacts: artifacts);

    public static ImportSeedResult Failed(params ImportSeedDiagnostic[] diagnostics)
        => new(false, 2, Diagnostics: diagnostics, Artifacts: []);
}
