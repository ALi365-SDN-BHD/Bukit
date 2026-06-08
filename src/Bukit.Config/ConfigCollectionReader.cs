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
                throw new ConfigException($"site.collections.{keyNode.Value} must be a mapping.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            var paginationNode = ConfigYamlHelpers.GetOptionalMapping(collectionNode, "pagination");
            var outputNode = ConfigYamlHelpers.GetOptionalMapping(collectionNode, "output");
            ThrowIfCollectionSchemaDeclared($"site.collections.{keyNode.Value}.schema", collectionNode);
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
                FilteredLists = ReadFilteredLists(collectionNode)
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
            throw new ConfigException($"Invalid YAML syntax in collections.yaml: {collectionsPath}", ex, DiagnosticCode.ConfigInvalidValue);
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
                throw new ConfigException($"collections.yaml: entry '{keyNode.Value}' must be a mapping.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            var paginationNode = ConfigYamlHelpers.GetOptionalMapping(collectionNode, "pagination");
            var outputNode = ConfigYamlHelpers.GetOptionalMapping(collectionNode, "output");
            ThrowIfCollectionSchemaDeclared($"collections.yaml:{keyNode.Value}.schema", collectionNode);
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
                FilteredLists = ReadFilteredLists(collectionNode)
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
                throw new ConfigException("Each item in filteredLists must be a mapping.", DiagnosticCode.ConfigRequiredFieldMissing);
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

    private static void ThrowIfCollectionSchemaDeclared(string path, YamlMappingNode collectionNode)
    {
        if (!collectionNode.Children.ContainsKey(new YamlScalarNode("schema")))
        {
            return;
        }

        throw new ConfigException(
            $"{path} was removed in Bukit vNext. Move scoped field definitions to content.modelSchema.fieldScopes.<collection>.");
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
                throw new ConfigException("content.sources items must be mappings.", DiagnosticCode.ConfigRequiredFieldMissing);
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
            FieldScopes = ReadFieldScopes(node),
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

        var fields = ReadCustomFieldSequence(seq);
        return fields.Count == 0 ? null : fields;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>? ReadFieldScopes(YamlMappingNode node)
    {
        var scopesNode = ConfigYamlHelpers.GetOptionalMapping(node, "fieldScopes")
            ?? ConfigYamlHelpers.GetOptionalMapping(node, "scopedFields");
        if (scopesNode is null || scopesNode.Children.Count == 0)
        {
            return null;
        }

        var scopes = new Dictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in scopesNode.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                continue;
            }

            if (kv.Value is not YamlSequenceNode fieldsNode)
            {
                throw new ConfigException($"content.modelSchema.fieldScopes.{keyNode.Value} must be a sequence.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            var fields = ReadCustomFieldSequence(fieldsNode);
            if (fields.Count > 0)
            {
                scopes[keyNode.Value.Trim()] = fields;
            }
        }

        return scopes.Count == 0 ? null : scopes;
    }

    private static IReadOnlyList<CustomFieldDefinitionConfig> ReadCustomFieldSequence(YamlSequenceNode seq)
    {
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

        return fields;
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
                    DescriptionField = ConfigYamlHelpers.GetOptionalString(child, "descriptionField"),
                    UrlField = ConfigYamlHelpers.GetOptionalString(child, "urlField"),
                    SameAsField = ConfigYamlHelpers.GetOptionalString(child, "sameAsField"),
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
                    TargetField = ConfigYamlHelpers.GetOptionalString(child, "targetField")
                        ?? ConfigYamlHelpers.GetOptionalString(child, "labelField"),
                    TargetIdField = ConfigYamlHelpers.GetOptionalString(child, "targetIdField")
                        ?? ConfigYamlHelpers.GetOptionalString(child, "idField"),
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
