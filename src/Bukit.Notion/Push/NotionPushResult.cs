namespace Bukit.Notion.Push;

public sealed record NotionPushResult(
    bool Success,
    int ExitCode,
    bool DryRun,
    NotionPushMode Mode,
    IReadOnlyList<NotionPushRecordResult>? Records = null,
    IReadOnlyList<NotionPushDiagnostic>? Diagnostics = null,
    IReadOnlyList<NotionPushArtifact>? Artifacts = null)
{
    public IReadOnlyList<NotionPushRecordResult> Records { get; init; } = Records ?? [];
    public IReadOnlyList<NotionPushDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
    public IReadOnlyList<NotionPushArtifact> Artifacts { get; init; } = Artifacts ?? [];

    public static NotionPushResult Failed(NotionPushMode mode, bool dryRun, params NotionPushDiagnostic[] diagnostics)
        => new(false, 2, dryRun, mode, Records: [], Diagnostics: diagnostics, Artifacts: []);
}
