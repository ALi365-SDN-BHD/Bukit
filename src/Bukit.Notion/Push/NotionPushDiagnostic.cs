namespace Bukit.Notion.Push;

public sealed record NotionPushDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path = null);
