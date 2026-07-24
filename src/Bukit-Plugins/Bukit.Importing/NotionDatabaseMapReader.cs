using System.Text;
using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

/// <summary>
/// Reads and writes Notion database map YAML files.
/// Extracted from ImportNotionPushWorkflow for single-responsibility.
/// </summary>
internal static class NotionDatabaseMapReader
{
    internal static List<NotionDatabaseTarget> ReadDatabaseMap(
        string mapPath, string inputDir, string defaultUniqueField)
    {
        var stream = new YamlStream();
        using var reader = File.OpenText(mapPath);
        stream.Load(reader);
        if (stream.Documents.Count == 0 ||
            stream.Documents[0].RootNode is not YamlMappingNode root ||
            GetMap(root, "databases") is not { } databases)
            return [];

        var targets = new List<NotionDatabaseTarget>();
        foreach (var kv in databases.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value) ||
                kv.Value is not YamlMappingNode map)
                continue;

            var key = keyNode.Value.Trim();
            var seedFile = GetScalar(map, "seed") ?? $"{key}.json";
            var collection = GetScalar(map, "collection") ?? InferCollection(key, seedFile);
            targets.Add(new NotionDatabaseTarget(
                Key: key,
                Title: GetScalar(map, "title") ?? ToTitle(key),
                SeedFile: seedFile,
                Collection: collection,
                DatabaseId: GetScalar(map, "databaseId"),
                UniqueField: GetScalar(map, "uniqueField") ?? defaultUniqueField,
                Schema: ReadSchema(map, key, mapPath)));
        }
        return targets.Where(t => File.Exists(Path.Combine(inputDir, t.SeedFile))).ToList();
    }

    internal static IReadOnlyDictionary<string, string>? ReadSchema(
        YamlMappingNode map,
        string databaseKey,
        string mapPath)
    {
        var schemaPath = $"{mapPath}:databases.{databaseKey}.schema";
        var schemaNode = GetNode(map, "schema");
        if (schemaNode is null)
            return null;
        if (schemaNode is not YamlMappingNode schemaMap)
            throw new FormatException($"{schemaPath}: schema must be a mapping.");

        var parsed = new List<(string Raw, string Canonical, string Type)>();
        var canonicalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in schemaMap.Children)
        {
            if (pair.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
                throw new FormatException($"{schemaPath}: schema contains an invalid field name.");
            var field = keyNode.Value;
            if (field != field.Trim())
                throw new FormatException($"{schemaPath}: schema key '{field}' must not contain boundary whitespace.");
            var canonical = NotionPropertyNaming.Canonicalize(field);
            if (string.IsNullOrWhiteSpace(canonical))
                throw new FormatException($"{schemaPath}: Schema key '{field}' has an empty canonical Notion property name.");
            if (pair.Value is not YamlScalarNode typeNode || string.IsNullOrWhiteSpace(typeNode.Value))
                throw new FormatException($"{schemaPath}: Schema field '{field}' must declare a scalar type.");
            var type = typeNode.Value.Trim().ToLowerInvariant();
            if (type is not ("rich_text" or "select" or "multi_select" or "url" or "date" or "number" or "checkbox"))
                throw new FormatException($"{schemaPath}: Unsupported Notion schema type '{type}' for database '{databaseKey}', field '{field}'.");
            if (!canonicalKeys.Add(canonical))
                throw new FormatException($"{schemaPath}: schema keys normalize to duplicate Notion property '{canonical}'.");
            parsed.Add((field, canonical, type));
        }

        var schema = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (raw, canonical, type) in parsed)
        {
            if (!raw.Equals(canonical, StringComparison.Ordinal))
                throw new FormatException($"{schemaPath}: Schema key '{raw}' must use canonical Notion property name '{canonical}'.");
            if (NotionPropertyNaming.IsCore(canonical))
                throw new FormatException($"{schemaPath}: Schema key '{raw}' conflicts with fixed core property '{canonical}'.");
            schema.Add(canonical, type);
        }
        return schema;
    }

    internal static bool DatabaseMapHasMissingDatabaseIds(string databaseMapPath, string seedDir)
    {
        if (!File.Exists(databaseMapPath))
            return false;

        var stream = new YamlStream();
        using var reader = File.OpenText(databaseMapPath);
        stream.Load(reader);
        if (stream.Documents.Count == 0 ||
            stream.Documents[0].RootNode is not YamlMappingNode root ||
            root.Children.FirstOrDefault(kv =>
                kv.Key is YamlScalarNode scalar && scalar.Value == "databases").Value is not YamlMappingNode databases)
            return false;

        foreach (var kv in databases.Children)
        {
            if (kv.Key is not YamlScalarNode key ||
                string.IsNullOrWhiteSpace(key.Value) ||
                kv.Value is not YamlMappingNode database)
                continue;
            var seed = database.Children.FirstOrDefault(entry =>
                entry.Key is YamlScalarNode scalar && scalar.Value == "seed").Value is YamlScalarNode seedNode
                ? seedNode.Value
                : $"{key.Value.Trim()}.json";
            if (string.IsNullOrWhiteSpace(seed) || !File.Exists(Path.Combine(seedDir, seed)))
                continue;
            var id = database.Children.FirstOrDefault(entry =>
                entry.Key is YamlScalarNode scalar && scalar.Value == "databaseId").Value as YamlScalarNode;
            if (string.IsNullOrWhiteSpace(id?.Value))
                return true;
        }

        return false;
    }

    internal static string ResolveGeneratedMapPath(string inputDir, string? databaseMapPath, string? generatedMapPath)
    {
        if (!string.IsNullOrWhiteSpace(generatedMapPath))
            return Path.GetFullPath(generatedMapPath);
        if (!string.IsNullOrWhiteSpace(databaseMapPath))
            return Path.GetFullPath(databaseMapPath);
        return Path.Combine(inputDir, "notion-database-map.generated.yaml");
    }

    internal static void WriteDatabaseMap(string path, IReadOnlyList<NotionDatabaseTarget> targets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sb = new StringBuilder();
        sb.AppendLine("databases:");
        foreach (var target in targets)
        {
            sb.AppendLine($"  {target.Key}:");
            sb.AppendLine($"    title: {target.Title}");
            sb.AppendLine($"    seed: {target.SeedFile}");
            sb.AppendLine($"    collection: {target.Collection}");
            if (!string.IsNullOrWhiteSpace(target.DatabaseId))
                sb.AppendLine($"    databaseId: {target.DatabaseId}");
            sb.AppendLine($"    uniqueField: {target.UniqueField}");
            if (target.Schema is { Count: > 0 })
            {
                sb.AppendLine("    schema:");
                foreach (var field in target.Schema.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"      {field.Key}: {field.Value}");
            }
        }
        File.WriteAllText(path, sb.ToString());
    }

    internal static string InferCollection(string key, string seedFile)
    {
        var fileBase = Path.GetFileNameWithoutExtension(seedFile);
        var found = ImportSeedRecordReader.KnownFiles.FirstOrDefault(k =>
            k.FileBase.Equals(fileBase, StringComparison.OrdinalIgnoreCase) ||
            k.FileBase.Equals(key, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(found.Collection) ? key.TrimEnd('s') : found.Collection;
    }

    internal static string ToTitle(string key)
        => string.IsNullOrWhiteSpace(key)
            ? "Content"
            : char.ToUpperInvariant(key[0]) + key[1..];

    private static YamlMappingNode? GetMap(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv =>
            kv.Key is YamlScalarNode scalar && scalar.Value == key).Value as YamlMappingNode;

    private static YamlNode? GetNode(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv =>
            kv.Key is YamlScalarNode scalar && scalar.Value == key).Value;

    private static string? GetScalar(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv =>
            kv.Key is YamlScalarNode scalar && scalar.Value == key).Value is YamlScalarNode value
            ? value.Value
            : null;
}
