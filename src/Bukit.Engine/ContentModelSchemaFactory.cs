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
        var fieldScopes = new Dictionary<string, IReadOnlyList<CustomFieldDefinition>>(StringComparer.OrdinalIgnoreCase);
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
                field.SemanticType,
                field.Label,
                field.Format,
                field.Enum,
                field.Min,
                field.Max,
                field.Default,
                field.SourcePolicy,
                ToReferenceRule(field.Reference));
        }

        foreach (var (scope, fields) in explicitSchema?.FieldScopes ?? new Dictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>(StringComparer.OrdinalIgnoreCase))
        {
            var scopedFields = fields
                .Where(field => !string.IsNullOrWhiteSpace(field.Name))
                .Select(field => new CustomFieldDefinition(
                    field.Name,
                    field.FieldType,
                    field.Required,
                    field.SemanticType,
                    field.Label,
                    field.Format,
                    field.Enum,
                    field.Min,
                    field.Max,
                    field.Default,
                    field.SourcePolicy,
                    ToReferenceRule(field.Reference)))
                .ToArray();

            if (!string.IsNullOrWhiteSpace(scope) && scopedFields.Length > 0)
            {
                fieldScopes[scope] = scopedFields;
            }
        }

        foreach (var mapping in explicitSchema?.EntityMappings ?? Array.Empty<EntityMappingConfig>())
        {
            entityMappings[mapping.RawKey] = new EntityMapping(
                mapping.RawKey,
                mapping.EntityType,
                mapping.IdField,
                mapping.NameField,
                mapping.Required,
                ToReferenceRule(mapping.Reference),
                mapping.DescriptionField,
                mapping.UrlField,
                mapping.SameAsField);
        }

        foreach (var mapping in explicitSchema?.RelationMappings ?? Array.Empty<RelationMappingConfig>())
        {
            relationMappings[mapping.RawKey] = new RelationMapping(
                mapping.RawKey,
                mapping.RelationType,
                mapping.TargetType,
                mapping.Required,
                ToReferenceRule(mapping.Reference),
                mapping.TargetField,
                mapping.TargetIdField);
        }

        return new ContentModelSchema(
            ContentTypes: explicitSchema?.ContentTypes,
            Statuses: explicitSchema?.Statuses ?? ContentModelSchemaValidator.Default.Statuses,
            ReviewStatuses: explicitSchema?.ReviewStatuses ?? ContentModelSchemaValidator.Default.ReviewStatuses,
            SyncStatuses: explicitSchema?.SyncStatuses ?? ContentModelSchemaValidator.Default.SyncStatuses,
            CanonicalMappings: canonicalMappings.Count == 0 ? null : canonicalMappings,
            CustomFields: customFields.Count == 0 ? null : customFields,
            FieldScopes: fieldScopes.Count == 0 ? null : fieldScopes,
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

    private static ContentReferenceRule? ToReferenceRule(ContentReferenceRuleConfig? config)
        => config is null
            ? null
            : new ContentReferenceRule(
                config.TargetType,
                config.IdField,
                config.LabelField,
                config.UrlField,
                config.Required);
}
