namespace Bukit.Notion.Seed;

public sealed record NotionSeedArtifact(
    string Type,
    string Path,
    string? Description = null);
