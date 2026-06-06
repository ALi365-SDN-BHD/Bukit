using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content.Markdown;

public sealed class MarkdownBodyStore : IContentBodyStore
{
    public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(document.ContentHtml))
        {
            return new ContentBody(document.ContentHtml);
        }

        if (string.IsNullOrWhiteSpace(document.BodyKey))
        {
            throw new InvalidOperationException($"Markdown document '{document.Id}' is missing BodyKey.");
        }

        var html = await MarkdownFolderProvider.RenderHtmlFromFileAsync(document.BodyKey, cancellationToken);
        return new ContentBody(html);
    }
}
