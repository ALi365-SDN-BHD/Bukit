namespace Bukit.Content.Notion;

public sealed class NotionRenderContext
{
    internal NotionRenderContext(NotionBlocksRenderer renderer, NotionApiClient client)
        : this(
            new Bukit.Notion.Rendering.NotionRenderContext(renderer.Inner, client.Transport),
            client)
    {
    }

    internal NotionRenderContext(
        Bukit.Notion.Rendering.NotionRenderContext inner,
        NotionApiClient client)
    {
        Inner = inner;
        Client = client;
    }

    internal Bukit.Notion.Rendering.NotionRenderContext Inner { get; }

    public NotionApiClient Client { get; }

    public Task<string> RenderChildrenAsync(string blockId, CancellationToken cancellationToken)
        => Inner.RenderChildrenAsync(blockId, cancellationToken);
}
