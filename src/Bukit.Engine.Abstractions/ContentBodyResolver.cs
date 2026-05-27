namespace Bukit.Engine.Abstractions.Content;

public static class ContentBodyResolver
{
    public static async Task<string> GetHtmlAsync(ContentItem item, IContentBodyStore bodyStore, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(item.ContentHtml))
        {
            return item.ContentHtml;
        }

        var body = await bodyStore.GetAsync(item, cancellationToken);
        return body.Html;
    }

    public static string GetHtml(ContentItem item, IContentBodyStore bodyStore)
    {
        if (!string.IsNullOrEmpty(item.ContentHtml))
        {
            return item.ContentHtml;
        }

        return bodyStore.GetAsync(item).GetAwaiter().GetResult().Html;
    }
}
