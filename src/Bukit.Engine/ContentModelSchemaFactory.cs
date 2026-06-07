using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class ContentModelSchemaFactory
{
    internal static ContentModelSchema FromConfig(AppConfig config)
    {
        var explicitSchema = config.Content.ModelSchema;
        var canonicalMappings = new Dictionary<string, CanonicalFieldMapping>(StringComparer.OrdinalIgnoreCase);
        var customFields = new Dictionary<string, CustomFieldDefinition>(StringComparer.OrdinalIgnoreCase);
        var collectionFields = new Dictionary<string, IReadOnlyList<CustomFieldDefinition>>(StringComparer.OrdinalIgnoreCase);
        var entityMappings = new Dictionary<string, EntityMapping>(StringComparer.OrdinalIgnoreCase);
        var relationMappings = new Dictionary<string, RelationMapping>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in explicitSchema?.CanonicalMappings ?? Array.Empty<CanonicalFieldMappingConfig>())
        {
            canonicalMappings[mapping.RawKey ?? mapping.CanonicalField] = new CanonicalFieldMapping(
                mapping.CanonicalField,
                mapping.RawKey,
                mapping.SemanticType,
                mapping.Required);
        }

        foreach (var field in explicitSchema?.CustomFields ?? Array.Empty<CustomFieldDefinitionConfig>())
        {
            customFields[field.Name] = new CustomFieldDefinition(
                field.Name,
                field.FieldType,
                field.Required,
                field.SemanticType);
        }

        foreach (var mapping in explicitSchema?.EntityMappings ?? Array.Empty<EntityMappingConfig>())
        {
            entityMappings[mapping.RawKey] = new EntityMapping(
                mapping.RawKey,
                mapping.EntityType,
                mapping.IdField,
                mapping.NameField,
                mapping.Required);
        }

        foreach (var mapping in explicitSchema?.RelationMappings ?? Array.Empty<RelationMappingConfig>())
        {
            relationMappings[mapping.RawKey] = new RelationMapping(
                mapping.RawKey,
                mapping.RelationType,
                mapping.TargetType,
                mapping.Required);
        }

        AddCollectionSchemaProjection(config.Site.Collections, customFields, collectionFields);

        return new ContentModelSchema(
            ContentTypes: explicitSchema?.ContentTypes,
            Statuses: explicitSchema?.Statuses ?? ContentModelSchemaValidator.Default.Statuses,
            ReviewStatuses: explicitSchema?.ReviewStatuses ?? ContentModelSchemaValidator.Default.ReviewStatuses,
            SyncStatuses: explicitSchema?.SyncStatuses ?? ContentModelSchemaValidator.Default.SyncStatuses,
            CanonicalMappings: canonicalMappings.Count == 0 ? null : canonicalMappings,
            CustomFields: customFields.Count == 0 ? null : customFields,
            CollectionFields: collectionFields.Count == 0 ? null : collectionFields,
            EntityMappings: entityMappings.Count == 0 ? null : entityMappings,
            RelationMappings: relationMappings.Count == 0 ? null : relationMappings,
            Media: explicitSchema?.Media is null
                ? null
                : new MediaPolicy(
                    explicitSchema.Media.RequireAlt,
                    explicitSchema.Media.RequireDescription,
                    explicitSchema.Media.RequireLicense,
                    explicitSchema.Media.AllowedKinds),
            RejectUnknownRawKeys: explicitSchema?.RejectUnknownRawKeys ?? false,
            RequireSummary: explicitSchema?.RequireSummary ?? false,
            RequireAuthor: explicitSchema?.RequireAuthor ?? false,
            RequireOrganization: explicitSchema?.RequireOrganization ?? false,
            RequireUpdatedAt: explicitSchema?.RequireUpdatedAt ?? false,
            RequireProvenance: explicitSchema?.RequireProvenance ?? false,
            RequireReviewedAt: explicitSchema?.RequireReviewedAt ?? false,
            RequireMediaAlt: explicitSchema?.RequireMediaAlt ?? ContentModelSchemaValidator.Default.RequireMediaAlt,
            RequireMediaDescription: explicitSchema?.RequireMediaDescription ?? false,
            RequireMediaLicense: explicitSchema?.RequireMediaLicense ?? false,
            RequireEntityIds: explicitSchema?.RequireEntityIds ?? false,
            RequireRelationTargets: explicitSchema?.RequireRelationTargets ?? ContentModelSchemaValidator.Default.RequireRelationTargets);
    }

    private static void AddCollectionSchemaProjection(
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        Dictionary<string, CustomFieldDefinition> customFields,
        Dictionary<string, IReadOnlyList<CustomFieldDefinition>> collectionFields)
    {
        if (collections is null)
        {
            return;
        }

        foreach (var (collectionName, collection) in collections)
        {
            var projectedFields = new List<CustomFieldDefinition>();
            foreach (var field in collection.Schema ?? Array.Empty<SchemaFieldDefinition>())
            {
                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    continue;
                }

                var projected = new CustomFieldDefinition(
                    field.Name,
                    field.Type,
                    field.Required,
                    SemanticType: field.Format);
                projectedFields.Add(projected);

                customFields.TryAdd(field.Name, projected with { Required = false });
            }

            if (projectedFields.Count > 0)
            {
                collectionFields[collectionName] = projectedFields;
            }
        }
    }
}
