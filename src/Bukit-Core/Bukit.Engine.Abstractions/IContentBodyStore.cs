namespace Bukit.Engine.Abstractions.Content;

public interface IContentBodyStore
{
    Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default);

    Task<ContentBody> GetAsync(RawContentDocument document, CancellationToken cancellationToken = default)
    {
        var fields = MergeFields(document.Properties, document.CustomFields);

        return GetAsync(
            new ContentDocument(
                ToRecord(
                    document.Id,
                    document.Title,
                    document.Slug,
                    document.PublishAt,
                    document.Body.InlineHtml,
                    fields),
                new ContentBodyRef(document.Body.InlineHtml, document.Body.BodyKey, document.Body.Markdown, document.Body.PlainText),
                ContentRoutePolicy.FromFields(fields),
                ContentPublishPolicy.FromFields(fields),
                fields,
                document.Source),
            cancellationToken);
    }

    private static IReadOnlyDictionary<string, ContentField> MergeFields(
        IReadOnlyDictionary<string, RawContentValue>? properties,
        IReadOnlyDictionary<string, ContentField>? customFields)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);

        if (properties is not null)
        {
            foreach (var (key, value) in properties)
            {
                fields[key] = new ContentField(value.Kind, value.Value);
            }
        }

        if (customFields is not null)
        {
            foreach (var (key, value) in customFields)
            {
                fields[key] = value;
            }
        }

        return fields;
    }

    private static ContentRecord ToRecord(
        string id,
        string title,
        string slug,
        DateTimeOffset publishAt,
        string? contentHtml,
        IReadOnlyDictionary<string, ContentField>? fields)
    {
        var defaultType = string.Equals(ContentFieldReader.GetText(fields, "sourceMode"), "data", StringComparison.OrdinalIgnoreCase)
            ? "module"
            : "page";
        var type = ContentFieldReader.GetText(fields, "type") ?? defaultType;
        var collection = ContentFieldReader.GetText(fields, "collection") ?? string.Empty;
        var status = ContentFieldReader.GetBool(fields, "draft") is true
            ? "draft"
            : ContentFieldReader.GetText(fields, "status") ?? "published";

        return new ContentRecord(
            new ContentIdentity(id, slug, ContentFieldReader.GetText(fields, "i18nKey") ?? slug, type, status),
            new ContentPresentation(title, GetSummary(fields), contentHtml, ContentFieldReader.GetText(fields, "language") ?? "und", Array.Empty<string>()),
            new ContentClassification(type, collection, Array.Empty<string>(), ContentFieldReader.GetTextList(fields, "tags") ?? Array.Empty<string>()),
            new ContentOwnership(ContentFieldReader.GetText(fields, "author"), ContentFieldReader.GetText(fields, "organization"), ContentFieldReader.GetText(fields, "owner"), ContentFieldReader.GetText(fields, "reviewer")),
            new ContentLifecycle(publishAt, ContentFieldReader.GetDate(fields, "updated"), ContentFieldReader.GetDate(fields, "expires_at"), ContentFieldReader.GetDate(fields, "reviewed_at")),
            new ProvenanceRecord(ContentFieldReader.GetText(fields, "source"), ContentFieldReader.GetText(fields, "original_url"), Array.Empty<string>(), Array.Empty<string>(), ContentFieldReader.GetText(fields, "sync_status")),
            new TrustMetadata(null, ContentFieldReader.GetText(fields, "review_status") ?? status, Array.Empty<string>()),
            Array.Empty<EntityRecord>(),
            Array.Empty<ContentRelation>(),
            Array.Empty<MediaAsset>());
    }

    private static string? GetSummary(IReadOnlyDictionary<string, ContentField>? fields)
        => ContentFieldReader.GetText(fields, "summary")
           ?? ContentFieldReader.GetText(fields, "description")
           ?? ContentFieldReader.GetText(fields, "excerpt");

}
