namespace Bukit.Notion.Mapping;

public sealed record NotionDatabaseMapDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path = null);
