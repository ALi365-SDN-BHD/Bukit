namespace Bukit.Engine.Abstractions.Content;

public interface IContentBodyStore
{
    Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default);

    Task<ContentBody> GetAsync(RawContentDocument document, CancellationToken cancellationToken = default)
    {
        var fields = ContentDocumentFactory.MergeFields(document.Properties, document.CustomFields);
        return GetAsync(ContentDocumentFactory.CreateDocument(document, fields), cancellationToken);
    }
}
