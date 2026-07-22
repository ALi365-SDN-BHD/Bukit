using System.Text.Json;

namespace Bukit.Content.Notion;

public delegate Task<string?> NotionBlockTransformer(
    JsonElement block,
    NotionRenderContext context,
    CancellationToken cancellationToken);

public sealed class NotionBlockRendererRegistry
{
    private NotionApiClient? _client;

    internal Bukit.Notion.Rendering.NotionBlockRendererRegistry Inner { get; } = new();

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
    {
        var registry = new NotionBlockRendererRegistry();
        registry.Register("paragraph", new BlockRenderers.RichTextContainerRenderer("paragraph", "p"));
        registry.Register("heading_1", new BlockRenderers.RichTextContainerRenderer("heading_1", "h1"));
        registry.Register("heading_2", new BlockRenderers.RichTextContainerRenderer("heading_2", "h2"));
        registry.Register("heading_3", new BlockRenderers.RichTextContainerRenderer("heading_3", "h3"));
        registry.Register("quote", new BlockRenderers.RichTextContainerRenderer("quote", "blockquote"));
        registry.Register("code", new BlockRenderers.CodeBlockRenderer());
        registry.Register("divider", new BlockRenderers.DividerBlockRenderer());
        registry.Register("image", new BlockRenderers.ImageBlockRenderer());
        registry.Register("callout", new BlockRenderers.CalloutBlockRenderer());
        registry.Register("to_do", new BlockRenderers.ToDoBlockRenderer());
        registry.Register("toggle", new BlockRenderers.ToggleBlockRenderer());
        registry.Register("bookmark", new BlockRenderers.BookmarkBlockRenderer());
        registry.Register("link_preview", new BlockRenderers.LinkPreviewBlockRenderer());
        registry.Register("video", new BlockRenderers.VideoBlockRenderer());
        registry.Register("embed", new BlockRenderers.EmbedBlockRenderer());
        registry.Register("equation", new BlockRenderers.EquationBlockRenderer());
        registry.Register("table", new BlockRenderers.TableBlockRenderer());
        registry.Register("file", new BlockRenderers.FileBlockRenderer());
        registry.Register("pdf", new BlockRenderers.PdfBlockRenderer());
        registry.Register("audio", new BlockRenderers.AudioBlockRenderer());
        registry.Register("child_page", new BlockRenderers.ChildEntityBlockRenderer("child_page"));
        registry.Register("child_database", new BlockRenderers.ChildEntityBlockRenderer("child_database"));
        registry.Register("synced_block", new BlockRenderers.SyncedBlockRenderer());
        registry.Register("column_list", new BlockRenderers.ColumnListBlockRenderer());
        registry.Register("column", new BlockRenderers.ColumnBlockRenderer());
        registry.Register("table_of_contents", new BlockRenderers.TableOfContentsBlockRenderer());
        registry.Register("link_to_page", new BlockRenderers.LinkToPageBlockRenderer());
        registry.Register("breadcrumb", INotionBlockRenderer.NoOp);
        registry.Register("template", INotionBlockRenderer.NoOp);
        return registry;
    }

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
