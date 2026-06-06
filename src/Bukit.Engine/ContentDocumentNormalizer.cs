using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class ContentDocumentNormalizer
{
    internal static ContentDocument ToDocument(RawContentDocument raw)
    {
        return new ContentDocument(
            CanonicalContentGraphBuilder.ToRecord(raw),
            raw.ContentHtml,
            raw.Fields,
            raw.BodyKey);
    }

    internal static IReadOnlyList<ContentDocument> ToDocuments(IReadOnlyList<RawContentDocument> rawDocuments)
        => rawDocuments.Select(ToDocument).ToArray();
}
