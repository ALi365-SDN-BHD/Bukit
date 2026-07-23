using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content.Markdown;

internal sealed class MarkdownBodyStore : IContentBodyStore
{
    public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(document.Body.Html))
        {
            return new ContentBody(document.Body.Html);
        }

        if (string.IsNullOrWhiteSpace(document.Body.BodyKey))
        {
            throw new InvalidOperationException($"Markdown document '{document.Id}' is missing BodyKey.");
        }

        var html = await MarkdownFolderProvider.RenderHtmlFromFileAsync(document.Body.BodyKey, cancellationToken);
        return new ContentBody(html);
    }
}
