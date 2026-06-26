namespace Bukit.Notion.Seed;

public sealed record NotionSeedCollection(
    string Name,
    string Path,
    IReadOnlyList<NotionSeedRecord>? Records = null)
{
    public IReadOnlyList<NotionSeedRecord> Records { get; init; } = Records ?? [];
}
