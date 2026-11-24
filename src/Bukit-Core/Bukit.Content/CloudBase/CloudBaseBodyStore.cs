using Bukit.Content.Markdown;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content.CloudBase;

/// <summary>Renders CloudBase Markdown lazily with the untrusted-link policy enabled (D9).</summary>
internal sealed class CloudBaseBodyStore : IContentBodyStore
{
    private readonly IReadOnlyDictionary<string, string> _markdownByKey;

    internal CloudBaseBodyStore(IReadOnlyDictionary<string, string> markdownByKey)
    {
        _markdownByKey = markdownByKey;
    }

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = document.Body.BodyKey;
        if (string.IsNullOrEmpty(key) || !_markdownByKey.TryGetValue(key, out var markdown))
        {
            return Task.FromResult(new ContentBody(document.Body.Html ?? string.Empty));
        }

        return Task.FromResult(new ContentBody(BasicMarkdownToHtml.Convert(markdown, untrustedLinks: true)));
    }
}
