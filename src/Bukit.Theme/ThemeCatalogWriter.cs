using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bukit.Theme;

public static class ThemeCatalogWriter
{
    public static string GenerateJson(ThemeManifestV2 manifest, ThemeComponentRegistry registry)
    {
        var catalog = new ThemeCatalog
        {
            Theme = manifest.Name,
            Version = manifest.Version,
            Description = manifest.Description,
            Extends = manifest.Extends,
            Sections = BuildSectionEntries(manifest, registry),
            Components = BuildComponentEntries(manifest, registry)
        };

        return JsonSerializer.Serialize(catalog, ThemeCatalogJsonContext.Default.ThemeCatalog);
    }

    public static void WriteToFile(ThemeManifestV2 manifest, ThemeComponentRegistry registry, string outputPath)
    {
        var json = GenerateJson(manifest, registry);
        var dir = Path.GetDirectoryName(outputPath);
        if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, json);
    }

    private static List<ThemeCatalogSectionEntry> BuildSectionEntries(ThemeManifestV2 manifest, ThemeComponentRegistry registry)
    {
        var entries = new List<ThemeCatalogSectionEntry>();
        foreach (var name in registry.GetAllSectionNames())
        {
            var def = registry.ResolveSection(name);
            if (def is null) continue;

            var sectionSchema = LoadSectionSchema(def);
            entries.Add(new ThemeCatalogSectionEntry
            {
                Name = name,
                Description = def.Description,
                Variants = def.Variants?.Keys.ToList(),
                RequiredProps = sectionSchema?.Props
                    ?.Where(p => p.Value.Required)
                    .Select(p => p.Key)
                    .ToList(),
                OptionalProps = sectionSchema?.Props
                    ?.Where(p => !p.Value.Required)
                    .Select(p => p.Key)
                    .ToList(),
                DataSources = def.Data?.Source is not null ? [def.Data.Source] : null,
                BestFor = GuessBestFor(name, def)
            });
        }
        return entries;
    }

    private static List<ThemeCatalogComponentEntry> BuildComponentEntries(ThemeManifestV2 manifest, ThemeComponentRegistry registry)
    {
        var entries = new List<ThemeCatalogComponentEntry>();
        foreach (var name in registry.GetAllComponentNames())
        {
            var def = registry.ResolveComponent(name);
            if (def is null) continue;

            entries.Add(new ThemeCatalogComponentEntry
            {
                Name = name,
                Props = def.Props
            });
        }
        return entries;
    }

    private static SectionSchema? LoadSectionSchema(ThemeSectionDefinition def)
    {
        if (string.IsNullOrEmpty(def.Schema)) return null;
        var schemaPath = def.Schema;
        if (!Path.IsPathRooted(schemaPath)) schemaPath = Path.GetFullPath(schemaPath);
        return SectionSchema.Load(schemaPath);
    }

    private static List<string>? GuessBestFor(string name, ThemeSectionDefinition def)
    {
        var result = new List<string>();
        var lower = name.ToLowerInvariant();
        if (lower.Contains("hero")) result.Add("home page");
        if (lower.Contains("grid") || lower.Contains("card")) result.Add("listing page");
        if (lower.Contains("cta")) result.Add("landing page");
        if (lower.Contains("contact")) result.Add("contact page");
        if (lower.Contains("about")) result.Add("about page");
        return result.Count > 0 ? result : null;
    }
}

public sealed class ThemeCatalog
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "";

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("extends")]
    public string? Extends { get; set; }

    [JsonPropertyName("sections")]
    public List<ThemeCatalogSectionEntry> Sections { get; set; } = [];

    [JsonPropertyName("components")]
    public List<ThemeCatalogComponentEntry> Components { get; set; } = [];
}

public sealed class ThemeCatalogSectionEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("variants")]
    public List<string>? Variants { get; set; }

    [JsonPropertyName("requiredProps")]
    public List<string>? RequiredProps { get; set; }

    [JsonPropertyName("optionalProps")]
    public List<string>? OptionalProps { get; set; }

    [JsonPropertyName("dataSources")]
    public List<string>? DataSources { get; set; }

    [JsonPropertyName("bestFor")]
    public List<string>? BestFor { get; set; }
}

public sealed class ThemeCatalogComponentEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("props")]
    public IReadOnlyDictionary<string, string>? Props { get; set; }
}

[JsonSerializable(typeof(ThemeCatalog))]
[JsonSerializable(typeof(ThemeCatalogSectionEntry))]
[JsonSerializable(typeof(ThemeCatalogComponentEntry))]
internal partial class ThemeCatalogJsonContext : JsonSerializerContext
{
}
