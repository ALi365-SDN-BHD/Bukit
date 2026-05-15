using System.Text.Json;
using Bukit.Content.Notion.BlockRenderers;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class BlockRendererExtendedTests
{
    [Fact]
    public async Task ToggleBlockRenderer_Basic_ReturnsDetailsElement()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "blk-1",
          "has_children": false,
          "toggle": {
            "rich_text": [
              { "plain_text": "Click to expand" }
            ]
          }
        }
        """);

        var renderer = new ToggleBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<details", html);
        Assert.Contains("<summary>Click to expand</summary>", html);
        Assert.Contains("</details>", html);
    }

    [Fact]
    public async Task ToggleBlockRenderer_WithColor_AddsColorClass()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "blk-2",
          "has_children": false,
          "toggle": {
            "rich_text": [
              { "plain_text": "Colored toggle" }
            ],
            "color": "green"
          }
        }
        """);

        var renderer = new ToggleBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"notion-green\"", html);
        Assert.Contains("<summary>Colored toggle</summary>", html);
    }

    [Fact]
    public async Task FileBlockRenderer_ExternalUrl_ReturnsLink()
    {
        using var doc = JsonDocument.Parse("""
        {
          "file": {
            "type": "external",
            "external": { "url": "https://cdn.example.com/report.pdf" },
            "caption": []
          }
        }
        """);

        var renderer = new FileBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"notion-file\"", html);
        Assert.Contains("href=\"https://cdn.example.com/report.pdf\"", html);
    }

    [Fact]
    public async Task FileBlockRenderer_NoUrl_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "file": {
            "type": "file",
            "file": {}
          }
        }
        """);

        var renderer = new FileBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task ToDoBlockRenderer_Unchecked_ReturnsUncheckedCheckbox()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "todo-1",
          "has_children": false,
          "to_do": {
            "rich_text": [
              { "plain_text": "Buy milk" }
            ],
            "checked": false
          }
        }
        """);

        var renderer = new ToDoBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"to-do\"", html);
        Assert.Contains("type=\"checkbox\"", html);
        Assert.DoesNotContain("checked", html);
        Assert.Contains("Buy milk", html);
    }

    [Fact]
    public async Task ToDoBlockRenderer_Checked_ReturnsCheckedCheckbox()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "todo-2",
          "has_children": false,
          "to_do": {
            "rich_text": [
              { "plain_text": "Done task" }
            ],
            "checked": true
          }
        }
        """);

        var renderer = new ToDoBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("checked", html);
        Assert.Contains("Done task", html);
    }

    [Fact]
    public async Task ToDoBlockRenderer_WithColor_AddsNotionColorClass()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "todo-3",
          "has_children": false,
          "to_do": {
            "rich_text": [
              { "plain_text": "Urgent item" }
            ],
            "checked": false,
            "color": "red"
          }
        }
        """);

        var renderer = new ToDoBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("to-do notion-red", html);
        Assert.Contains("Urgent item", html);
    }

    [Fact]
    public async Task CodeBlockRenderer_WithLanguage_AddsLanguageClass()
    {
        using var doc = JsonDocument.Parse("""
        {
          "code": {
            "language": "python",
            "rich_text": [
              { "plain_text": "print(1)" }
            ]
          }
        }
        """);

        var renderer = new CodeBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<pre><code class=\"language-python\">", html);
        Assert.Contains("print(1)", html);
    }

    [Fact]
    public async Task CodeBlockRenderer_WithoutLanguage_NoLanguageClass()
    {
        using var doc = JsonDocument.Parse("""
        {
          "code": {
            "rich_text": [
              { "plain_text": "plain code" }
            ]
          }
        }
        """);

        var renderer = new CodeBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<pre><code>", html);
        Assert.DoesNotContain("language-", html);
        Assert.Contains("plain code", html);
    }

    [Fact]
    public async Task ColumnListBlockRenderer_NoChildren_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "col-list-1",
          "has_children": false,
          "column_list": {}
        }
        """);

        var renderer = new ColumnListBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task ColumnListBlockRenderer_NoId_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "has_children": true,
          "column_list": {}
        }
        """);

        var renderer = new ColumnListBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task LinkPreviewBlockRenderer_WithUrl_ReturnsAnchor()
    {
        using var doc = JsonDocument.Parse("""
        {
          "link_preview": {
            "url": "https://example.com/article"
          }
        }
        """);

        var renderer = new LinkPreviewBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<a href=\"https://example.com/article\"", html);
        Assert.Contains("class=\"bookmark notion-link-preview\"", html);
    }

    [Fact]
    public async Task LinkPreviewBlockRenderer_WithoutUrl_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "link_preview": {
            "url": ""
          }
        }
        """);

        var renderer = new LinkPreviewBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }
}
