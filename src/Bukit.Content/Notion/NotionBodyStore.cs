using Bukit.Engine.Abstractions.Content;
using System.Collections.Concurrent;

namespace Bukit.Content.Notion;

internal sealed class NotionBodyStore : IContentBodyStore
{
    private readonly Func<ContentItem, CancellationToken, Task<string>> _htmlFactory;
    private readonly ConcurrentDictionary<string, Lazy<Task<ContentBody>>> _cache = new(StringComparer.OrdinalIgnoreCase);

    internal NotionBodyStore(Func<ContentItem, CancellationToken, Task<string>> htmlFactory)
    {
        _htmlFactory = htmlFactory;
    }

    public async Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(item.ContentHtml))
        {
            return new ContentBody(item.ContentHtml);
        }

        var key = item.BodyKey ?? item.Id;
        var lazy = _cache.GetOrAdd(
            key,
            _ => new Lazy<Task<ContentBody>>(
                async () => new ContentBody(await _htmlFactory(item, cancellationToken)),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return await lazy.Value;
    }
}
