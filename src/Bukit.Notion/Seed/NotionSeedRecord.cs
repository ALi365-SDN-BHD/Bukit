using System.Text.Json;

namespace Bukit.Notion.Seed;

public sealed record NotionSeedRecord(
    string Collection,
    int Index,
    IReadOnlyDictionary<string, JsonElement>? Fields = null)
{
    public IReadOnlyDictionary<string, JsonElement> Fields { get; init; } =
        Fields ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}
