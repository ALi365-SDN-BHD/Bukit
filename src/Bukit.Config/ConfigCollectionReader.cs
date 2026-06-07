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
                Template = ConfigYamlHelpers.GetOptionalString(collectionNode, "template"),
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
                Template = ConfigYamlHelpers.GetOptionalString(collectionNode, "template"),
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

    internal static ContentModelSchemaConfig? ReadContentModelSchema(YamlMappingNode contentNode)
    {
        var node = ConfigYamlHelpers.GetOptionalMapping(contentNode, "modelSchema");
        if (node is null)
        {
            return null;
        }

        return new ContentModelSchemaConfig
        {
            ContentTypes = ConfigYamlHelpers.ReadStringList(node, "contentTypes"),
            Statuses = ConfigYamlHelpers.ReadStringList(node, "statuses"),
            ReviewStatuses = ConfigYamlHelpers.ReadStringList(node, "reviewStatuses"),
            SyncStatuses = ConfigYamlHelpers.ReadStringList(node, "syncStatuses"),
            CanonicalMappings = ReadCanonicalMappings(node),
            CustomFields = ReadCustomFields(node),
            EntityMappings = ReadEntityMappings(node),
            RelationMappings = ReadRelationMappings(node),
            Media = ReadMediaPolicy(node),
            RejectUnknownRawKeys = ConfigYamlHelpers.GetOptionalBool(node, "rejectUnknownRawKeys") ?? false,
            RequireSummary = ConfigYamlHelpers.GetOptionalBool(node, "requireSummary") ?? false,
            RequireAuthor = ConfigYamlHelpers.GetOptionalBool(node, "requireAuthor") ?? false,
            RequireOrganization = ConfigYamlHelpers.GetOptionalBool(node, "requireOrganization") ?? false,
            RequireUpdatedAt = ConfigYamlHelpers.GetOptionalBool(node, "requireUpdatedAt") ?? false,
            RequireProvenance = ConfigYamlHelpers.GetOptionalBool(node, "requireProvenance") ?? false,
            RequireReviewedAt = ConfigYamlHelpers.GetOptionalBool(node, "requireReviewedAt") ?? false,
            RequireMediaAlt = ConfigYamlHelpers.GetOptionalBool(node, "requireMediaAlt") ?? true,
            RequireMediaDescription = ConfigYamlHelpers.GetOptionalBool(node, "requireMediaDescription") ?? false,
            RequireMediaLicense = ConfigYamlHelpers.GetOptionalBool(node, "requireMediaLicense") ?? false,
            RequireEntityIds = ConfigYamlHelpers.GetOptionalBool(node, "requireEntityIds") ?? false,
            RequireRelationTargets = ConfigYamlHelpers.GetOptionalBool(node, "requireRelationTargets") ?? true
        };
    }

    private static IReadOnlyList<CanonicalFieldMappingConfig>? ReadCanonicalMappings(YamlMappingNode node)
    {
        var seq = ConfigYamlHelpers.GetOptionalSequence(node, "canonicalMappings");
        if (seq is null || seq.Children.Count == 0)
        {
            return null;
        }

        var mappings = new List<CanonicalFieldMappingConfig>();
        foreach (var child in seq.Children.OfType<YamlMappingNode>())
        {
            var canonicalField = ConfigYamlHelpers.GetOptionalString(child, "canonicalField")
                ?? ConfigYamlHelpers.GetOptionalString(child, "field");
            if (string.IsNullOrWhiteSpace(canonicalField))
            {
                continue;
            }

            mappings.Add(new CanonicalFieldMappingConfig
            {
                CanonicalField = canonicalField,
                RawKey = ConfigYamlHelpers.GetOptionalString(child, "rawKey"),
                SemanticType = ConfigYamlHelpers.GetOptionalString(child, "semanticType"),
                Required = ConfigYamlHelpers.GetOptionalBool(child, "required") ?? false
            });
        }

        return mappings.Count == 0 ? null : mappings;
    }

    private static IReadOnlyList<CustomFieldDefinitionConfig>? ReadCustomFields(YamlMappingNode node)
    {
        var seq = ConfigYamlHelpers.GetOptionalSequence(node, "customFields");
        if (seq is null || seq.Children.Count == 0)
        {
            return null;
        }

        var fields = new List<CustomFieldDefinitionConfig>();
        foreach (var child in seq.Children.OfType<YamlMappingNode>())
        {
            var name = ConfigYamlHelpers.GetOptionalString(child, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            fields.Add(new CustomFieldDefinitionConfig
            {
                Name = name,
                FieldType = ConfigYamlHelpers.GetOptionalString(child, "fieldType")
                    ?? ConfigYamlHelpers.GetOptionalString(child, "type")
                    ?? "string",
                Required = ConfigYamlHelpers.GetOptionalBool(child, "required") ?? false,
                SemanticType = ConfigYamlHelpers.GetOptionalString(child, "semanticType"),
                Label = ConfigYamlHelpers.GetOptionalString(child, "label"),
                Format = ConfigYamlHelpers.GetOptionalString(child, "format"),
                Enum = ConfigYamlHelpers.ReadStringList(child, "enum"),
                Min = ConfigYamlHelpers.GetOptionalDouble(child, "min"),
                Max = ConfigYamlHelpers.GetOptionalDouble(child, "max"),
                Default = child.Children.TryGetValue(new YamlScalarNode("default"), out var defaultNode)
                    ? ConfigYamlHelpers.ToObject(defaultNode)
                    : null,
                SourcePolicy = ConfigYamlHelpers.GetOptionalString(child, "sourcePolicy"),
                Reference = ReadReferenceRule(child)
            });
        }

        return fields.Count == 0 ? null : fields;
    }

    private static IReadOnlyList<EntityMappingConfig>? ReadEntityMappings(YamlMappingNode node)
        => ReadSchemaMappings(node, "entityMappings", child =>
        {
            var rawKey = ConfigYamlHelpers.GetOptionalString(child, "rawKey");
            var entityType = ConfigYamlHelpers.GetOptionalString(child, "entityType")
                ?? ConfigYamlHelpers.GetOptionalString(child, "type");
            return string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(entityType)
                ? null
                : new EntityMappingConfig
                {
                    RawKey = rawKey,
                    EntityType = entityType,
                    IdField = ConfigYamlHelpers.GetOptionalString(child, "idField"),
                    NameField = ConfigYamlHelpers.GetOptionalString(child, "nameField"),
                    Required = ConfigYamlHelpers.GetOptionalBool(child, "required") ?? false,
                    Reference = ReadReferenceRule(child)
                };
        });

    private static IReadOnlyList<RelationMappingConfig>? ReadRelationMappings(YamlMappingNode node)
        => ReadSchemaMappings(node, "relationMappings", child =>
        {
            var rawKey = ConfigYamlHelpers.GetOptionalString(child, "rawKey");
            var relationType = ConfigYamlHelpers.GetOptionalString(child, "relationType")
                ?? ConfigYamlHelpers.GetOptionalString(child, "type");
            return string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(relationType)
                ? null
                : new RelationMappingConfig
                {
                    RawKey = rawKey,
                    RelationType = relationType,
                    TargetType = ConfigYamlHelpers.GetOptionalString(child, "targetType"),
                    Required = ConfigYamlHelpers.GetOptionalBool(child, "required") ?? false,
                    Reference = ReadReferenceRule(child)
                };
        });

    private static ContentReferenceRuleConfig? ReadReferenceRule(YamlMappingNode node)
    {
        var refNode = ConfigYamlHelpers.GetOptionalMapping(node, "reference")
            ?? ConfigYamlHelpers.GetOptionalMapping(node, "referenceRule");
        if (refNode is null)
        {
            return null;
        }

        return new ContentReferenceRuleConfig
        {
            TargetType = ConfigYamlHelpers.GetOptionalString(refNode, "targetType"),
            IdField = ConfigYamlHelpers.GetOptionalString(refNode, "idField"),
            LabelField = ConfigYamlHelpers.GetOptionalString(refNode, "labelField")
                ?? ConfigYamlHelpers.GetOptionalString(refNode, "nameField"),
            UrlField = ConfigYamlHelpers.GetOptionalString(refNode, "urlField"),
            Required = ConfigYamlHelpers.GetOptionalBool(refNode, "required") ?? false
        };
    }

    private static IReadOnlyList<T>? ReadSchemaMappings<T>(
        YamlMappingNode node,
        string key,
        Func<YamlMappingNode, T?> read)
    {
        var seq = ConfigYamlHelpers.GetOptionalSequence(node, key);
        if (seq is null || seq.Children.Count == 0)
        {
            return null;
        }

        var values = new List<T>();
        foreach (var child in seq.Children.OfType<YamlMappingNode>())
        {
            var value = read(child);
            if (value is not null)
            {
                values.Add(value);
            }
        }

        return values.Count == 0 ? null : values;
    }

    private static MediaPolicyConfig? ReadMediaPolicy(YamlMappingNode node)
    {
        var mediaNode = ConfigYamlHelpers.GetOptionalMapping(node, "media");
        if (mediaNode is null)
        {
            return null;
        }

        return new MediaPolicyConfig
        {
            RequireAlt = ConfigYamlHelpers.GetOptionalBool(mediaNode, "requireAlt") ?? true,
            RequireDescription = ConfigYamlHelpers.GetOptionalBool(mediaNode, "requireDescription") ?? false,
            RequireLicense = ConfigYamlHelpers.GetOptionalBool(mediaNode, "requireLicense") ?? false,
            AllowedKinds = ConfigYamlHelpers.ReadStringList(mediaNode, "allowedKinds")
        };
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
