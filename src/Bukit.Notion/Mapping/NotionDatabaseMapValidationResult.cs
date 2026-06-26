using Bukit.Notion;

namespace Bukit.Notion.Mapping;

public sealed record NotionDatabaseMapValidationResult(
    bool Success,
    int ExitCode,
    NotionDatabaseMap? DatabaseMap = null,
    IReadOnlyList<NotionDatabaseMapDiagnostic>? Diagnostics = null,
    IReadOnlyList<NotionDatabaseMapArtifact>? Artifacts = null)
{
    public IReadOnlyList<NotionDatabaseMapDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
    public IReadOnlyList<NotionDatabaseMapArtifact> Artifacts { get; init; } = Artifacts ?? [];

    public static NotionDatabaseMapValidationResult Succeeded(NotionDatabaseMap databaseMap)
        => new(
            Success: true,
            ExitCode: 0,
            DatabaseMap: databaseMap,
            Diagnostics:
            [
                new NotionDatabaseMapDiagnostic(
                    "notion.databaseMapValid",
                    NotionDiagnosticSeverity.Info,
                    "Notion database map is valid.",
                    databaseMap.Path)
            ],
            Artifacts:
            [
                new NotionDatabaseMapArtifact(
                    "database-map-validation",
                    databaseMap.Path,
                    "Validated Notion database map handoff file.")
            ]);

    public static NotionDatabaseMapValidationResult Failed(params NotionDatabaseMapDiagnostic[] diagnostics)
        => new(false, 2, Diagnostics: diagnostics, Artifacts: []);
}
