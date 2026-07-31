namespace Bukit.Engine.Abstractions.Content;

/// <summary>
/// Factory methods for constructing <see cref="ContentDocument"/> and
/// <see cref="ContentRecord"/> instances from raw content fields.
/// Extracted from <see cref="IContentBodyStore"/> default interface logic.
/// </summary>
public static class ContentDocumentFactory
{
    /// <summary>
    /// Merges raw properties into custom fields. Properties that already
    /// exist in <paramref name="customFields"/> (case-insensitive) are
    /// skipped so that explicit custom field values take precedence.
    /// </summary>
    public static IReadOnlyDictionary<string, ContentField>? MergeFields(
        IReadOnlyDictionary<string, RawContentValue>? properties,
        IReadOnlyDictionary<string, ContentField>? customFields)
    {
        if (properties is null || properties.Count == 0)
        {
            return customFields;
        }

        if (customFields is IDictionary<string, ContentField> mutableCustomFields &&
            !mutableCustomFields.IsReadOnly)
        {
            foreach (var (key, value) in properties)
            {
                if (!mutableCustomFields.ContainsKey(key))
                {
                    mutableCustomFields[key] = new ContentField(value.Kind, value.Value);
                }
            }

            return customFields;
        }

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

    /// <summary>
    /// Creates a <see cref="ContentDocument"/> from a <see cref="RawContentDocument"/>
    /// and its merged fields dictionary.
    /// </summary>
    public static ContentDocument CreateDocument(
        RawContentDocument document,
        IReadOnlyDictionary<string, ContentField>? fields)
    {
        return new ContentDocument(
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
            document.Source);
    }

    /// <summary>
    /// Builds a <see cref="ContentRecord"/> from scalar content fields,
    /// applying canonical type inference, author projection, and lifecycle mapping.
    /// </summary>
    public static ContentRecord ToRecord(
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

        var authorProjection = ContentAuthorProfileProjectionReader.Read(fields);
        return new ContentRecord(
            new ContentIdentity(id, slug, ContentFieldReader.GetText(fields, "i18nKey") ?? slug, type, status),
            new ContentPresentation(title, GetSummary(fields), contentHtml, ContentFieldReader.GetText(fields, "language") ?? "und", Array.Empty<string>()),
            new ContentClassification(type, collection, Array.Empty<string>(), ContentFieldReader.GetTextList(fields, "tags") ?? Array.Empty<string>()),
            new ContentOwnership(ContentFieldReader.GetText(fields, "author"), ContentFieldReader.GetText(fields, "organization"), ContentFieldReader.GetText(fields, "owner"), ContentFieldReader.GetText(fields, "reviewer"))
            {
                AuthorType = ContentFieldReader.GetText(fields, "authorType"),
                UsesAuthorRelation = authorProjection.UsesAuthorRelation,
                AuthorProfiles = authorProjection.Profiles
            },
            new ContentLifecycle(publishAt, ContentFieldReader.GetDate(fields, "updated"), ContentFieldReader.GetDate(fields, "expires_at"), ContentFieldReader.GetDate(fields, "reviewed_at"))
            {
                Evergreen = ContentFieldReader.GetBool(fields, "evergreen") is true
            },
            new ProvenanceRecord(ContentFieldReader.GetText(fields, "source"), ContentFieldReader.GetText(fields, "original_url"), Array.Empty<string>(), Array.Empty<string>(), ContentFieldReader.GetText(fields, "sync_status")),
            new TrustMetadata(null, ContentFieldReader.GetText(fields, "review_status") ?? status, Array.Empty<string>()),
            Array.Empty<EntityRecord>(),
            Array.Empty<ContentRelation>(),
            Array.Empty<MediaAsset>());
    }

    /// <summary>
    /// Resolves the best available summary text from content fields,
    /// checking <c>summary</c>, <c>description</c>, and <c>excerpt</c> in order.
    /// </summary>
    public static string? GetSummary(IReadOnlyDictionary<string, ContentField>? fields)
        => ContentFieldReader.GetText(fields, "summary")
           ?? ContentFieldReader.GetText(fields, "description")
           ?? ContentFieldReader.GetText(fields, "excerpt");
}
