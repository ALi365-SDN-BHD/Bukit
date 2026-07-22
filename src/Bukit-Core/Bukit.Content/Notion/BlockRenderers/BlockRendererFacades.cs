using System.Text.Json;
using Canonical = Bukit.Notion.Rendering.BlockRenderers;

namespace Bukit.Content.Notion.BlockRenderers;

internal static class BlockRendererFacade
{
    internal static Task<string?> RenderAsync(
        Bukit.Notion.Rendering.INotionBlockRenderer renderer,
        JsonElement block,
        NotionRenderContext context,
        CancellationToken cancellationToken)
        => renderer.RenderAsync(
            block,
            context is null ? null! : context.Inner,
            cancellationToken);
}

public sealed class AudioBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.AudioBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class BookmarkBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.BookmarkBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class CalloutBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.CalloutBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class ChildEntityBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.ChildEntityBlockRenderer _inner;
    public ChildEntityBlockRenderer(string typeName) => _inner = new Canonical.ChildEntityBlockRenderer(typeName);
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class CodeBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.CodeBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class ColumnBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.ColumnBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class ColumnListBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.ColumnListBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class DividerBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.DividerBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class EmbedBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.EmbedBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class EquationBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.EquationBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class FileBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.FileBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class ImageBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.ImageBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class LinkPreviewBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.LinkPreviewBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class LinkToPageBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.LinkToPageBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class NoOpBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.NoOpBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class PdfBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.PdfBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class RichTextContainerRenderer : INotionBlockRenderer
{
    private readonly Canonical.RichTextContainerRenderer _inner;
    public RichTextContainerRenderer(string containerName, string tag)
        => _inner = new Canonical.RichTextContainerRenderer(containerName, tag);
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class SyncedBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.SyncedBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class TableBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.TableBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class TableOfContentsBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.TableOfContentsBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class ToDoBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.ToDoBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class ToggleBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.ToggleBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

public sealed class VideoBlockRenderer : INotionBlockRenderer
{
    private readonly Canonical.VideoBlockRenderer _inner = new();
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        => BlockRendererFacade.RenderAsync(_inner, block, context, cancellationToken);
}

internal static class NotionBlockHelpers
{
    internal static string? GetString(JsonElement obj, string name)
        => Canonical.NotionBlockHelpers.GetString(obj, name);

    internal static string ExtractPlainText(JsonElement richTextArray)
        => Canonical.NotionBlockHelpers.ExtractPlainText(richTextArray);

    internal static string GetBlockColorClass(JsonElement typeContainer)
        => Canonical.NotionBlockHelpers.GetBlockColorClass(typeContainer);

    internal static string? GetBlockColor(JsonElement typeContainer)
        => Canonical.NotionBlockHelpers.GetBlockColor(typeContainer);

    internal static string? ExtractFileUrl(JsonElement container)
        => Canonical.NotionBlockHelpers.ExtractFileUrl(container);

    internal static string NotionBlockColorToCssBackground(string notionColor)
        => Canonical.NotionBlockHelpers.NotionBlockColorToCssBackground(notionColor);

    internal static bool IsYouTubeUrl(string url, out string embedUrl)
        => Canonical.NotionBlockHelpers.IsYouTubeUrl(url, out embedUrl);

    internal static string? ExtractQueryParam(string url, string paramName)
        => Canonical.NotionBlockHelpers.ExtractQueryParam(url, paramName);
}
