using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine.Abstractions.Content;

public sealed record ContentDocument
{
    public ContentDocument(
        ContentRecord record,
        ContentBodyRef body,
        ContentRoutePolicy? route = null,
        ContentPublishPolicy? publish = null,
        IReadOnlyDictionary<string, ContentField>? customFields = null,
        ContentSourceInfo? source = null,
        IReadOnlyList<ContentDiagnostic>? diagnostics = null)
    {
        Record = record;
        Body = body;
        Route = route ?? ContentRoutePolicy.Empty;
        Publish = publish ?? ContentPublishPolicy.Empty;
        CustomFields = customFields;
        Source = source ?? ContentSourceInfo.Unknown;
        Diagnostics = diagnostics ?? Array.Empty<ContentDiagnostic>();
    }

    public ContentRecord Record { get; init; }
    public ContentBodyRef Body { get; init; }
    public ContentRoutePolicy Route { get; init; }
    public ContentPublishPolicy Publish { get; init; }
    public IReadOnlyDictionary<string, ContentField>? CustomFields { get; init; }
    public ContentSourceInfo Source { get; init; }
    public IReadOnlyList<ContentDiagnostic> Diagnostics { get; init; }

    public string Id => Record.Identity.Id;
    public string Title => Record.Presentation.Title;
    public string Slug => Record.Identity.Slug;
    public DateTimeOffset PublishAt => Record.Lifecycle.PublishedAt;

    internal static ContentDocument Create(
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
        var type = ContentFieldReader.GetText(fields, "type") ?? defaultType;
        var collection = ContentFieldReader.GetText(fields, "collection") ?? string.Empty;
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
            new ContentClassification(type, collection, sections, tags),
            new ContentOwnership(ContentFieldReader.GetText(fields, "author"), ContentFieldReader.GetText(fields, "organization"), ContentFieldReader.GetText(fields, "owner"), ContentFieldReader.GetText(fields, "reviewer")),
            new ContentLifecycle(publishAt, ContentFieldReader.GetDate(fields, "updated"), ContentFieldReader.GetDate(fields, "expires_at"), ContentFieldReader.GetDate(fields, "reviewed_at"))
            {
                Evergreen = ContentFieldReader.GetBool(fields, "evergreen") is true
            },
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

        return new ContentDocument(
            record,
            new ContentBodyRef(contentHtml, bodyKey),
            ContentRoutePolicy.FromFields(fields),
            ContentPublishPolicy.FromFields(fields),
            fields);
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

public sealed record ContentBodyRef(
    string? Html = null,
    string? BodyKey = null,
    string? Markdown = null,
    string? PlainText = null);

public sealed record ContentRoutePolicy(
    string? Url = null,
    string? OutputPath = null,
    string? Template = null,
    string? PermalinkPattern = null,
    string? ListGroup = null)
{
    public static readonly ContentRoutePolicy Empty = new();

    public static ContentRoutePolicy FromFields(IReadOnlyDictionary<string, ContentField>? fields)
        => new(
            ContentFieldReader.GetText(fields, "url"),
            ContentFieldReader.GetText(fields, "outputPath"),
            ContentFieldReader.GetText(fields, "template"),
            ContentFieldReader.GetText(fields, "permalink"),
            ContentFieldReader.GetText(fields, "listGroup"));
}

public sealed record ContentPublishPolicy(
    bool Draft = false,
    bool NoIndex = false,
    bool NoFollow = false,
    bool ExcludeFromFeed = false,
    bool ExcludeFromSearch = false,
    bool ExcludeFromSitemap = false,
    bool IsDataModule = false)
{
    public static readonly ContentPublishPolicy Empty = new();

    public static ContentPublishPolicy FromFields(IReadOnlyDictionary<string, ContentField>? fields)
        => new(
            ContentFieldReader.GetBool(fields, "draft") is true,
            IsNoIndex(fields),
            string.Equals(ContentFieldReader.GetText(fields, "robots"), "nofollow", StringComparison.OrdinalIgnoreCase),
            ContentFieldReader.GetBool(fields, "feedExclude") is true || ContentFieldReader.GetBool(fields, "excludeFromFeed") is true,
            ContentFieldReader.GetBool(fields, "searchExclude") is true || ContentFieldReader.GetBool(fields, "excludeFromSearch") is true,
            ContentFieldReader.GetBool(fields, "sitemapExclude") is true || ContentFieldReader.GetBool(fields, "excludeFromSitemap") is true,
            string.Equals(ContentFieldReader.GetText(fields, "sourceMode"), "data", StringComparison.OrdinalIgnoreCase));

    private static bool IsNoIndex(IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (ContentFieldReader.GetBool(fields, "noindex") is true)
        {
            return true;
        }

        var robots = ContentFieldReader.GetText(fields, "robots");
        return robots is not null &&
               (robots.Contains("noindex", StringComparison.OrdinalIgnoreCase) ||
                robots.Equals("none", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record ContentDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Field = null,
    string? SourceId = null);

public sealed record RoutedContentDocument(
    ContentDocument Document,
    RouteInfo Route,
    DateTimeOffset? LastModified = null);
