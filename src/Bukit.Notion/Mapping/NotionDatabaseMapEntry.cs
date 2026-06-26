namespace Bukit.Notion.Mapping;

public sealed record NotionDatabaseMapEntry(
    string Name,
    string? Title,
    string? Seed,
    string? Collection,
    string? DataSourceId,
    string? DatabaseId,
    string? UniqueField,
    IReadOnlyDictionary<string, NotionPropertyMapping>? Properties = null)
{
    public IReadOnlyDictionary<string, NotionPropertyMapping> Properties { get; init; } =
        Properties ?? new Dictionary<string, NotionPropertyMapping>(StringComparer.Ordinal);

    public string? EffectiveDataSourceId
        => !string.IsNullOrWhiteSpace(DataSourceId) ? DataSourceId : DatabaseId;
}
