namespace Bukit.Engine.Abstractions.Content;

public interface IContentBodyStore
{
    Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default);

    Task<ContentBody> GetAsync(RawContentDocument document, CancellationToken cancellationToken = default)
        => GetAsync(
            new ContentDocument(
                ToRecord(
                    document.Id,
                    document.Title,
                    document.Slug,
                    document.PublishAt,
                    document.Body.InlineHtml,
                    document.CustomFields),
                new ContentBodyRef(document.Body.InlineHtml, document.Body.BodyKey, document.Body.Markdown, document.Body.PlainText),
                ContentRoutePolicy.FromFields(document.CustomFields),
                ContentPublishPolicy.FromFields(document.CustomFields),
                document.CustomFields,
                document.Source),
            cancellationToken);

    private static ContentRecord ToRecord(
        string id,
        string title,
        string slug,
        DateTimeOffset publishAt,
        string? contentHtml,
        IReadOnlyDictionary<string, ContentField>? fields)
    {
        var type = ContentFieldReader.GetText(fields, "type")
            ?? ContentFieldReader.GetText(fields, "collection")
            ?? "page";
        var status = ContentFieldReader.GetBool(fields, "draft") is true
            ? "draft"
            : ContentFieldReader.GetText(fields, "status") ?? "published";

        return new ContentRecord(
            new ContentIdentity(id, slug, ContentFieldReader.GetText(fields, "i18nKey") ?? slug, type, status),
            new ContentPresentation(title, GetSummary(fields), contentHtml, ContentFieldReader.GetText(fields, "language") ?? "und", Array.Empty<string>()),
            new ContentClassification(type, ContentFieldReader.GetText(fields, "collection") ?? type, Array.Empty<string>(), ContentFieldReader.GetTextList(fields, "tags") ?? Array.Empty<string>()),
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
