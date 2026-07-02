using System.Text.Json.Serialization;

namespace Bukit.Theme;

public sealed class PageSectionDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("variant")]
    public string? Variant { get; set; }

    [JsonPropertyName("props")]
    public Dictionary<string, object?>? Props { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("filter")]
    public Dictionary<string, object?>? Filter { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("sort")]
    public string? Sort { get; set; }
}
