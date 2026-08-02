using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

public static class ContentModelSchemaProjection
{
    public static ContentModelSchema FromConfig(AppConfig config)
        => ContentModelSchemaFactory.FromConfig(config);

    public static IReadOnlyList<ContentValidationIssue> ValidateDocuments(
        AppConfig config,
        IReadOnlyList<ContentDocument> documents)
    {
        var schema = ContentModelSchemaFactory.FromConfig(config);
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(documents);
        return ContentModelSchemaValidator.Validate(graph, schema)
            .Concat(documents.SelectMany(ToSchemaErrors))
            .ToArray();
    }

    public static IReadOnlySet<string> GetFieldNames(AppConfig config, string? collectionName)
    {
        var schema = ContentModelSchemaFactory.FromConfig(config);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in schema.CustomFields?.Values ?? Array.Empty<CustomFieldDefinition>())
        {
            if (!string.IsNullOrWhiteSpace(definition.Name))
            {
                names.Add(definition.Name.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(collectionName) &&
            schema.FieldScopes?.TryGetValue(collectionName.Trim(), out var scopedFields) is true)
        {
            foreach (var definition in scopedFields)
            {
                if (!string.IsNullOrWhiteSpace(definition.Name))
                {
                    names.Add(definition.Name.Trim());
                }
            }
        }

        return names;
    }

    private static IEnumerable<ContentValidationIssue> ToSchemaErrors(ContentDocument document)
    {
        foreach (var diagnostic in document.Diagnostics.Where(diagnostic =>
                     string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ContentValidationIssue(
                diagnostic.Field ?? string.Empty,
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.SourceId ?? document.Id);
        }
    }
}
