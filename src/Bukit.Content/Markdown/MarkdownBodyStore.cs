namespace Bukit.Content.Markdown;

public sealed class MarkdownBodyStore : IContentBodyStore
{
    public async Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(item.ContentHtml))
        {
            return new ContentBody(item.ContentHtml);
        }

        if (string.IsNullOrWhiteSpace(item.BodyKey))
        {
            throw new InvalidOperationException($"Markdown item '{item.Id}' is missing BodyKey.");
        }

        var html = await MarkdownFolderProvider.RenderHtmlFromFileAsync(item.BodyKey, cancellationToken);
        return new ContentBody(html);
    }
}
