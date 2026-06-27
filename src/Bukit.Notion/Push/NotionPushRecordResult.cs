namespace Bukit.Notion.Push;

public sealed record NotionPushRecordResult(
    string Collection,
    string SeedFile,
    string Operation,
    string? Title,
    string UniqueField,
    string UniqueValue,
    string DataSourceId,
    string Status = NotionPushRecordStatus.Planned,
    string? RemotePageId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
