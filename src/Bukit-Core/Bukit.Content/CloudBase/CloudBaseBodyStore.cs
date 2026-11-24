using Bukit.Content.Markdown;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content.CloudBase;

/// <summary>Renders CloudBase Markdown lazily with the untrusted-link policy enabled (D9).</summary>
internal sealed class CloudBaseBodyStore : IContentBodyStore
{
    private readonly IReadOnlyDictionary<string, string> _markdownByKey;
    private readonly IReadOnlyDictionary<string, string> _imageUrls;
    private readonly string _placeholder;

    internal CloudBaseBodyStore(
        IReadOnlyDictionary<string, string> markdownByKey,
        IReadOnlyDictionary<string, string>? imageUrls = null,
        string placeholder = "/assets/images/noneimg-news.jpg")
    {
        _markdownByKey = markdownByKey;
        _imageUrls = imageUrls ?? new Dictionary<string, string>(StringComparer.Ordinal);
        _placeholder = placeholder;
    }

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = document.Body.BodyKey;
        if (string.IsNullOrEmpty(key) || !_markdownByKey.TryGetValue(key, out var markdown))
        {
            return Task.FromResult(new ContentBody(document.Body.Html ?? string.Empty));
        }

        return Task.FromResult(new ContentBody(BasicMarkdownToHtml.Convert(
            markdown,
            untrustedLinks: true,
            imageUrlResolver: ResolveImageUrl)));
    }

    private string ResolveImageUrl(string url)
    {
        if (!url.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return _imageUrls.TryGetValue(url, out var signed) ? signed : _placeholder;
    }
}
