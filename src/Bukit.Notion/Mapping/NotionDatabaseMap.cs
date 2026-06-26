namespace Bukit.Notion.Mapping;

public sealed record NotionDatabaseMap(
    string Path,
    IReadOnlyDictionary<string, NotionDatabaseMapEntry>? Databases = null)
{
    public IReadOnlyDictionary<string, NotionDatabaseMapEntry> Databases { get; init; } =
        Databases ?? new Dictionary<string, NotionDatabaseMapEntry>(StringComparer.Ordinal);
}
