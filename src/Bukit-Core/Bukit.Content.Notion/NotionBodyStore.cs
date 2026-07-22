using Bukit.Engine.Abstractions.Content;
using System.Collections.Concurrent;

namespace Bukit.Content.Notion;

internal sealed class NotionBodyStore : IContentBodyStore
{
    private readonly Func<ContentDocument, CancellationToken, Task<string>> _htmlFactory;
    private readonly ConcurrentDictionary<string, Lazy<Task<ContentBody>>> _cache = new(StringComparer.OrdinalIgnoreCase);

    internal NotionBodyStore(Func<ContentDocument, CancellationToken, Task<string>> htmlFactory)
    {
        _htmlFactory = htmlFactory;
    }

    public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(document.Body.Html))
        {
            return new ContentBody(document.Body.Html);
        }

        var key = document.Body.BodyKey ?? document.Id;
        var lazy = _cache.GetOrAdd(
            key,
            _ => new Lazy<Task<ContentBody>>(
                async () => new ContentBody(await _htmlFactory(document, cancellationToken)),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazy.Value;
        }
        catch
        {
            ((ICollection<KeyValuePair<string, Lazy<Task<ContentBody>>>>)_cache)
                .Remove(new KeyValuePair<string, Lazy<Task<ContentBody>>>(key, lazy));
            throw;
        }
    }
}
