using System.Text.Json;

namespace Bukit.Content.Notion;

public delegate Task<string?> NotionBlockTransformer(
    JsonElement block,
    NotionRenderContext context,
    CancellationToken cancellationToken);

public sealed class NotionBlockRendererRegistry
{
    private NotionApiClient? _client;

    internal Bukit.Notion.Rendering.NotionBlockRendererRegistry Inner { get; }

    public NotionBlockRendererRegistry()
        : this(new Bukit.Notion.Rendering.NotionBlockRendererRegistry())
    {
    }

    private NotionBlockRendererRegistry(
        Bukit.Notion.Rendering.NotionBlockRendererRegistry inner)
    {
        Inner = inner;
    }

    public NotionBlockRendererRegistry Register(string blockType, INotionBlockRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        Inner.Register(blockType, new RendererAdapter(this, renderer));
        return this;
    }

    public NotionBlockRendererRegistry SetCustomTransformer(
        string blockType,
        NotionBlockTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        Inner.SetCustomTransformer(
            blockType,
            (block, context, cancellationToken) =>
                transformer(block, CreateContext(context), cancellationToken));
        return this;
    }

    public NotionBlockRendererRegistry RemoveCustomTransformer(string blockType)
    {
        Inner.RemoveCustomTransformer(blockType);
        return this;
    }

    internal Task<string?> RenderBlockAsync(
        string blockType,
        JsonElement block,
        NotionRenderContext context,
        CancellationToken cancellationToken)
        => Inner.RenderBlockAsync(
            blockType,
            block,
            context is null ? null! : context.Inner,
            cancellationToken);

    internal void BindClient(NotionApiClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    private NotionRenderContext CreateContext(Bukit.Notion.Rendering.NotionRenderContext context)
        => context is null
            ? null!
            : new(context, _client ?? throw new InvalidOperationException(
                "The Notion renderer registry must be attached to a renderer before use."));

    public static NotionBlockRendererRegistry CreateDefault()
        => new(Bukit.Notion.Rendering.NotionBlockRendererRegistry.CreateDefault());

    private sealed class RendererAdapter(
        NotionBlockRendererRegistry owner,
        INotionBlockRenderer renderer)
        : Bukit.Notion.Rendering.INotionBlockRenderer
    {
        public Task<string?> RenderAsync(
            JsonElement block,
            Bukit.Notion.Rendering.NotionRenderContext context,
            CancellationToken cancellationToken)
            => renderer.RenderAsync(block, owner.CreateContext(context), cancellationToken);
    }
}
