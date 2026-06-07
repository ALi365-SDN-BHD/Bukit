using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class DerivedContentDocumentFactory
{
    internal static ContentDocument Create(
        string id,
        string title,
        string slug,
        DateTimeOffset publishAt,
        ContentBodyRef body,
        IReadOnlyDictionary<string, ContentField>? customFields = null,
        ContentSourceInfo? source = null)
    {
        var raw = new RawContentDocument(
            Id: id,
            Title: title,
            Slug: slug,
            PublishAt: publishAt,
            Body: new RawBody(body.Html, body.BodyKey, body.Markdown, body.PlainText),
            Properties: RawContentValue.FromFields(customFields),
            Source: source,
            CustomFields: customFields);

        return ContentDocumentNormalizer.ToDocument(raw);
    }
}
