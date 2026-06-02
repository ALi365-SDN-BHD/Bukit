using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace Bukit.Cli.Commands;

internal sealed record ImportSeedRecord(
    string Collection,
    string Title,
    string Slug,
    string? Summary,
    string? Content,
    string? Language,
    bool Published,
    string? SeoTitle,
    string? SeoDescription);

internal static class ImportSeedRecordReader
{
    internal static readonly (string FileBase, string Collection)[] KnownFiles =
    [
        ("pages", "page"),
        ("posts", "post"),
        ("companies", "company"),
        ("services", "service")
    ];

    internal static List<ImportSeedRecord> ReadDirectory(string inputDir)
    {
        var records = new List<ImportSeedRecord>();
        foreach (var (fileBase, collection) in KnownFiles)
        {
            var jsonPath = Path.Combine(inputDir, $"{fileBase}.json");
            if (File.Exists(jsonPath))
                records.AddRange(ReadJson(jsonPath, collection));

            var yamlPath = Path.Combine(inputDir, $"{fileBase}.yaml");
            if (File.Exists(yamlPath))
                records.AddRange(ReadYaml(yamlPath, collection));

            var ymlPath = Path.Combine(inputDir, $"{fileBase}.yml");
            if (File.Exists(ymlPath))
                records.AddRange(ReadYaml(ymlPath, collection));
        }

        return records;
    }

    internal static List<ImportSeedRecord> ReadSeedFile(string inputDir, string seedFile, string collection)
    {
        var path = Path.Combine(inputDir, seedFile);
        if (!File.Exists(path)) return [];

        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".json" => ReadJson(path, collection).ToList(),
            ".yaml" or ".yml" => ReadYaml(path, collection).ToList(),
            _ => []
        };
    }

    private static IEnumerable<ImportSeedRecord> ReadJson(string path, string collection)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var title = ReadString(item, "title") ?? ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(title)) continue;

            yield return new ImportSeedRecord(
                Collection: NormalizeCollection(collection, ReadString(item, "type")),
                Title: title,
                Slug: ReadString(item, "slug") ?? "",
                Summary: ReadString(item, "summary"),
                Content: ReadString(item, "content"),
                Language: ReadString(item, "language"),
                Published: ReadBool(item, "published") ?? true,
                SeoTitle: ReadString(item, "seo_title"),
                SeoDescription: ReadString(item, "seo_description"));
        }
    }

    private static IEnumerable<ImportSeedRecord> ReadYaml(string path, string collection)
    {
        var stream = new YamlStream();
        using var reader = File.OpenText(path);
        stream.Load(reader);
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlSequenceNode seq)
            yield break;

        foreach (var node in seq.Children.OfType<YamlMappingNode>())
        {
            var title = ReadString(node, "title") ?? ReadString(node, "name");
            if (string.IsNullOrWhiteSpace(title)) continue;

            yield return new ImportSeedRecord(
                Collection: NormalizeCollection(collection, ReadString(node, "type")),
                Title: title,
                Slug: ReadString(node, "slug") ?? "",
                Summary: ReadString(node, "summary"),
                Content: ReadString(node, "content"),
                Language: ReadString(node, "language"),
                Published: ReadBool(node, "published") ?? true,
                SeoTitle: ReadString(node, "seo_title"),
                SeoDescription: ReadString(node, "seo_description"));
        }
    }

    private static string NormalizeCollection(string fallback, string? type)
    {
        var normalized = string.IsNullOrWhiteSpace(type) ? fallback : type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "home" or "page" or "pages" => "page",
            "post" or "posts" or "article" or "articles" => "post",
            "company" or "companies" => "company",
            "service" or "services" => "service",
            _ => fallback
        };
    }

    private static string? ReadString(JsonElement item, string name)
    {
        return item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement item, string name)
    {
        return item.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }

    private static string? ReadString(YamlMappingNode node, string name)
    {
        return node.Children.TryGetValue(new YamlScalarNode(name), out var value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }

    private static bool? ReadBool(YamlMappingNode node, string name)
    {
        var value = ReadString(node, name);
        return bool.TryParse(value, out var result) ? result : null;
    }
}
