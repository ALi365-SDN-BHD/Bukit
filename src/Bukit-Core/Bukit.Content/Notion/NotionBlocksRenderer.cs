using System.Text;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Bukit.Shared;

namespace Bukit.Content.Notion;

public sealed class NotionBlocksRenderer
{
    private readonly Bukit.Notion.Rendering.NotionBlocksRenderer _inner;
    private readonly NotionBlockRendererRegistry _registry;

    public NotionBlocksRenderer(NotionApiClient client)
        : this(client, NotionBlockRendererRegistry.CreateDefault())
    {
    }

    public NotionBlocksRenderer(NotionApiClient client, NotionBlockRendererRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _registry.BindClient(client);
        _inner = new Bukit.Notion.Rendering.NotionBlocksRenderer(client.Transport, registry.Inner);
    }

    public NotionBlockRendererRegistry Registry => _registry;

    internal Bukit.Notion.Rendering.NotionBlocksRenderer Inner => _inner;

    public Task<string> RenderPageAsync(string pageId, CancellationToken cancellationToken)
        => TranslateAsync(() => _inner.RenderPageAsync(pageId, cancellationToken));

    internal Task RenderChildrenToBuilderAsync(
        string blockId,
        StringBuilder builder,
        CancellationToken cancellationToken)
        => TranslateAsync(() => _inner.RenderChildrenToBuilderAsync(blockId, builder, cancellationToken));

    private static async Task TranslateAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is NotionRenderingException or NotionApiException)
        {
            throw new ContentException(exception.Message, exception);
        }
    }

    private static async Task<T> TranslateAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (exception is NotionRenderingException or NotionApiException)
        {
            throw new ContentException(exception.Message, exception);
        }
    }
}
