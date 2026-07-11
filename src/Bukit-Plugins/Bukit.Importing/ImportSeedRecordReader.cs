using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

public static class ImportSeedRecordReader
{
    public static readonly (string FileBase, string Collection)[] KnownFiles =
    [
        ("pages", "page"),
        ("navigation", "navigation"),
        ("posts", "post"),
        ("companies", "company"),
        ("services", "service")
    ];

    public static List<ImportSeedRecord> ReadDirectory(string inputDir)
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

    public static List<ImportSeedRecord> ReadSeedFile(string inputDir, string seedFile, string collection)
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
                SeoDescription: ReadString(item, "seo_description"),
                ExtraFields: ReadExtraFields(item));
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
                SeoDescription: ReadString(node, "seo_description"),
                ExtraFields: ReadExtraFields(node, path));
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
            "navigation" or "nav" or "menu" or "menus" => "navigation",
            _ => fallback
        };
    }

    private static IReadOnlyDictionary<string, object?>? ReadExtraFields(JsonElement item)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in item.EnumerateObject())
        {
            if (IsCoreField(property.Name))
                continue;
            fields[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number when property.Value.TryGetInt64(out var l) => l,
                JsonValueKind.Number when property.Value.TryGetDouble(out var d) => d,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => property.Value.EnumerateArray().Select(ReadJsonValue).ToArray(),
                _ => null
            };
        }

        return fields.Count == 0 ? null : fields;
    }

    private static object? ReadJsonValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var l) => l,
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => throw new FormatException($"Unsupported JSON array value kind: {value.ValueKind}.")
        };

    private static IReadOnlyDictionary<string, object?>? ReadExtraFields(YamlMappingNode node, string path)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in node.Children)
        {
            if (kv.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value) ||
                IsCoreField(key.Value))
                continue;
            fields[key.Value] = kv.Value switch
            {
                YamlScalarNode value => ParseYamlScalar(value),
                YamlSequenceNode sequence => ReadYamlScalarSequence(sequence, path, key.Value),
                YamlMappingNode => throw new FormatException(
                    $"YAML seed '{path}' field '{key.Value}' contains a nested mapping; only scalar values and scalar sequences are supported."),
                _ => throw new FormatException(
                    $"YAML seed '{path}' field '{key.Value}' contains an unsupported YAML node.")
            };
        }

        return fields.Count == 0 ? null : fields;
    }

    private static IReadOnlyList<object?> ReadYamlScalarSequence(
        YamlSequenceNode sequence,
        string path,
        string field)
    {
        var values = new List<object?>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            if (child is not YamlScalarNode scalar)
            {
                var kind = child is YamlMappingNode ? "mapping" : child is YamlSequenceNode ? "sequence" : "node";
                throw new FormatException(
                    $"YAML seed '{path}' field '{field}' contains a nested {kind}; only scalar sequence items are supported.");
            }
            values.Add(ParseYamlScalar(scalar));
        }
        return values.AsReadOnly();
    }

    private static bool IsCoreField(string name)
        => name is "title" or "name" or "slug" or "type" or "summary" or "content" or
           "language" or "published" or "seo_title" or "seo_description";

    private static object? ParseYamlScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        var tag = scalar.Tag.ToString();
        if (scalar.Style is not YamlDotNet.Core.ScalarStyle.Plain ||
            tag is "tag:yaml.org,2002:str" or "!!str")
            return value;
        if (value is null || value is "~" || value.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;
        if (string.IsNullOrWhiteSpace(value))
            return value;
        if (bool.TryParse(value, out var b))
            return b;
        if (long.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var l))
            return l;
        if (value.Equals(".nan", StringComparison.OrdinalIgnoreCase))
            return double.NaN;
        if (value.Equals(".inf", StringComparison.OrdinalIgnoreCase))
            return double.PositiveInfinity;
        if (value.Equals("-.inf", StringComparison.OrdinalIgnoreCase))
            return double.NegativeInfinity;
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return value;
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
