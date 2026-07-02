using System.Text.Json;

namespace Bukit.Theme;

public sealed record SchemaPropDefinition
{
    public string Type { get; init; } = "string";
    public bool Required { get; init; }
    public int? MaxLength { get; init; }
}

public sealed record SectionSchema
{
    public string Name { get; init; } = "";
    public string? Label { get; init; }
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, SchemaPropDefinition>? Props { get; init; }

    public static SectionSchema? Load(string schemaPath)
    {
        if (!File.Exists(schemaPath)) return null;

        try
        {
            var json = File.ReadAllText(schemaPath);
            return JsonSerializer.Deserialize<SectionSchema>(json, JsonContext.Default.SectionSchema);
        }
        catch
        {
            return null;
        }
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(SectionSchema))]
[System.Text.Json.Serialization.JsonSerializable(typeof(SchemaPropDefinition))]
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, SchemaPropDefinition>))]
internal partial class JsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
