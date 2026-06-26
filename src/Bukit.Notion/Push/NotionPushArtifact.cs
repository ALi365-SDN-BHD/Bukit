namespace Bukit.Notion.Push;

public sealed record NotionPushArtifact(
    string Type,
    string Path,
    string? Description = null);
