using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine.Abstractions.Content;

public sealed record ContentDocument(
    ContentRecord Record,
    string? ContentHtml,
    IReadOnlyDictionary<string, ContentField>? Fields,
    string? BodyKey)
{
    public string Id => Record.Identity.Id;
    public string Title => Record.Presentation.Title;
    public string Slug => Record.Identity.Slug;
    public DateTimeOffset PublishAt => Record.Lifecycle.PublishedAt;

    public static ContentDocument Create(
        string id,
        string title,
        string slug,
        DateTimeOffset publishAt,
        string? contentHtml,
        IReadOnlyDictionary<string, ContentField>? fields = null,
        string? bodyKey = null)
    {
        var defaultType = string.Equals(ContentFieldReader.GetText(fields, "sourceMode"), "data", StringComparison.OrdinalIgnoreCase)
            ? "module"
            : "page";
        var type = ContentFieldReader.GetText(fields, "type")
            ?? ContentFieldReader.GetText(fields, "collection")
            ?? defaultType;
        var status = ContentFieldReader.GetBool(fields, "draft") is true
            ? "draft"
            : ContentFieldReader.GetText(fields, "status") ?? "published";
        var summary = ContentFieldReader.GetText(fields, "summary")
            ?? ContentFieldReader.GetText(fields, "description")
            ?? ContentFieldReader.GetText(fields, "excerpt");
        var sections = ContentFieldReader.GetTextList(fields, "sections")
            ?? ContentFieldReader.GetTextList(fields, "categories")
            ?? Array.Empty<string>();
        var tags = MergeLists(
            ContentFieldReader.GetTextList(fields, "tags"),
            ContentFieldReader.GetTextList(fields, "categories"));
        var translations = ContentFieldReader.GetTextList(fields, "translations")
            ?? Array.Empty<string>();
        var entities = ExtractEntities(fields);
        var relations = ExtractRelations(fields, translations, entities);
        var media = ExtractMedia(fields);

        var record = new ContentRecord(
            new ContentIdentity(id, slug, ContentFieldReader.GetText(fields, "i18nKey") ?? slug, type, status),
            new ContentPresentation(title, summary, contentHtml, ContentFieldReader.GetText(fields, "language") ?? "und", translations),
            new ContentClassification(type, ContentFieldReader.GetText(fields, "collection") ?? type, sections, tags),
            new ContentOwnership(ContentFieldReader.GetText(fields, "author"), ContentFieldReader.GetText(fields, "organization"), ContentFieldReader.GetText(fields, "owner"), ContentFieldReader.GetText(fields, "reviewer")),
            new ContentLifecycle(publishAt, ContentFieldReader.GetDate(fields, "updated"), ContentFieldReader.GetDate(fields, "expires_at"), ContentFieldReader.GetDate(fields, "reviewed_at")),
            new ProvenanceRecord(
                ContentFieldReader.GetText(fields, "source"),
                ContentFieldReader.GetText(fields, "original_url") ?? ContentFieldReader.GetText(fields, "source_url") ?? ContentFieldReader.GetText(fields, "url"),
                ContentFieldReader.GetTextList(fields, "citations") ?? Array.Empty<string>(),
                ContentFieldReader.GetTextList(fields, "references") ?? Array.Empty<string>(),
                ContentFieldReader.GetText(fields, "sync_status")),
            new TrustMetadata(
                ContentFieldReader.GetNumber(fields, "credibility_score") ?? ContentFieldReader.GetNumber(fields, "trust_score"),
                ContentFieldReader.GetText(fields, "review_status") ?? status,
                ContentFieldReader.GetTextList(fields, "quality_flags") ?? Array.Empty<string>()),
            entities,
            relations,
            media);

        return new ContentDocument(record, contentHtml, fields, bodyKey);
    }

    private static IReadOnlyList<string> MergeLists(params IReadOnlyList<string>?[] lists)
    {
        var values = new List<string>();
        foreach (var list in lists)
        {
            if (list is null)
            {
                continue;
            }

            foreach (var value in list)
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    !values.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    private static IReadOnlyList<EntityRecord> ExtractEntities(IReadOnlyDictionary<string, ContentField>? fields)
    {
        var entities = new List<EntityRecord>();
        AppendNamedEntities(entities, "product", ContentFieldReader.GetTextList(fields, "products"));
        AppendNamedEntities(entities, "service", ContentFieldReader.GetTextList(fields, "services"));
        AppendNamedEntities(entities, "place", ContentFieldReader.GetTextList(fields, "places"));
        AppendNamedEntities(entities, "person", ContentFieldReader.GetTextList(fields, "people"));
        AppendNamedEntities(entities, "company", ContentFieldReader.GetTextList(fields, "companies"));

        return entities
            .GroupBy(x => $"{x.Type}:{x.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    private static void AppendNamedEntities(List<EntityRecord> target, string type, IReadOnlyList<string>? names)
    {
        if (names is null)
        {
            return;
        }

        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                target.Add(new EntityRecord(type, name));
            }
        }
    }

    private static IReadOnlyList<ContentRelation> ExtractRelations(
        IReadOnlyDictionary<string, ContentField>? fields,
        IReadOnlyList<string> translations,
        IReadOnlyList<EntityRecord> entities)
    {
        var relations = new List<ContentRelation>();
        foreach (var translation in translations)
        {
            relations.Add(new ContentRelation("translation-of", translation, "content", translation));
        }

        foreach (var related in ContentFieldReader.GetTextList(fields, "related_to") ?? Array.Empty<string>())
        {
            relations.Add(new ContentRelation("related-to", related, "content", related));
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

    private static IReadOnlyList<MediaAsset> ExtractMedia(IReadOnlyDictionary<string, ContentField>? fields)
    {
        var media = new List<MediaAsset>();
        AddMedia(media, "image", "image", fields);
        AddMedia(media, "image", "cover", fields);
        AddMedia(media, "video", "video", fields);
        AddMedia(media, "file", "attachment", fields);
        return media;
    }

    private static void AddMedia(List<MediaAsset> media, string kind, string key, IReadOnlyDictionary<string, ContentField>? fields)
    {
        var url = ContentFieldReader.GetText(fields, key);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        media.Add(new MediaAsset(
            kind,
            url,
            ContentFieldReader.GetText(fields, $"{key}_alt") ?? ContentFieldReader.GetText(fields, "alt"),
            ContentFieldReader.GetText(fields, $"{key}_caption") ?? ContentFieldReader.GetText(fields, "caption"),
            ContentFieldReader.GetText(fields, $"{key}_description") ?? ContentFieldReader.GetText(fields, "description"),
            ContentFieldReader.GetText(fields, $"{key}_license") ?? ContentFieldReader.GetText(fields, "license")));
    }
}

public sealed record RoutedContentDocument(
    ContentDocument Document,
    RouteInfo Route,
    DateTimeOffset? LastModified = null);
