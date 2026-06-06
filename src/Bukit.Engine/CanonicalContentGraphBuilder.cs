using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class CanonicalContentGraphBuilder
{
    internal static CanonicalContentGraph BuildFromDocuments(IReadOnlyList<ContentDocument> documents)
    {
        if (documents.Count == 0)
        {
            return CanonicalContentGraph.Empty;
        }

        var records = new List<ContentRecord>(documents.Count);
        var entities = new List<EntityRecord>();
        var relations = new List<ContentRelation>();

        foreach (var document in documents)
        {
            var record = document.Record;
            records.Add(record);
            entities.AddRange(record.Entities);
            relations.AddRange(record.Relations);
        }

        return new CanonicalContentGraph(
            records,
            entities
                .GroupBy(x => $"{x.Type}:{x.Id ?? x.Name}", StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToArray(),
            relations
                .GroupBy(x => $"{x.Type}:{x.TargetType}:{x.TargetId ?? x.Target}", StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToArray(),
            documents);
    }

    internal static ContentRecord ToRecord(RawContentDocument raw)
        => ToRecord(new ContentRecordSource(
            raw.Id,
            raw.Title,
            raw.Slug,
            raw.PublishAt,
            raw.ContentHtml,
            raw.Fields));

    private static ContentRecord ToRecord(ContentRecordSource source)
    {
        var language = FirstText(source, "language") ?? "und";
        var defaultType = string.Equals(FirstText(source, "sourceMode"), "data", StringComparison.OrdinalIgnoreCase)
            ? "module"
            : "page";
        var type = FirstText(source, "type")
            ?? FirstText(source, "collection")
            ?? defaultType;
        var collection = FirstText(source, "collection") ?? type;
        var status = ResolveStatus(source);
        var citations = ExtractCitationUrls(source);
        var references = FirstList(source, "references") ?? Array.Empty<string>();
        var translations = ExtractTranslations(source);
        var tags = MergeLists(FirstList(source, "tags"), FirstList(source, "categories"));
        var sections = FirstList(source, "sections")
            ?? FirstList(source, "categories")
            ?? Array.Empty<string>();
        var entities = ExtractEntities(source);
        var relations = ExtractRelations(source, translations, entities);
        var media = ExtractMedia(source);
        var authors = FirstList(source, "authors");
        var owners = FirstList(source, "owners");
        var reviewers = FirstList(source, "reviewers");

        return new ContentRecord(
            new ContentIdentity(
                source.Id,
                source.Slug,
                FirstText(source, "i18nKey") ?? source.Slug,
                type,
                status),
            new ContentPresentation(
                source.Title,
                FirstText(source, "summary")
                    ?? FirstText(source, "description")
                    ?? FirstText(source, "excerpt"),
                source.ContentHtml,
                language,
                translations),
            new ContentClassification(type, collection, sections, tags),
            new ContentOwnership(
                FirstText(source, "author") ?? authors?.FirstOrDefault(),
                FirstText(source, "organization") ?? FirstText(source, "org") ?? FirstText(source, "company"),
                FirstText(source, "owner") ?? owners?.FirstOrDefault(),
                FirstText(source, "reviewer") ?? reviewers?.FirstOrDefault()),
            new ContentLifecycle(
                source.PublishAt,
                FirstDate(source, "updated") ?? FirstDate(source, "modified") ?? FirstDate(source, "update_time") ?? FirstDate(source, "last_edited_time"),
                FirstDate(source, "expires_at") ?? FirstDate(source, "expires"),
                ParseGeoMeta(source).DateReviewed),
            new ProvenanceRecord(
                FirstText(source, "source"),
                FirstText(source, "original_url") ?? FirstText(source, "source_url") ?? FirstText(source, "url"),
                citations,
                references,
                FirstText(source, "sync_status")),
            new TrustMetadata(
                FirstDouble(source, "credibility_score") ?? FirstDouble(source, "trust_score"),
                FirstText(source, "review_status") ?? status,
                FirstList(source, "quality_flags") ?? Array.Empty<string>()),
            entities,
            relations,
            media);
    }

    private static string ResolveStatus(ContentRecordSource source)
    {
        if (ContentFieldReader.GetBool(source.Fields, "draft") is true)
        {
            return "draft";
        }

        return FirstText(source, "status") ?? "published";
    }

    private static IReadOnlyList<string> ExtractTranslations(ContentRecordSource source)
    {
        var translations = FirstList(source, "translations");
        if (translations is { Count: > 0 })
        {
            return translations;
        }

        return ContentFieldReader.TryGetI18nKey(source.Fields, out var key)
            ? new[] { key }
            : Array.Empty<string>();
    }

    private static IReadOnlyList<string> ExtractCitationUrls(ContentRecordSource source)
    {
        var geo = ParseGeoMeta(source);
        if (geo.Citations is { Count: > 0 })
        {
            return geo.Citations.Select(x => x.Url).ToArray();
        }

        return FirstList(source, "citations") ?? Array.Empty<string>();
    }

    private static IReadOnlyList<EntityRecord> ExtractEntities(ContentRecordSource source)
    {
        var entities = new List<EntityRecord>();

        if (ContentFieldReader.TryGetField(source.Fields, "entities", out var entitiesField) &&
            entitiesField.Value is IEnumerable<object> entityItems)
        {
            foreach (var raw in entityItems)
            {
                if (raw is IReadOnlyDictionary<string, object> map)
                {
                    var name = ReadMapString(map, "name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    entities.Add(new EntityRecord(
                        ReadMapString(map, "type") ?? "thing",
                        name,
                        ReadMapString(map, "description"),
                        ReadMapString(map, "id"),
                        ReadMapString(map, "url"),
                        ReadMapList(map, "sameAs") ?? ReadMapList(map, "same_as")));
                }
            }
        }

        AppendNamedEntities(entities, "product", FirstList(source, "products"));
        AppendNamedEntities(entities, "service", FirstList(source, "services"));
        AppendNamedEntities(entities, "place", FirstList(source, "places"));
        AppendNamedEntities(entities, "person", FirstList(source, "people"));
        AppendNamedEntities(entities, "person", FirstList(source, "authors"));
        AppendNamedEntities(entities, "company", FirstList(source, "companies"));

        if (source.Fields is not null)
        {
            foreach (var (key, field) in source.Fields)
            {
                if (field.Value is IEnumerable<Dictionary<string, object?>> links &&
                    key.EndsWith("_links", StringComparison.OrdinalIgnoreCase))
                {
                    var inferredType = InferEntityTypeFromKey(key);
                    foreach (var link in links)
                    {
                        var title = ReadObjectString(link, "title") ?? ReadObjectString(link, "id");
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            continue;
                        }

                        entities.Add(new EntityRecord(
                            inferredType,
                            title,
                            null,
                            ReadObjectString(link, "id"),
                            ReadObjectString(link, "url")));
                    }
                }
            }
        }

        return entities
            .GroupBy(x => $"{x.Type}:{x.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    private static IReadOnlyList<ContentRelation> ExtractRelations(
        ContentRecordSource source,
        IReadOnlyList<string> translations,
        IReadOnlyList<EntityRecord> entities)
    {
        var relations = new List<ContentRelation>();

        foreach (var translation in translations)
        {
            relations.Add(new ContentRelation("translation-of", translation, "content", translation));
        }

        foreach (var related in FirstList(source, "related_to") ?? Array.Empty<string>())
        {
            relations.Add(new ContentRelation("related-to", related, "content", related));
        }

        if (source.Fields is not null)
        {
            foreach (var (key, field) in source.Fields)
            {
                if (field.Value is IEnumerable<Dictionary<string, object?>> links &&
                    key.EndsWith("_links", StringComparison.OrdinalIgnoreCase))
                {
                    var relationType = key[..^"_links".Length].Replace('_', '-');
                    foreach (var link in links)
                    {
                        var target = ReadObjectString(link, "title") ?? ReadObjectString(link, "id");
                        if (!string.IsNullOrWhiteSpace(target))
                        {
                            relations.Add(new ContentRelation(
                                relationType,
                                target,
                                InferEntityTypeFromKey(key),
                                ReadObjectString(link, "id")));
                        }
                    }
                }
            }
        }

        foreach (var entity in entities)
        {
            relations.Add(new ContentRelation("mentions", entity.Name, entity.Type, entity.Id));
        }

        return relations
            .GroupBy(x => $"{x.Type}:{x.Target}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    private static IReadOnlyList<MediaAsset> ExtractMedia(ContentRecordSource source)
    {
        var media = new List<MediaAsset>();
        AddMedia(media, "image", FirstText(source, "image"), source, "image");
        AddMedia(media, "image", FirstText(source, "cover"), source, "cover");
        AddMedia(media, "video", FirstText(source, "video"), source, "video");
        AddMedia(media, "file", FirstText(source, "attachment"), source, "attachment");
        if (source.Fields is not null)
        {
            foreach (var (key, field) in source.Fields)
            {
                var kind = InferMediaKind(key);
                if (kind is null)
                {
                    continue;
                }

                if (field.Type.Equals("file", StringComparison.OrdinalIgnoreCase) && field.Value is not null)
                {
                    AddMedia(media, kind, field.Value.ToString(), source, key);
                    continue;
                }

                if (field.Type.Equals("files", StringComparison.OrdinalIgnoreCase) &&
                    field.Value is IEnumerable<string> files)
                {
                    foreach (var file in files)
                    {
                        AddMedia(media, kind, file, source, key);
                    }
                }
            }
        }

        return media
            .GroupBy(x => $"{x.Kind}:{x.Url}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    private static void AddMedia(List<MediaAsset> media, string kind, string? url, ContentRecordSource source, string key)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        media.Add(new MediaAsset(
            kind,
            url,
            FirstText(source, key + "_alt"),
            FirstText(source, key + "_caption"),
            FirstText(source, key + "_description") ?? FirstText(source, key + "_desc"),
            FirstText(source, key + "_license")));
    }

    private static void AppendNamedEntities(List<EntityRecord> target, string type, IReadOnlyList<string>? names)
    {
        if (names is null)
        {
            return;
        }

        foreach (var name in names)
        {
            target.Add(new EntityRecord(type, name));
        }
    }

    private static IReadOnlyList<string> MergeLists(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
    {
        var result = new List<string>();
        if (first is not null)
        {
            result.AddRange(first);
        }

        if (second is not null)
        {
            foreach (var value in second)
            {
                if (!result.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(value);
                }
            }
        }

        return result;
    }

    private static string? ReadMapString(IReadOnlyDictionary<string, object> map, string key)
    {
        return map.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
    }

    private static IReadOnlyList<string>? ReadMapList(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value))
        {
            return null;
        }

        return ContentFieldReader.ToTextList(value);
    }

    private static string? FirstText(ContentRecordSource source, string key)
        => ContentFieldReader.GetText(source.Fields, key);

    private static IReadOnlyList<string>? FirstList(ContentRecordSource source, string key)
        => ContentFieldReader.GetTextList(source.Fields, key);

    private static DateTimeOffset? FirstDate(ContentRecordSource source, string key)
        => ContentFieldReader.GetDate(source.Fields, key);

    private static double? FirstDouble(ContentRecordSource source, string key)
        => ContentFieldReader.GetNumber(source.Fields, key);

    private static SeoGeoMetaParser.ParsedGeoMeta ParseGeoMeta(ContentRecordSource source)
        => SeoGeoMetaParser.ParseGeoMeta(source.Fields);

    private static string InferEntityTypeFromKey(string key)
    {
        var normalized = key[..^"_links".Length];
        return normalized switch
        {
            "people" or "authors" or "reviewers" or "owners" => "person",
            "companies" or "organization" or "organizations" => "company",
            "places" => "place",
            "products" => "product",
            "services" => "service",
            _ => "thing"
        };
    }

    private static string? InferMediaKind(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.Contains("image", StringComparison.Ordinal) ||
            normalized.Contains("cover", StringComparison.Ordinal) ||
            normalized.Contains("icon", StringComparison.Ordinal) ||
            normalized.Contains("gallery", StringComparison.Ordinal))
        {
            return "image";
        }

        if (normalized.Contains("video", StringComparison.Ordinal))
        {
            return "video";
        }

        if (normalized.Contains("file", StringComparison.Ordinal) ||
            normalized.Contains("attachment", StringComparison.Ordinal) ||
            normalized.Contains("document", StringComparison.Ordinal))
        {
            return "file";
        }

        return null;
    }

    private static string? ReadObjectString(IReadOnlyDictionary<string, object?> map, string key)
    {
        return map.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
    }

    private sealed record ContentRecordSource(
        string Id,
        string Title,
        string Slug,
        DateTimeOffset PublishAt,
        string? ContentHtml,
        IReadOnlyDictionary<string, ContentField>? Fields);
}
