using Bukit.Notion.Push;

namespace Bukit.Notion.Report;

public sealed record NotionPushReport(
    bool DryRun,
    string Mode,
    int PlannedCreate,
    int PlannedUpdate,
    int PlannedReplace,
    IReadOnlyList<NotionPushRecordResult>? Records = null,
    IReadOnlyList<NotionPushDiagnostic>? Diagnostics = null)
{
    public int PlannedUpsert { get; init; }

    public int Planned { get; init; }

    public int Created { get; init; }

    public int Updated { get; init; }

    public int Replaced { get; init; }

    public int Failed { get; init; }

    public int Skipped { get; init; }

    public IReadOnlyList<NotionPushRecordResult> Records { get; init; } = Records ?? [];
    public IReadOnlyList<NotionPushDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}
