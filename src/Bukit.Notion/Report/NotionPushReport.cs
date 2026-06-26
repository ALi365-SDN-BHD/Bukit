using Bukit.Notion.Push;

namespace Bukit.Notion.Report;

public sealed record NotionPushReport(
    bool DryRun,
    string Mode,
    int PlannedCreate,
    int PlannedUpdate,
    int PlannedReplace,
    IReadOnlyList<NotionPushRecordResult>? Records = null)
{
    public IReadOnlyList<NotionPushRecordResult> Records { get; init; } = Records ?? [];
}
