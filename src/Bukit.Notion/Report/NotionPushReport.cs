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
    public IReadOnlyList<NotionPushRecordResult> Records { get; init; } = Records ?? [];
    public IReadOnlyList<NotionPushDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}
