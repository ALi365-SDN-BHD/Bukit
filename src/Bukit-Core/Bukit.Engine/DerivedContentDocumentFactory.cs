using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class DerivedContentDocumentFactory
{
    /// <param name="language">
    /// The language of the build deriving the page, written as its <c>language</c> field unless the fields set one,
    /// so the page's projections, publish document and JSON-LD name it instead of <c>und</c>.
    /// </param>
    internal static ContentDocument Create(
        string id,
        string title,
        string slug,
        DateTimeOffset publishAt,
        ContentBodyRef body,
        IReadOnlyDictionary<string, ContentField>? customFields = null,
        ContentSourceInfo? source = null,
        string? language = null)
    {
        if (!string.IsNullOrWhiteSpace(language) && ContentFieldReader.GetText(customFields, "language") is null)
        {
            customFields = new Dictionary<string, ContentField>(customFields ?? new Dictionary<string, ContentField>(), StringComparer.OrdinalIgnoreCase)
            {
                ["language"] = new ContentField("text", language.Trim())
            };
        }

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
