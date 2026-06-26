using Bukit.Notion;

namespace Bukit.Notion.Seed;

public sealed record NotionSeedValidationResult(
    bool Success,
    int ExitCode,
    NotionSeedSet? SeedSet = null,
    IReadOnlyList<NotionSeedDiagnostic>? Diagnostics = null,
    IReadOnlyList<NotionSeedArtifact>? Artifacts = null)
{
    public IReadOnlyList<NotionSeedDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
    public IReadOnlyList<NotionSeedArtifact> Artifacts { get; init; } = Artifacts ?? [];

    public static NotionSeedValidationResult Succeeded(NotionSeedSet seedSet, IReadOnlyList<NotionSeedDiagnostic>? diagnostics = null)
        => new(
            Success: true,
            ExitCode: 0,
            SeedSet: seedSet,
            Diagnostics: BuildSuccessDiagnostics(seedSet, diagnostics),
            Artifacts:
            [
                new NotionSeedArtifact(
                    "seed-validation",
                    seedSet.SeedDirectory,
                    "Validated Notion seed handoff directory.")
            ]);

    public static NotionSeedValidationResult Failed(params NotionSeedDiagnostic[] diagnostics)
        => new(false, 2, Diagnostics: diagnostics, Artifacts: []);

    private static IReadOnlyList<NotionSeedDiagnostic> BuildSuccessDiagnostics(
        NotionSeedSet seedSet,
        IReadOnlyList<NotionSeedDiagnostic>? diagnostics)
    {
        var successDiagnostics = new List<NotionSeedDiagnostic>(diagnostics ?? [])
        {
            new(
                "notion.seedValid",
                NotionDiagnosticSeverity.Info,
                "Notion seed artifacts are valid.",
                seedSet.SeedDirectory)
        };
        return successDiagnostics;
    }
}
