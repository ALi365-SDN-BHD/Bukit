namespace Bukit.Notion.Mapping;

public sealed record NotionDatabaseMapArtifact(
    string Type,
    string Path,
    string? Description = null);
