using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class CanonicalContentGraphBuilder
{
    internal static CanonicalContentGraph Build(IReadOnlyList<ContentItem> items)
    {
        if (items.Count == 0)
        {
            return CanonicalContentGraph.Empty;
        }

        var records = new List<ContentRecord>(items.Count);
        var entities = new List<EntityRecord>();

        foreach (var item in items)
        {
            var record = ToRecord(item);
            records.Add(record);
            entities.AddRange(record.Entities);
        }

        return new CanonicalContentGraph(records, entities);
    }

    internal static CanonicalContentGraph BuildFromDocuments(IReadOnlyList<ContentDocument> documents)
    {
        if (documents.Count == 0)
        {
            return CanonicalContentGraph.Empty;
        }

        var records = documents.Select(document => document.Record).ToArray();
        var entities = records
            .SelectMany(record => record.Entities)
            .GroupBy(entity => $"{entity.Type}:{entity.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var relations = records
            .SelectMany(record => record.Relations)
            .GroupBy(relation => $"{relation.Type}:{relation.Target}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        return new CanonicalContentGraph(records, entities, documents, relations);
    }

    internal static ContentRecord ToRecord(ContentItem item)
    {
        var language = FirstText(item, "language") ?? "und";
        var type = FirstText(item, "type")
            ?? FirstText(item, "collection")
            ?? "page";
        var collection = FirstText(item, "collection") ?? type;
        var status = ResolveStatus(item);
        var citations = ExtractCitationUrls(item);
        var references = FirstList(item, "references") ?? Array.Empty<string>();
        var translations = ExtractTranslations(item);
        var tags = MergeLists(FirstList(item, "tags"), FirstList(item, "categories"));
        var sections = FirstList(item, "sections")
            ?? FirstList(item, "categories")
            ?? Array.Empty<string>();
        var entities = ExtractEntities(item);
        var relations = ExtractRelations(item, translations, entities);
        var media = ExtractMedia(item);
        var authors = FirstList(item, "authors");
        var owners = FirstList(item, "owners");
        var reviewers = FirstList(item, "reviewers");

        return new ContentRecord(
            new ContentIdentity(
                item.Id,
                item.Slug,
                FirstText(item, "i18nKey") ?? item.Slug,
                type,
                status),
            new ContentPresentation(
                item.Title,
                FirstText(item, "summary")
                    ?? FirstText(item, "description")
                    ?? FirstText(item, "excerpt"),
                item.ContentHtml,
                language,
                translations),
            new ContentClassification(type, collection, sections, tags),
            new ContentOwnership(
                FirstText(item, "author") ?? authors?.FirstOrDefault(),
                FirstText(item, "organization") ?? FirstText(item, "org") ?? FirstText(item, "company"),
                FirstText(item, "owner") ?? owners?.FirstOrDefault(),
                FirstText(item, "reviewer") ?? reviewers?.FirstOrDefault()),
            new ContentLifecycle(
                item.PublishAt,
                FirstDate(item, "updated") ?? FirstDate(item, "modified") ?? FirstDate(item, "update_time") ?? FirstDate(item, "last_edited_time"),
                FirstDate(item, "expires_at") ?? FirstDate(item, "expires"),
                SeoGeoMetaParser.ParseGeoMeta(item).DateReviewed),
            new ProvenanceRecord(
                FirstText(item, "source"),
                FirstText(item, "original_url") ?? FirstText(item, "source_url") ?? FirstText(item, "url"),
                citations,
                references,
                FirstText(item, "sync_status")),
            new TrustMetadata(
                FirstDouble(item, "credibility_score") ?? FirstDouble(item, "trust_score"),
                FirstText(item, "review_status") ?? status,
                FirstList(item, "quality_flags") ?? Array.Empty<string>()),
            entities,
            relations,
            media);
    }

    private static string ResolveStatus(ContentItem item)
    {
        if (item.Meta.TryGetValue("draft", out var draft) && draft is bool isDraft && isDraft)
        {
            return "draft";
        }

        return FirstText(item, "status") ?? "published";
    }

    private static IReadOnlyList<string> ExtractTranslations(ContentItem item)
    {
        var translations = FirstList(item, "translations");
        if (translations is { Count: > 0 })
        {
            return translations;
        }

        return MetaHelpers.TryGetI18nKey(item.Meta, out var key)
            ? new[] { key }
            : Array.Empty<string>();
    }

    private static IReadOnlyList<string> ExtractCitationUrls(ContentItem item)
    {
        var geo = SeoGeoMetaParser.ParseGeoMeta(item);
        if (geo.Citations is { Count: > 0 })
        {
            return geo.Citations.Select(x => x.Url).ToArray();
        }

        return FirstList(item, "citations") ?? Array.Empty<string>();
    }

    private static IReadOnlyList<EntityRecord> ExtractEntities(ContentItem item)
    {
        var entities = new List<EntityRecord>();

        if (item.Meta.TryGetValue("entities", out var rawEntities) && rawEntities is IEnumerable<object> entityItems)
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
                        ReadMapString(map, "id")));
                }
            }
        }

        AppendNamedEntities(entities, "product", MetaHelpers.GetStringList(item.Meta, "products"));
        AppendNamedEntities(entities, "service", MetaHelpers.GetStringList(item.Meta, "services"));
        AppendNamedEntities(entities, "place", MetaHelpers.GetStringList(item.Meta, "places"));
        AppendNamedEntities(entities, "person", MetaHelpers.GetStringList(item.Meta, "people"));
        AppendNamedEntities(entities, "company", MetaHelpers.GetStringList(item.Meta, "companies"));
        AppendNamedEntities(entities, "product", FirstList(item, "products"));
        AppendNamedEntities(entities, "service", FirstList(item, "services"));
        AppendNamedEntities(entities, "place", FirstList(item, "places"));
        AppendNamedEntities(entities, "person", FirstList(item, "people"));
        AppendNamedEntities(entities, "person", FirstList(item, "authors"));
        AppendNamedEntities(entities, "company", FirstList(item, "companies"));

        if (item.Fields is not null)
        {
            foreach (var (key, field) in item.Fields)
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
                            ReadObjectString(link, "id")));
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
        ContentItem item,
        IReadOnlyList<string> translations,
        IReadOnlyList<EntityRecord> entities)
    {
        var relations = new List<ContentRelation>();

        foreach (var translation in translations)
        {
            relations.Add(new ContentRelation("translation-of", translation));
        }

        foreach (var related in FirstList(item, "related_to") ?? Array.Empty<string>())
        {
            relations.Add(new ContentRelation("related-to", related));
        }

        if (item.Fields is not null)
        {
            foreach (var (key, field) in item.Fields)
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
                            relations.Add(new ContentRelation(relationType, target));
                        }
                    }
                }
            }
        }

        foreach (var entity in entities)
        {
            relations.Add(new ContentRelation("mentions", entity.Name));
        }

        return relations
            .GroupBy(x => $"{x.Type}:{x.Target}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    private static IReadOnlyList<MediaAsset> ExtractMedia(ContentItem item)
    {
        var media = new List<MediaAsset>();
        AddMedia(media, "image", MetaHelpers.GetString(item.Meta, "image"), MetaHelpers.GetString(item.Meta, "image_alt"));
        AddMedia(media, "image", MetaHelpers.GetString(item.Meta, "cover"), MetaHelpers.GetString(item.Meta, "cover_alt"));
        AddMedia(media, "video", MetaHelpers.GetString(item.Meta, "video"), MetaHelpers.GetString(item.Meta, "video_alt"));
        AddMedia(media, "file", MetaHelpers.GetString(item.Meta, "attachment"), MetaHelpers.GetString(item.Meta, "attachment_alt"));
        if (item.Fields is not null)
        {
            foreach (var (key, field) in item.Fields)
            {
                var kind = InferMediaKind(key);
                if (kind is null)
                {
                    continue;
                }

                if (field.Type.Equals("file", StringComparison.OrdinalIgnoreCase) && field.Value is not null)
                {
                    AddMedia(media, kind, field.Value.ToString(), FirstText(item, key + "_alt"));
                    continue;
                }

                if (field.Type.Equals("files", StringComparison.OrdinalIgnoreCase) &&
                    field.Value is IEnumerable<string> files)
                {
                    foreach (var file in files)
                    {
                        AddMedia(media, kind, file, FirstText(item, key + "_alt"));
                    }
                }
            }
        }

        return media
            .GroupBy(x => $"{x.Kind}:{x.Url}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    private static void AddMedia(List<MediaAsset> media, string kind, string? url, string? alt)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        media.Add(new MediaAsset(kind, url, alt));
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

    private static DateTimeOffset? TryGetDate(IReadOnlyDictionary<string, object> meta, string key)
    {
        var value = MetaHelpers.GetString(meta, key);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static double? TryGetDouble(IReadOnlyDictionary<string, object> meta, string key)
    {
        var value = MetaHelpers.GetString(meta, key);
        return double.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? ReadMapString(IReadOnlyDictionary<string, object> map, string key)
    {
        return map.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
    }

    private static string? FirstText(ContentItem item, string key)
        => MetaHelpers.TryGetTextField(item.Fields, key) ?? MetaHelpers.GetString(item.Meta, key);

    private static IReadOnlyList<string>? FirstList(ContentItem item, string key)
    {
        if (item.Fields is not null &&
            item.Fields.TryGetValue(key, out var field) &&
            field.Value is not null)
        {
            if (field.Value is IEnumerable<string> strings)
            {
                var list = strings.Select(x => (x ?? string.Empty).Trim()).Where(x => x.Length > 0).ToArray();
                if (list.Length > 0)
                {
                    return list;
                }
            }

            if (field.Value is IEnumerable<object> objects)
            {
                var list = objects.Select(x => x?.ToString() ?? string.Empty).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                if (list.Length > 0)
                {
                    return list;
                }
            }
        }

        return MetaHelpers.GetStringList(item.Meta, key);
    }

    private static DateTimeOffset? FirstDate(ContentItem item, string key)
    {
        if (item.Fields is not null &&
            item.Fields.TryGetValue(key, out var field) &&
            field.Value is DateTimeOffset dto)
        {
            return dto;
        }

        return TryGetDate(item.Meta, key);
    }

    private static double? FirstDouble(ContentItem item, string key)
    {
        if (item.Fields is not null &&
            item.Fields.TryGetValue(key, out var field) &&
            field.Value is not null &&
            double.TryParse(field.Value.ToString(), out var parsed))
        {
            return parsed;
        }

        return TryGetDouble(item.Meta, key);
    }

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
}
