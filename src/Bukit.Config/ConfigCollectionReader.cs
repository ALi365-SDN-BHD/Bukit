using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static class ConfigCollectionReader
{
    internal static IReadOnlyDictionary<string, CollectionConfig>? ReadCollections(YamlMappingNode siteNode)
    {
        var collectionsNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "collections");
        if (collectionsNode is null)
        {
            return null;
        }

        var collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in collectionsNode.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                continue;
            }

            if (kv.Value is not YamlMappingNode collectionNode)
            {
                throw new ConfigException($"site.collections.{keyNode.Value} must be a mapping.");
            }

            var paginationNode = ConfigYamlHelpers.GetOptionalMapping(collectionNode, "pagination");
            var outputNode = ConfigYamlHelpers.GetOptionalMapping(collectionNode, "output");
            collections[keyNode.Value.Trim()] = new CollectionConfig
            {
                Permalink = ConfigYamlHelpers.GetRequiredString(collectionNode, "permalink"),
                Template = ConfigYamlHelpers.GetRequiredString(collectionNode, "template"),
                ListRoute = ConfigYamlHelpers.GetOptionalString(collectionNode, "listRoute"),
                ListTemplate = ConfigYamlHelpers.GetOptionalString(collectionNode, "listTemplate"),
                SchemaFailMode = ConfigYamlHelpers.GetOptionalString(collectionNode, "schemaFailMode"),
                Pagination = new CollectionPaginationConfig
                {
                    Enabled = paginationNode is not null && (ConfigYamlHelpers.GetOptionalBool(paginationNode, "enabled") ?? false),
                    PageSize = paginationNode is null ? 10 : ConfigYamlHelpers.GetOptionalIntStrict(paginationNode, "pageSize") ?? 10
                },
                Output = new CollectionOutputConfig
                {
                    Rss = outputNode is null ? true : ConfigYamlHelpers.GetOptionalBool(outputNode, "rss") ?? true,
                    Sitemap = outputNode is null ? true : ConfigYamlHelpers.GetOptionalBool(outputNode, "sitemap") ?? true,
                    Archive = outputNode is not null && (ConfigYamlHelpers.GetOptionalBool(outputNode, "archive") ?? false)
                },
                FilteredLists = ReadFilteredLists(collectionNode),
                Schema = ReadSchema(collectionNode)
            };
        }

        return collections.Count == 0 ? null : collections;
    }

    internal static IReadOnlyDictionary<string, CollectionConfig>? TryReadCollectionsFile(string siteYamlPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(siteYamlPath)) ?? ".";
        var collectionsPath = Path.Combine(dir, "collections.yaml");
        if (!File.Exists(collectionsPath))
        {
            return null;
        }

        using var reader = File.OpenText(collectionsPath);
        var yaml = new YamlStream();
        try
        {
            yaml.Load(reader);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ConfigException($"Invalid YAML syntax in collections.yaml: {collectionsPath}", ex);
        }

        if (yaml.Documents.Count == 0)
        {
            return null;
        }

        if (yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            return null;
        }

        var collectionsNode = ConfigYamlHelpers.GetOptionalMapping(root, "collections") ?? root;
        if (collectionsNode is null || collectionsNode.Children.Count == 0)
        {
            return null;
        }

        var collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in collectionsNode.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                continue;
            }

            if (kv.Value is not YamlMappingNode collectionNode)
            {
                throw new ConfigException($"collections.yaml: entry '{keyNode.Value}' must be a mapping.");
            }

            var paginationNode = ConfigYamlHelpers.GetOptionalMapping(collectionNode, "pagination");
            var outputNode = ConfigYamlHelpers.GetOptionalMapping(collectionNode, "output");
            collections[keyNode.Value.Trim()] = new CollectionConfig
            {
                Permalink = ConfigYamlHelpers.GetRequiredString(collectionNode, "permalink"),
                Template = ConfigYamlHelpers.GetRequiredString(collectionNode, "template"),
                ListRoute = ConfigYamlHelpers.GetOptionalString(collectionNode, "listRoute"),
                ListTemplate = ConfigYamlHelpers.GetOptionalString(collectionNode, "listTemplate"),
                SchemaFailMode = ConfigYamlHelpers.GetOptionalString(collectionNode, "schemaFailMode"),
                Pagination = new CollectionPaginationConfig
                {
                    Enabled = paginationNode is not null && (ConfigYamlHelpers.GetOptionalBool(paginationNode, "enabled") ?? false),
                    PageSize = paginationNode is null ? 10 : ConfigYamlHelpers.GetOptionalIntStrict(paginationNode, "pageSize") ?? 10
                },
                Output = new CollectionOutputConfig
                {
                    Rss = outputNode is null ? true : ConfigYamlHelpers.GetOptionalBool(outputNode, "rss") ?? true,
                    Sitemap = outputNode is null ? true : ConfigYamlHelpers.GetOptionalBool(outputNode, "sitemap") ?? true,
                    Archive = outputNode is not null && (ConfigYamlHelpers.GetOptionalBool(outputNode, "archive") ?? false)
                },
                FilteredLists = ReadFilteredLists(collectionNode),
                Schema = ReadSchema(collectionNode)
            };
        }

        return collections.Count == 0 ? null : collections;
    }

    internal static IReadOnlyList<FilteredListConfig>? ReadFilteredLists(YamlMappingNode collectionNode)
    {
        var filteredListNode = ConfigYamlHelpers.GetOptionalSequence(collectionNode, "filteredLists");
        if (filteredListNode is null || filteredListNode.Children.Count == 0)
        {
            return null;
        }

        var filteredLists = new List<FilteredListConfig>();
        foreach (var child in filteredListNode.Children)
        {
            if (child is not YamlMappingNode filterNode)
            {
                throw new ConfigException("Each item in filteredLists must be a mapping.");
            }

            filteredLists.Add(new FilteredListConfig
            {
                Field = ConfigYamlHelpers.GetRequiredString(filterNode, "field"),
                Value = ConfigYamlHelpers.GetRequiredString(filterNode, "value"),
                ListRoute = ConfigYamlHelpers.GetRequiredString(filterNode, "listRoute"),
                ListTemplate = ConfigYamlHelpers.GetOptionalString(filterNode, "listTemplate")
            });
        }

        return filteredLists.Count == 0 ? null : filteredLists;
    }

    internal static IReadOnlyList<SchemaFieldDefinition>? ReadSchema(YamlMappingNode collectionNode)
    {
        var schemaNode = ConfigYamlHelpers.GetOptionalSequence(collectionNode, "schema");
        if (schemaNode is null || schemaNode.Children.Count == 0)
        {
            return null;
        }

        var fields = new List<SchemaFieldDefinition>();
        foreach (var child in schemaNode.Children)
        {
            if (child is not YamlMappingNode fieldNode)
            {
                continue;
            }

            var name = ConfigYamlHelpers.GetOptionalString(fieldNode, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            fields.Add(new SchemaFieldDefinition
            {
                Name = name,
                Type = ConfigYamlHelpers.GetOptionalString(fieldNode, "type") ?? "string",
                Label = ConfigYamlHelpers.GetOptionalString(fieldNode, "label"),
                Format = ConfigYamlHelpers.GetOptionalString(fieldNode, "format"),
                Enum = ConfigYamlHelpers.ReadStringList(fieldNode, "enum"),
                Min = ConfigYamlHelpers.GetOptionalDouble(fieldNode, "min"),
                Max = ConfigYamlHelpers.GetOptionalDouble(fieldNode, "max"),
                Required = ConfigYamlHelpers.GetOptionalBool(fieldNode, "required") ?? false,
                Default = fieldNode.Children.TryGetValue(new YamlScalarNode("default"), out var defaultNode)
                    ? ConfigYamlHelpers.ToObject(defaultNode)
                    : null
            });
        }

        return fields.Count == 0 ? null : fields;
    }

    internal static IReadOnlyList<ContentSourceConfig>? ReadSources(YamlMappingNode contentNode)
    {
        var sourcesNode = ConfigYamlHelpers.GetOptionalSequence(contentNode, "sources");
        if (sourcesNode is null)
        {
            return null;
        }

        var sources = new List<ContentSourceConfig>();
        foreach (var n in sourcesNode.Children)
        {
            if (n is not YamlMappingNode m)
            {
                throw new ConfigException("content.sources items must be mappings.");
            }

            sources.Add(ReadSource(m));
        }

        return sources;
    }

    internal static ContentSourceConfig ReadSource(YamlMappingNode sourceNode)
    {
        var type = ConfigYamlHelpers.GetRequiredString(sourceNode, "type");
        var name = ConfigYamlHelpers.GetOptionalString(sourceNode, "name");
        var mode = ConfigYamlHelpers.GetOptionalString(sourceNode, "mode") ?? "content";
        var collection = ConfigYamlHelpers.GetOptionalString(sourceNode, "collection");
        var addToCollections = ConfigYamlHelpers.ReadStringList(sourceNode, "addToCollections");
        if (type.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentSourceConfig
            {
                Type = "notion",
                Name = name,
                Mode = mode,
                Collection = collection,
                AddToCollections = addToCollections,
                Notion = SiteDefaultsApplier.ReadNotionConfigFrom(sourceNode)
            };
        }

        if (type.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentSourceConfig
            {
                Type = "markdown",
                Name = name,
                Mode = mode,
                Collection = collection,
                AddToCollections = addToCollections,
                Markdown = SiteDefaultsApplier.ReadMarkdownConfigFrom(sourceNode)
            };
        }

        return new ContentSourceConfig
        {
            Type = type,
            Name = name,
            Mode = mode,
            Collection = collection,
            AddToCollections = addToCollections
        };
    }
}
