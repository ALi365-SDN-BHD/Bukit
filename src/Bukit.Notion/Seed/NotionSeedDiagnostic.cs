namespace Bukit.Notion.Seed;

public sealed record NotionSeedDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path = null);
