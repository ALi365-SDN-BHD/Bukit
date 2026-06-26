using Bukit.Notion;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Bukit.Notion.Mapping;

public static class NotionDatabaseMapLoader
{
    public static NotionDatabaseMap? Load(string path, out IReadOnlyList<NotionDatabaseMapDiagnostic> diagnostics)
    {
        var errors = new List<NotionDatabaseMapDiagnostic>();
        try
        {
            using var reader = new StreamReader(File.OpenRead(path));
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                diagnostics = [InvalidYaml(path, "Database map root must be a mapping.")];
                return null;
            }

            YamlMappingNode? databasesNode = GetOptionalMapping(root, "databases");
            if (databasesNode is null)
            {
                diagnostics = [new NotionDatabaseMapDiagnostic(
                    "notion.databaseMapMissingDatabases",
                    NotionDiagnosticSeverity.Error,
                    "Database map must contain a databases mapping.",
                    path)];
                return null;
            }

            var entries = new Dictionary<string, NotionDatabaseMapEntry>(StringComparer.Ordinal);
            foreach ((YamlNode keyNode, YamlNode valueNode) in databasesNode.Children)
            {
                string? name = ReadKey(keyNode);
                if (name is null || valueNode is not YamlMappingNode entryNode)
                {
                    errors.Add(InvalidYaml(path, "Each databases entry must be a named mapping."));
                    continue;
                }

                entries[name] = ReadEntry(name, entryNode, path, errors);
            }

            diagnostics = errors;
            return new NotionDatabaseMap(path, entries);
        }
        catch (YamlException ex)
        {
            diagnostics = [InvalidYaml(path, $"Database map contains invalid YAML: {ex.Message}")];
            return null;
        }
        catch (IOException ex)
        {
            diagnostics = [InvalidYaml(path, $"Database map could not be read: {ex.Message}")];
            return null;
        }
    }

    private static NotionDatabaseMapEntry ReadEntry(
        string name,
        YamlMappingNode entryNode,
        string path,
        List<NotionDatabaseMapDiagnostic> diagnostics)
        => new(
            Name: name,
            Title: GetOptionalString(entryNode, "title"),
            Seed: GetOptionalString(entryNode, "seed"),
            Collection: GetOptionalString(entryNode, "collection"),
            DataSourceId: GetOptionalString(entryNode, "dataSourceId"),
            DatabaseId: GetOptionalString(entryNode, "databaseId"),
            UniqueField: GetOptionalString(entryNode, "uniqueField"),
            Properties: ReadProperties(entryNode, $"{path}#databases.{name}", diagnostics));

    private static IReadOnlyDictionary<string, NotionPropertyMapping> ReadProperties(
        YamlMappingNode entryNode,
        string path,
        List<NotionDatabaseMapDiagnostic> diagnostics)
    {
        if (!entryNode.Children.TryGetValue(new YamlScalarNode("properties"), out YamlNode? propertiesNode))
        {
            return new Dictionary<string, NotionPropertyMapping>(StringComparer.Ordinal);
        }

        if (propertiesNode is not YamlMappingNode properties)
        {
            diagnostics.Add(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapInvalidProperty",
                NotionDiagnosticSeverity.Error,
                "properties must be a mapping when present.",
                path));
            return new Dictionary<string, NotionPropertyMapping>(StringComparer.Ordinal);
        }

        var mappings = new Dictionary<string, NotionPropertyMapping>(StringComparer.Ordinal);
        foreach ((YamlNode keyNode, YamlNode valueNode) in properties.Children)
        {
            string? propertyName = ReadKey(keyNode);
            if (propertyName is null || valueNode is not YamlMappingNode propertyNode)
            {
                diagnostics.Add(new NotionDatabaseMapDiagnostic(
                    "notion.databaseMapInvalidProperty",
                    NotionDiagnosticSeverity.Error,
                    "Each property mapping must be a named mapping.",
                    path));
                continue;
            }

            mappings[propertyName] = new NotionPropertyMapping(
                propertyName,
                GetOptionalString(propertyNode, "source"),
                GetOptionalString(propertyNode, "type"));
        }

        return mappings;
    }

    private static YamlMappingNode? GetOptionalMapping(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? child))
        {
            return null;
        }

        return child as YamlMappingNode;
    }

    private static string? GetOptionalString(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? child))
        {
            return null;
        }

        return child is YamlScalarNode scalar ? scalar.Value : null;
    }

    private static string? ReadKey(YamlNode keyNode)
        => keyNode is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value)
            ? scalar.Value.Trim()
            : null;

    private static NotionDatabaseMapDiagnostic InvalidYaml(string path, string message)
        => new(
            "notion.databaseMapInvalidYaml",
            NotionDiagnosticSeverity.Error,
            message,
            path);
}
