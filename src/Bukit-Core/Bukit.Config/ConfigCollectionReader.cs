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
                ListTitle = ConfigYamlHelpers.GetOptionalString(collectionNode, "listTitle"),
                ListDescription = ConfigYamlHelpers.GetOptionalString(collectionNode, "listDescription"),
                ListTemplate = ConfigYamlHelpers.GetOptionalString(collectionNode, "listTemplate"),
                SchemaFailMode = ConfigYamlHelpers.GetOptionalString(collectionNode, "schemaFailMode"),
                Pagination = new CollectionPaginationConfig
                {
                    Enabled = paginationNode is not null && (ConfigYamlHelpers.GetOptionalBool(paginationNode, "enabled") ?? false),
                    PageSize = paginationNode is null ? 10 : ConfigYamlHelpers.GetOptionalIntStrict(paginationNode, "pageSize") ?? 10,
                    UrlPattern = paginationNode is null ? "page/:num/" : ConfigYamlHelpers.GetOptionalString(paginationNode, "urlPattern") ?? "page/:num/",
                    FirstPageUsesListRoute = paginationNode is null || (ConfigYamlHelpers.GetOptionalBool(paginationNode, "firstPageUsesListRoute") ?? true)
                },
                Output = new CollectionOutputConfig
                {
                    Rss = outputNode is null ? true : ConfigYamlHelpers.GetOptionalBool(outputNode, "rss") ?? true,
                    Sitemap = outputNode is null ? true : ConfigYamlHelpers.GetOptionalBool(outputNode, "sitemap") ?? true,
                    Archive = outputNode is not null && (ConfigYamlHelpers.GetOptionalBool(outputNode, "archive") ?? false),
                    FeedPath = outputNode is null ? null : ConfigYamlHelpers.GetOptionalString(outputNode, "feedPath"),
                    FeedTitle = outputNode is null ? null : ConfigYamlHelpers.GetOptionalString(outputNode, "feedTitle"),
                    FeedDescription = outputNode is null ? null : ConfigYamlHelpers.GetOptionalString(outputNode, "feedDescription"),
                    ArchiveDetail = outputNode is null ? null : ReadArchiveDetail(outputNode)
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
                Operator = ConfigYamlHelpers.GetOptionalString(filterNode, "operator") ?? "equals",
                Value = ConfigYamlHelpers.GetOptionalString(filterNode, "value"),
                Values = ConfigYamlHelpers.ReadStringList(filterNode, "values"),
                ListRoute = ConfigYamlHelpers.GetRequiredString(filterNode, "listRoute"),
                Title = ConfigYamlHelpers.GetOptionalString(filterNode, "title"),
                Description = ConfigYamlHelpers.GetOptionalString(filterNode, "description"),
                ListTemplate = ConfigYamlHelpers.GetOptionalString(filterNode, "listTemplate"),
                PageSize = ConfigYamlHelpers.GetOptionalIntStrict(filterNode, "pageSize"),
                UrlPattern = ConfigYamlHelpers.GetOptionalString(filterNode, "urlPattern"),
                EmptyBehavior = ConfigYamlHelpers.GetOptionalString(filterNode, "emptyBehavior") ?? "render"
            });
        }

        return filteredLists.Count == 0 ? null : filteredLists;
    }

    private static ArchiveDetailConfig? ReadArchiveDetail(YamlMappingNode outputNode)
    {
        var archiveDetailNode = ConfigYamlHelpers.GetOptionalMapping(outputNode, "archiveDetail");
        if (archiveDetailNode is null)
        {
            return null;
        }

        return new ArchiveDetailConfig
        {
            Depth = ConfigYamlHelpers.GetOptionalString(archiveDetailNode, "depth") ?? "monthly",
            Template = ConfigYamlHelpers.GetOptionalString(archiveDetailNode, "template"),
            RoutePrefix = ConfigYamlHelpers.GetOptionalString(archiveDetailNode, "routePrefix")
        };
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

    internal static RouteMetadataConfig? ReadRouteMetadata(YamlMappingNode contentNode)
    {
        var node = ConfigYamlHelpers.GetOptionalMapping(contentNode, "routeMetadata");
        if (node is null)
        {
            return null;
        }

        var source = ConfigYamlHelpers.GetOptionalString(node, "source");
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ConfigException("content.routeMetadata.source is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return new RouteMetadataConfig
        {
            Source = source,
            RouteField = ConfigYamlHelpers.GetOptionalString(node, "routeField") ?? "route",
            TitleField = ConfigYamlHelpers.GetOptionalString(node, "titleField") ?? "title",
            SummaryField = ConfigYamlHelpers.GetOptionalString(node, "summaryField") ?? "summary",
            SeoTitleField = ConfigYamlHelpers.GetOptionalString(node, "seoTitleField") ?? "seo_title",
            SeoDescriptionField = ConfigYamlHelpers.GetOptionalString(node, "seoDescriptionField") ?? "seo_description",
            RequiredRoutes = ConfigYamlHelpers.ReadStringList(node, "requiredRoutes") ?? Array.Empty<string>()
        };
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
            var canonicalField = ConfigYamlHelpers.GetOptionalString(child, "canonicalField");
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
        var scopesNode = ConfigYamlHelpers.GetOptionalMapping(node, "fieldScopes");
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
                FieldType = ConfigYamlHelpers.GetOptionalString(child, "fieldType") ?? "string",
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
            var entityType = ConfigYamlHelpers.GetOptionalString(child, "entityType");
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
            var relationType = ConfigYamlHelpers.GetOptionalString(child, "relationType");
            return string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(relationType)
                ? null
                : new RelationMappingConfig
                {
                    RawKey = rawKey,
                    RelationType = relationType,
                    TargetType = ConfigYamlHelpers.GetOptionalString(child, "targetType"),
                    TargetField = ConfigYamlHelpers.GetOptionalString(child, "targetField"),
                    TargetIdField = ConfigYamlHelpers.GetOptionalString(child, "targetIdField"),
                    Required = ConfigYamlHelpers.GetOptionalBool(child, "required") ?? false,
                    Reference = ReadReferenceRule(child)
                };
        });

    private static ContentReferenceRuleConfig? ReadReferenceRule(YamlMappingNode node)
    {
        var refNode = ConfigYamlHelpers.GetOptionalMapping(node, "reference");
        if (refNode is null)
        {
            return null;
        }

        return new ContentReferenceRuleConfig
        {
            TargetType = ConfigYamlHelpers.GetOptionalString(refNode, "targetType"),
            IdField = ConfigYamlHelpers.GetOptionalString(refNode, "idField"),
            LabelField = ConfigYamlHelpers.GetOptionalString(refNode, "labelField"),
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
        var dataIndex = ReadDataIndex(sourceNode);
        if (type.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentSourceConfig
            {
                Type = "notion",
                Name = name,
                Mode = mode,
                Collection = collection,
                AddToCollections = addToCollections,
                Notion = SiteDefaultsApplier.ReadNotionConfigFrom(sourceNode),
                DataIndex = dataIndex
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
                Markdown = SiteDefaultsApplier.ReadMarkdownConfigFrom(sourceNode),
                DataIndex = dataIndex
            };
        }

        return new ContentSourceConfig
        {
            Type = type,
            Name = name,
            Mode = mode,
            Collection = collection,
            AddToCollections = addToCollections,
            DataIndex = dataIndex
        };
    }

    private static DataIndexConfig? ReadDataIndex(YamlMappingNode sourceNode)
    {
        var node = ConfigYamlHelpers.GetOptionalMapping(sourceNode, "dataIndex");
        if (node is null)
        {
            return null;
        }

        return new DataIndexConfig
        {
            ScopeField = ConfigYamlHelpers.GetOptionalString(node, "scopeField") ?? "scope",
            KeyField = ConfigYamlHelpers.GetOptionalString(node, "keyField") ?? "key",
            ValueField = ConfigYamlHelpers.GetOptionalString(node, "valueField") ?? "value",
            ValueTypeField = ConfigYamlHelpers.GetOptionalString(node, "valueTypeField") ?? "value_type",
            RequiredKeys = ConfigYamlHelpers.ReadStringList(node, "requiredKeys")
        };
    }
}
