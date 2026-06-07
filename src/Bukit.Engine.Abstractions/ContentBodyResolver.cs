namespace Bukit.Engine.Abstractions.Content;

public static class ContentBodyResolver
{
    public static async Task<string> GetHtmlAsync(ContentDocument document, IContentBodyStore bodyStore, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(document.Body.Html))
        {
            return document.Body.Html;
        }

        var body = await bodyStore.GetAsync(document, cancellationToken);
        return body.Html;
    }

    [Obsolete("Blocking. Use GetHtmlAsync instead to avoid sync-over-async deadlocks.")]
    public static string GetHtml(ContentDocument document, IContentBodyStore bodyStore)
    {
        if (!string.IsNullOrEmpty(document.Body.Html))
        {
            return document.Body.Html;
        }

        return bodyStore.GetAsync(document).GetAwaiter().GetResult().Html;
    }

}
