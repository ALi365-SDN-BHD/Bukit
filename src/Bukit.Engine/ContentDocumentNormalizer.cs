using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class ContentDocumentNormalizer
{
    private static readonly IContentNormalizer Default = new DefaultContentNormalizer();

    internal static ContentDocument ToDocument(RawContentDocument raw, ContentModelSchema? schema = null)
        => Default.Normalize(raw, schema);

    internal static IReadOnlyList<ContentDocument> ToDocuments(IReadOnlyList<RawContentDocument> rawDocuments, ContentModelSchema? schema = null)
        => rawDocuments.Select(raw => ToDocument(raw, schema)).ToArray();
}

internal sealed class DefaultContentNormalizer : IContentNormalizer
{
    public ContentDocument Normalize(RawContentDocument raw, ContentModelSchema? schema = null)
    {
        var fields = ApplySchemaDefaults(
            ApplyCanonicalMappings(raw.CustomFields ?? ToFieldMap(raw.Properties), schema),
            schema);
        var diagnostics = BuildDiagnostics(raw, fields, schema);
        var normalizedRaw = raw with { CustomFields = fields };
        return new ContentDocument(
            CanonicalContentGraphBuilder.ToRecord(normalizedRaw),
            new ContentBodyRef(raw.Body.InlineHtml, raw.Body.BodyKey, raw.Body.Markdown, raw.Body.PlainText),
            ContentRoutePolicy.FromFields(fields),
            ContentPublishPolicy.FromFields(fields),
            fields,
            raw.Source,
            diagnostics);
    }

    private static IReadOnlyDictionary<string, ContentField>? ApplyCanonicalMappings(
        IReadOnlyDictionary<string, ContentField>? fields,
        ContentModelSchema? schema)
    {
        if (fields is null ||
            schema?.CanonicalMappings is null ||
            schema.CanonicalMappings.Count == 0)
        {
            return fields;
        }

        Dictionary<string, ContentField>? projected = null;
        foreach (var mapping in schema.CanonicalMappings.Values)
        {
            if (string.IsNullOrWhiteSpace(mapping.CanonicalField))
            {
                continue;
            }

            var rawKey = string.IsNullOrWhiteSpace(mapping.RawKey)
                ? mapping.CanonicalField
                : mapping.RawKey;
            if (string.Equals(rawKey, mapping.CanonicalField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ContentFieldReader.TryGetField(fields, mapping.CanonicalField, out _))
            {
                continue;
            }

            if (!ContentFieldReader.TryGetField(fields, rawKey, out var rawField))
            {
                continue;
            }

            projected ??= new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);
            projected[mapping.CanonicalField] = rawField;
        }

        return projected ?? fields;
    }

    private static IReadOnlyDictionary<string, ContentField>? ApplySchemaDefaults(
        IReadOnlyDictionary<string, ContentField>? fields,
        ContentModelSchema? schema)
    {
        if (schema is null)
        {
            return fields;
        }

        Dictionary<string, ContentField>? projected = null;
        foreach (var definition in EnumerateDefaultableFields(schema, fields))
        {
            if (definition.Default is null ||
                string.IsNullOrWhiteSpace(definition.Name) ||
                ContentFieldReader.TryGetField(projected ?? fields, definition.Name, out _))
            {
                continue;
            }

            projected ??= fields is null
                ? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);
            projected[definition.Name] = ToContentField(definition.FieldType, definition.Default);
        }

        return projected ?? fields;
    }

    private static IEnumerable<CustomFieldDefinition> EnumerateDefaultableFields(
        ContentModelSchema schema,
        IReadOnlyDictionary<string, ContentField>? fields)
    {
        foreach (var definition in schema.CustomFields?.Values ?? Array.Empty<CustomFieldDefinition>())
        {
            yield return definition;
        }

        var collection = ContentFieldReader.GetText(fields, "collection")
            ?? ContentFieldReader.GetText(fields, "type");
        if (!string.IsNullOrWhiteSpace(collection) &&
            schema.CollectionFields?.TryGetValue(collection, out var collectionFields) is true)
        {
            foreach (var definition in collectionFields)
            {
                yield return definition;
            }
        }
    }

    private static IReadOnlyDictionary<string, ContentField>? ToFieldMap(IReadOnlyDictionary<string, RawContentValue>? properties)
    {
        if (properties is null)
        {
            return null;
        }

        return properties.ToDictionary(
            x => x.Key,
            x => new ContentField(x.Value.Kind, x.Value.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static ContentField ToContentField(string? fieldType, object value)
    {
        var type = (fieldType ?? "string").Trim().ToLowerInvariant();
        return type switch
        {
            "bool" or "boolean" => new ContentField("bool", value),
            "number" or "int" or "integer" => new ContentField("number", value),
            "date" or "datetime" => new ContentField("date", value),
            "list" or "array" or "string[]" => new ContentField("list", value),
            _ => new ContentField("text", value)
        };
    }

    private static IReadOnlyList<ContentDiagnostic> BuildDiagnostics(
        RawContentDocument raw,
        IReadOnlyDictionary<string, ContentField>? fields,
        ContentModelSchema? schema)
    {
        if (schema is null)
        {
            return Array.Empty<ContentDiagnostic>();
        }

        var diagnostics = new List<ContentDiagnostic>();
        foreach (var mapping in schema.CanonicalMappings?.Values ?? Array.Empty<CanonicalFieldMapping>())
        {
            if (mapping.Required && !ContentFieldReader.TryGetField(fields, mapping.CanonicalField, out _))
            {
                diagnostics.Add(new ContentDiagnostic(
                    "content.required_canonical_field_missing",
                    "error",
                    $"Required canonical field '{mapping.CanonicalField}' is missing.",
                    mapping.CanonicalField,
                    raw.Id));
            }
        }

        foreach (var definition in schema.CustomFields?.Values ?? Array.Empty<CustomFieldDefinition>())
        {
            if (definition.Required && !ContentFieldReader.TryGetField(fields, definition.Name, out _))
            {
                diagnostics.Add(new ContentDiagnostic(
                    "content.required_custom_field_missing",
                    "error",
                    $"Required custom field '{definition.Name}' is missing.",
                    definition.Name,
                    raw.Id));
            }
        }

        var collection = ContentFieldReader.GetText(fields, "collection")
            ?? ContentFieldReader.GetText(fields, "type");
        if (!string.IsNullOrWhiteSpace(collection) &&
            schema.CollectionFields?.TryGetValue(collection, out var collectionFields) is true)
        {
            foreach (var definition in collectionFields)
            {
                if (definition.Required && !ContentFieldReader.TryGetField(fields, definition.Name, out _))
                {
                    diagnostics.Add(new ContentDiagnostic(
                        "content.required_collection_field_missing",
                        "error",
                        $"Required field '{definition.Name}' is missing for collection '{collection}'.",
                        definition.Name,
                        raw.Id));
                }
            }
        }

        if (schema.RejectUnknownRawKeys && raw.Properties is { Count: > 0 })
        {
            var allowed = BuildAllowedRawKeys(schema);
            foreach (var key in raw.Properties.Keys)
            {
                if (!allowed.Contains(key))
                {
                    diagnostics.Add(new ContentDiagnostic(
                        "content.unknown_raw_key",
                        "error",
                        $"Raw key '{key}' is not declared by the content model schema.",
                        key,
                        raw.Id));
                }
            }
        }

        return diagnostics;
    }

    private static HashSet<string> BuildAllowedRawKeys(ContentModelSchema schema)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in schema.CanonicalMappings?.Values ?? Array.Empty<CanonicalFieldMapping>())
        {
            allowed.Add(mapping.CanonicalField);
            allowed.Add(mapping.RawKey ?? mapping.CanonicalField);
        }

        foreach (var definition in schema.CustomFields?.Values ?? Array.Empty<CustomFieldDefinition>())
        {
            allowed.Add(definition.Name);
        }

        foreach (var collectionFields in schema.CollectionFields?.Values ?? Array.Empty<IReadOnlyList<CustomFieldDefinition>>())
        {
            foreach (var definition in collectionFields)
            {
                allowed.Add(definition.Name);
            }
        }

        foreach (var mapping in schema.EntityMappings?.Values ?? Array.Empty<EntityMapping>())
        {
            allowed.Add(mapping.RawKey);
        }

        foreach (var mapping in schema.RelationMappings?.Values ?? Array.Empty<RelationMapping>())
        {
            allowed.Add(mapping.RawKey);
        }

        return allowed;
    }
}
