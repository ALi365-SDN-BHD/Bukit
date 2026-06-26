namespace Bukit.Notion.Seed;

public sealed record NotionSeedSet(
    string SeedDirectory,
    IReadOnlyList<NotionSeedCollection>? Collections = null)
{
    public IReadOnlyList<NotionSeedCollection> Collections { get; init; } = Collections ?? [];
}
