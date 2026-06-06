using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.Normalization;

public sealed class ContentNormalizer : IContentNormalizer
{
    private static readonly HashSet<string> BuiltInKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "collection",
        "language",
        "summary",
        "description",
        "excerpt",
        "tags",
        "categories",
        "sections",
        "author",
        "authors",
        "organization",
        "org",
        "company",
        "owner",
        "owners",
        "reviewer",
        "reviewers",
        "updatedAt",
        "updated",
        "modified",
        "update_time",
        "last_edited_time",
        "expiresAt",
        "expires_at",
        "expires",
        "reviewedAt",
        "originalSource",
        "original_url",
        "source_url",
        "citations",
        "references",
        "credibilityScore",
        "credibility_score",
        "trust_score",
        "reviewStatus",
        "review_status",
        "qualityFlags",
        "quality_flags",
        "products",
        "services",
        "places",
        "people",
        "companies",
        "relations.translationOf",
        "relations.relatedTo",
        "image",
        "image_alt",
        "image_caption",
        "image_description",
        "image_license",
        "draft",
        "noindex",
        "nofollow",
        "excludeFromFeed",
        "excludeFromSearch",
        "excludeFromSitemap",
        "isDataModule",
        "route.url",
        "route.outputPath",
        "route.template",
        "route.permalinkPattern",
        "route.listGroup"
    };

    public ContentDocument Normalize(RawContentDocument raw, ContentModelSchema schema)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(schema);

        var diagnostics = BuildDiagnostics(raw, schema);
        var type = GetText(raw, schema, "type") ?? raw.SourceKind;
        var collection = GetText(raw, schema, "collection") ?? type;
        var language = GetText(raw, schema, "language") ?? "und";
        var draft = GetBool(raw, schema, "draft");
        var entities = BuildEntities(raw);
        var relations = BuildRelations(raw);
        var media = BuildMedia(raw);

        var record = new ContentRecord(
            Identity: new ContentIdentity(
                raw.SourceId,
                raw.Slug ?? raw.SourceId,
                raw.SourceId,
                type,
                draft ? "draft" : "published"),
            Presentation: new ContentPresentation(
                raw.Title,
                GetText(raw, schema, "summary", "description", "excerpt"),
                raw.Body.InlineHtml,
                language,
                Array.Empty<string>()),
            Classification: new ContentClassification(
                type,
                collection,
                GetTextList(raw, schema, "sections", "categories"),
                MergeLists(GetTextList(raw, schema, "tags"), GetTextList(raw, schema, "categories"))),
            Ownership: new ContentOwnership(
                GetText(raw, schema, "author", "authors"),
                GetText(raw, schema, "organization", "org", "company"),
                GetText(raw, schema, "owner", "owners"),
                GetText(raw, schema, "reviewer", "reviewers")),
            Lifecycle: new ContentLifecycle(
                raw.PublishedAt ?? DateTimeOffset.MinValue,
                GetDate(raw, schema, "updatedAt", "updated", "modified", "update_time", "last_edited_time"),
                GetDate(raw, schema, "expiresAt", "expires_at", "expires"),
                GetDate(raw, schema, "reviewedAt")),
            Provenance: new ProvenanceRecord(
                raw.Source.Provider,
                GetText(raw, schema, "originalSource", "original_url", "source_url") ?? raw.Source.ExternalUrl?.ToString(),
                GetTextList(raw, schema, "citations"),
                GetTextList(raw, schema, "references"),
                raw.Source.SyncStatus),
            Trust: new TrustMetadata(
                GetDouble(raw, schema, "credibilityScore", "credibility_score", "trust_score"),
                GetText(raw, schema, "reviewStatus", "review_status") ?? "unreviewed",
                GetTextList(raw, schema, "qualityFlags", "quality_flags")),
            Entities: entities,
            Relations: relations,
            Media: media);

        return new ContentDocument(
            record,
            new ContentBodyRef(raw.Body.InlineHtml, raw.Body.BodyKey, raw.Body.Markdown, raw.Body.PlainText),
            new ContentRoutePolicy(
                GetText(raw, "route.url"),
                GetText(raw, "route.outputPath"),
                GetText(raw, "route.template"),
                GetText(raw, "route.permalinkPattern"),
                GetText(raw, "route.listGroup")),
            new ContentPublishPolicy(
                draft,
                GetBool(raw, schema, "noindex"),
                GetBool(raw, schema, "nofollow"),
                GetBool(raw, schema, "excludeFromFeed"),
                GetBool(raw, schema, "excludeFromSearch"),
                GetBool(raw, schema, "excludeFromSitemap"),
                GetBool(raw, schema, "isDataModule")),
            raw.CustomFields,
            diagnostics);
    }

    private static IReadOnlyList<EntityRecord> BuildEntities(RawContentDocument raw)
    {
        var entities = new List<EntityRecord>();
        AddEntities(entities, "product", GetTextList(raw, "products"));
        AddEntities(entities, "service", GetTextList(raw, "services"));
        AddEntities(entities, "place", GetTextList(raw, "places"));
        AddEntities(entities, "person", GetTextList(raw, "people"));
        AddEntities(entities, "company", GetTextList(raw, "companies"));
        return entities;
    }

    private static void AddEntities(List<EntityRecord> entities, string type, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            entities.Add(new EntityRecord(type, name));
        }
    }

    private static IReadOnlyList<ContentRelation> BuildRelations(RawContentDocument raw)
    {
        var relations = new List<ContentRelation>();
        AddRelations(relations, "translation-of", GetTextList(raw, "relations.translationOf"));
        AddRelations(relations, "related-to", GetTextList(raw, "relations.relatedTo"));
        return relations;
    }

    private static void AddRelations(List<ContentRelation> relations, string type, IReadOnlyList<string> targets)
    {
        foreach (var target in targets)
        {
            relations.Add(new ContentRelation(type, target));
        }
    }

    private static IReadOnlyList<MediaAsset> BuildMedia(RawContentDocument raw)
    {
        var image = GetText(raw, "image");
        if (string.IsNullOrWhiteSpace(image))
        {
            return Array.Empty<MediaAsset>();
        }

        return
        [
            new MediaAsset(
                "image",
                image,
                GetText(raw, "image_alt"),
                GetText(raw, "image_caption"),
                GetText(raw, "image_description"),
                GetText(raw, "image_license"))
        ];
    }

    private static IReadOnlyList<ContentDiagnostic> BuildDiagnostics(RawContentDocument raw, ContentModelSchema schema)
    {
        var allowedSchemaKeys = new HashSet<string>(schema.CustomFields.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in schema.CanonicalMappings.Values)
        {
            allowedSchemaKeys.Add(mapping.Source);
        }

        return raw.Properties.Keys
            .Where(key => !BuiltInKeys.Contains(key) && !allowedSchemaKeys.Contains(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Select(key => new ContentDiagnostic(
                "content.unknown_raw_key",
                "error",
                $"Unknown raw content key '{key}'. Declare it as a custom field or map it to a canonical field.",
                key,
                raw.SourceId))
            .ToArray();
    }

    private static string? GetText(RawContentDocument raw, string key)
    {
        if (!raw.Properties.TryGetValue(key, out var value) || value.Value is null)
        {
            return null;
        }

        var text = value.Value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static IReadOnlyList<string> GetTextList(RawContentDocument raw, string key)
    {
        if (!raw.Properties.TryGetValue(key, out var value) || value.Value is null)
        {
            return Array.Empty<string>();
        }

        return value.Value switch
        {
            string text => string.IsNullOrWhiteSpace(text) ? Array.Empty<string>() : [text.Trim()],
            IEnumerable<string> strings => strings
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray(),
            IEnumerable<object> objects => objects
                .Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray(),
            _ => Array.Empty<string>()
        };
    }

    private static bool GetBool(RawContentDocument raw, string key)
    {
        if (!raw.Properties.TryGetValue(key, out var value) || value.Value is null)
        {
            return false;
        }

        return value.Value switch
        {
            bool boolean => boolean,
            string text => bool.TryParse(text, out var parsed) && parsed,
            _ => false
        };
    }

    private static DateTimeOffset? GetDate(RawContentDocument raw, string key)
    {
        var text = GetText(raw, key);
        return DateTimeOffset.TryParse(text, out var parsed) ? parsed : null;
    }

    private static double? GetDouble(RawContentDocument raw, string key)
    {
        if (!raw.Properties.TryGetValue(key, out var value) || value.Value is null)
        {
            return null;
        }

        return value.Value switch
        {
            double number => number,
            float number => number,
            int number => number,
            long number => number,
            string text when double.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static string? GetText(RawContentDocument raw, ContentModelSchema schema, string target, params string[] aliases)
    {
        foreach (var key in CandidateKeys(schema, target, aliases))
        {
            var text = GetText(raw, key);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetTextList(RawContentDocument raw, ContentModelSchema schema, string target, params string[] aliases)
    {
        foreach (var key in CandidateKeys(schema, target, aliases))
        {
            var list = GetTextList(raw, key);
            if (list.Count > 0)
            {
                return list;
            }
        }

        return Array.Empty<string>();
    }

    private static bool GetBool(RawContentDocument raw, ContentModelSchema schema, string target, params string[] aliases)
    {
        foreach (var key in CandidateKeys(schema, target, aliases))
        {
            if (raw.Properties.ContainsKey(key))
            {
                return GetBool(raw, key);
            }
        }

        return false;
    }

    private static DateTimeOffset? GetDate(RawContentDocument raw, ContentModelSchema schema, string target, params string[] aliases)
    {
        foreach (var key in CandidateKeys(schema, target, aliases))
        {
            var date = GetDate(raw, key);
            if (date is not null)
            {
                return date;
            }
        }

        return null;
    }

    private static double? GetDouble(RawContentDocument raw, ContentModelSchema schema, string target, params string[] aliases)
    {
        foreach (var key in CandidateKeys(schema, target, aliases))
        {
            var number = GetDouble(raw, key);
            if (number is not null)
            {
                return number;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateKeys(ContentModelSchema schema, string target, IReadOnlyList<string> aliases)
    {
        yield return target;

        foreach (var mapping in schema.CanonicalMappings.Values)
        {
            if (string.Equals(mapping.Target, target, StringComparison.OrdinalIgnoreCase))
            {
                yield return mapping.Source;
            }
        }

        foreach (var alias in aliases)
        {
            yield return alias;
        }
    }

    private static IReadOnlyList<string> MergeLists(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        if (first.Count == 0)
        {
            return second;
        }

        if (second.Count == 0)
        {
            return first;
        }

        return first.Concat(second).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
