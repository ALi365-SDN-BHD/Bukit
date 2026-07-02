using Bukit.Engine.Abstractions.Content;
using System.Text.Json;

namespace Bukit.Content.Notion;

/// <summary>
/// A custom transformer function that converts a Notion block to HTML.
/// Return <c>null</c> to fall back to the default renderer for the block type.
/// </summary>
public delegate Task<string?> NotionBlockTransformer(
    JsonElement block,
    NotionRenderContext context,
    CancellationToken cancellationToken);

/// <summary>
/// Registry that maps Notion block type strings to <see cref="INotionBlockRenderer"/> instances.
/// Supports registering custom transformers that override built-in renderers.
/// </summary>
public sealed class NotionBlockRendererRegistry
{
    private readonly Dictionary<string, INotionBlockRenderer> _renderers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NotionBlockTransformer> _customTransformers = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a built-in renderer for the given block type.
    /// </summary>
    public NotionBlockRendererRegistry Register(string blockType, INotionBlockRenderer renderer)
    {
        _renderers[blockType] = renderer;
        return this;
    }

    /// <summary>
    /// Registers a custom transformer for the given block type.
    /// Custom transformers take precedence over built-in renderers.
    /// If the custom transformer returns <c>null</c>, the built-in renderer is used as fallback.
    /// </summary>
    public NotionBlockRendererRegistry SetCustomTransformer(string blockType, NotionBlockTransformer transformer)
    {
        _customTransformers[blockType] = transformer;
        return this;
    }

    /// <summary>
    /// Removes a previously registered custom transformer for the given block type.
    /// </summary>
    public NotionBlockRendererRegistry RemoveCustomTransformer(string blockType)
    {
        _customTransformers.Remove(blockType);
        return this;
    }

    /// <summary>
    /// Renders the given block using the registered custom transformer or built-in renderer.
    /// Returns <c>null</c> if no renderer is found for the block type.
    /// </summary>
    internal async Task<string?> RenderBlockAsync(
        string blockType,
        JsonElement block,
        NotionRenderContext context,
        CancellationToken cancellationToken)
    {
        // Custom transformers take priority
        if (_customTransformers.TryGetValue(blockType, out var transformer))
        {
            var customResult = await transformer(block, context, cancellationToken);
            if (customResult is not null)
            {
                return customResult;
            }

            // Custom transformer returned null → fall through to built-in
        }

        if (_renderers.TryGetValue(blockType, out var renderer))
        {
            return await renderer.RenderAsync(block, context, cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// Creates a registry pre-populated with all built-in Notion block renderers.
    /// </summary>
    public static NotionBlockRendererRegistry CreateDefault()
    {
        var registry = new NotionBlockRendererRegistry();

        // Rich-text container blocks
        registry.Register("paragraph", new BlockRenderers.RichTextContainerRenderer("paragraph", "p"));
        registry.Register("heading_1", new BlockRenderers.RichTextContainerRenderer("heading_1", "h1"));
        registry.Register("heading_2", new BlockRenderers.RichTextContainerRenderer("heading_2", "h2"));
        registry.Register("heading_3", new BlockRenderers.RichTextContainerRenderer("heading_3", "h3"));
        registry.Register("quote", new BlockRenderers.RichTextContainerRenderer("quote", "blockquote"));

        // Specialized blocks
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
}
