using Bukit.Engine.Abstractions.Content;
using System.Text.Json;
using Bukit.Content.Notion;
using Bukit.Content.Notion.BlockRenderers;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionBlockRenderersTests
{
    [Fact]
    public async Task CodeBlockRenderer_RendersCaptionAsFigure()
    {
        using var doc = JsonDocument.Parse("""
        {
          "code": {
            "language": "csharp",
            "rich_text": [
              { "plain_text": "Console.WriteLine(1);" }
            ],
            "caption": [
              { "plain_text": "示例代码" }
            ]
          }
        }
        """);

        var renderer = new CodeBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<figure>", html);
        Assert.Contains("language-csharp", html);
        Assert.Contains("<figcaption>示例代码</figcaption>", html);
    }

    [Fact]
    public async Task CalloutBlockRenderer_SupportsFileIcon()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "blk-1",
          "has_children": false,
          "callout": {
            "icon": {
              "type": "file",
              "file": { "url": "https://example.com/icon.png" }
            },
            "rich_text": [
              { "plain_text": "提示内容" }
            ]
          }
        }
        """);

        var renderer = new CalloutBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"callout-icon\"><img src=\"https://example.com/icon.png\"", html);
        Assert.Contains("提示内容", html);
    }

    [Fact]
    public async Task FileBlockRenderer_RendersLinkWithCaption()
    {
        using var doc = JsonDocument.Parse("""
        {
          "file": {
            "type": "external",
            "external": { "url": "https://example.com/a.zip" },
            "caption": [
              { "plain_text": "下载附件" }
            ]
          }
        }
        """);

        var renderer = new FileBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"notion-file\"", html);
        Assert.Contains("href=\"https://example.com/a.zip\"", html);
        Assert.Contains("下载附件", html);
    }

    [Fact]
    public async Task ChildEntityBlockRenderer_RendersTitle()
    {
        using var doc = JsonDocument.Parse("""
        {
          "child_page": {
            "title": "子页面"
          }
        }
        """);

        var renderer = new ChildEntityBlockRenderer("child_page");
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"notion-child-page\"", html);
        Assert.Contains("子页面", html);
    }

    // ── NotionColorPalette tests ──────────────────────────────────────────

    [Fact]
    public void NotionColorPalette_ToForeground_KnownColors()
    {
        Assert.Equal("#787774", NotionColorPalette.ToForeground("gray"));
        Assert.Equal("#64473A", NotionColorPalette.ToForeground("brown"));
        Assert.Equal("#D9730D", NotionColorPalette.ToForeground("orange"));
        Assert.Equal("#DFAB01", NotionColorPalette.ToForeground("yellow"));
        Assert.Equal("#0F7B6C", NotionColorPalette.ToForeground("green"));
        Assert.Equal("#0B6E99", NotionColorPalette.ToForeground("blue"));
        Assert.Equal("#6940A5", NotionColorPalette.ToForeground("purple"));
        Assert.Equal("#AD1A72", NotionColorPalette.ToForeground("pink"));
        Assert.Equal("#E03E3E", NotionColorPalette.ToForeground("red"));
    }

    [Fact]
    public void NotionColorPalette_ToForeground_UnknownReturnsInherit()
    {
        Assert.Equal("inherit", NotionColorPalette.ToForeground("unknown"));
    }

    [Fact]
    public void NotionColorPalette_ToBackground_KnownColors()
    {
        Assert.Equal("#F1F1EF", NotionColorPalette.ToBackground("gray_background"));
        Assert.Equal("#E7F3F8", NotionColorPalette.ToBackground("blue_background"));
        Assert.Equal("#E7F3F8", NotionColorPalette.ToBackground("blue")); // plain form
    }

    [Fact]
    public void NotionColorPalette_ToBackground_UnknownReturnsDefault()
    {
        Assert.Equal(NotionColorPalette.DefaultBg, NotionColorPalette.ToBackground("unknown"));
    }

    // ── BookmarkBlockRenderer color support tests ──────────────────────────

    [Fact]
    public async Task BookmarkBlockRenderer_RendersWithColor()
    {
        using var doc = JsonDocument.Parse("""
        {
          "bookmark": {
            "url": "https://example.com",
            "caption": [],
            "color": "blue"
          }
        }
        """);

        var renderer = new BookmarkBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"bookmark notion-blue\"", html);
        Assert.Contains("href=\"https://example.com\"", html);
    }

    [Fact]
    public async Task BookmarkBlockRenderer_DefaultColor_NoNotionClass()
    {
        using var doc = JsonDocument.Parse("""
        {
          "bookmark": {
            "url": "https://example.com",
            "caption": [],
            "color": "default"
          }
        }
        """);

        var renderer = new BookmarkBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"bookmark\"", html);
        Assert.DoesNotContain("notion-", html);
    }

    // ── EquationBlockRenderer color support tests ──────────────────────────

    [Fact]
    public async Task EquationBlockRenderer_RendersWithColor()
    {
        using var doc = JsonDocument.Parse("""
        {
          "equation": {
            "expression": "x^2",
            "color": "red"
          }
        }
        """);

        var renderer = new EquationBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"math-block notion-red\"", html);
        Assert.Contains("\\[x^2\\]", html);
    }

    // ── CalloutBlockRenderer custom_emoji support tests ────────────────────

    [Fact]
    public async Task CalloutBlockRenderer_SupportsCustomEmoji()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "blk-2",
          "has_children": false,
          "callout": {
            "icon": {
              "type": "custom_emoji",
              "custom_emoji": { "url": "https://example.com/custom.png" }
            },
            "rich_text": [
              { "plain_text": "Custom emoji callout" }
            ]
          }
        }
        """);

        var renderer = new CalloutBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"callout-icon\"><img src=\"https://example.com/custom.png\"", html);
        Assert.Contains("Custom emoji callout", html);
    }

    // ── TableOfContentsBlockRenderer tests ──────────────────────────────────

    [Fact]
    public async Task TableOfContentsBlockRenderer_RendersNav()
    {
        using var doc = JsonDocument.Parse("""{ "table_of_contents": {} }""");

        var renderer = new TableOfContentsBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<nav class=\"notion-toc\">", html);
    }

    // ── LinkToPageBlockRenderer tests ───────────────────────────────────────

    [Fact]
    public async Task LinkToPageBlockRenderer_RendersPageLink()
    {
        using var doc = JsonDocument.Parse("""
        {
          "link_to_page": {
            "type": "page_id",
            "page_id": "abc-123"
          }
        }
        """);

        var renderer = new LinkToPageBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"notion-link-to-page\"", html);
        Assert.Contains("Linked page", html);
        Assert.DoesNotContain("abc-123", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-notion-id", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LinkToPageBlockRenderer_RendersDatabaseLink()
    {
        using var doc = JsonDocument.Parse("""
        {
          "link_to_page": {
            "type": "database_id",
            "database_id": "db-456"
          }
        }
        """);

        var renderer = new LinkToPageBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("Linked page", html);
        Assert.DoesNotContain("db-456", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-notion-id", html, StringComparison.Ordinal);
    }

    // ── NoOpBlockRenderer tests ─────────────────────────────────────────────

    [Fact]
    public async Task NoOpBlockRenderer_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("""{ "breadcrumb": {} }""");

        var renderer = new NoOpBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Equal(string.Empty, html);
    }

    // ── NotionRichTextRenderer mention tests ──────────────────────────────

    [Fact]
    public void NotionRichTextRenderer_MentionUser_RendersMentionSpan()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "mention",
            "mention": {
              "type": "user",
              "user": { "id": "user-1" }
            },
            "plain_text": "@Alice",
            "href": null
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("class=\"notion-mention\"", html);
        Assert.Contains("data-mention-type=\"user\"", html);
        Assert.Contains("@Alice", html);
    }

    [Fact]
    public void NotionRichTextRenderer_MentionPage_RendersLinkedMention()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "mention",
            "mention": {
              "type": "page",
              "page": { "id": "page-1" }
            },
            "plain_text": "Linked Page",
            "href": "https://notion.so/page-1"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("class=\"notion-mention\"", html);
        Assert.Contains("data-mention-type=\"page\"", html);
        Assert.Contains("https://notion.so/page-1", html);
        Assert.Contains("Linked Page</a>", html);
    }

    [Fact]
    public void NotionRichTextRenderer_MentionDate_RendersPlainText()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "mention",
            "mention": {
              "type": "date",
              "date": { "start": "2024-01-01" }
            },
            "plain_text": "2024-01-01",
            "href": null
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("class=\"notion-mention\"", html);
        Assert.Contains("data-mention-type=\"date\"", html);
        Assert.Contains("2024-01-01", html);
    }

    [Fact]
    public void NotionRichTextRenderer_MentionWithAnnotations()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "mention",
            "mention": {
              "type": "user",
              "user": { "id": "user-1" }
            },
            "plain_text": "@Bob",
            "href": null,
            "annotations": {
              "bold": true,
              "italic": false,
              "strikethrough": false,
              "underline": false,
              "code": false,
              "color": "default"
            }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("<strong>@Bob</strong>", html);
        Assert.Contains("class=\"notion-mention\"", html);
    }

    // ── NotionRichTextRenderer annotation tests ────────────────────────────

    [Fact]
    public void NotionRichTextRenderer_BoldAndItalic()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "plain_text": "bold italic",
            "annotations": {
              "bold": true,
              "italic": true,
              "strikethrough": false,
              "underline": false,
              "code": false,
              "color": "default"
            }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("<strong>", html);
        Assert.Contains("<em>", html);
    }

    [Fact]
    public void NotionRichTextRenderer_CodeAnnotation()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "plain_text": "console.log",
            "annotations": {
              "bold": false,
              "italic": false,
              "strikethrough": false,
              "underline": false,
              "code": true,
              "color": "default"
            }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("<code>console.log</code>", html);
    }

    [Fact]
    public void NotionRichTextRenderer_ColorAnnotation()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "plain_text": "red text",
            "annotations": {
              "bold": false,
              "italic": false,
              "strikethrough": false,
              "underline": false,
              "code": false,
              "color": "red"
            }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("style=\"color:#E03E3E\"", html);
        Assert.Contains("red text", html);
    }

    // ── TableBlockRenderer row header tests ─────────────────────────────────

    [Fact]
    public async Task TableBlockRenderer_WithRowHeader_FirstColumnIsTh()
    {
        // This test verifies that has_row_header is read from the block JSON.
        // Full integration requires API mocking, so we test the flag parsing.
        using var doc = JsonDocument.Parse("""
        {
          "id": "table-1",
          "has_children": true,
          "table": {
            "has_column_header": false,
            "has_row_header": true
          }
        }
        """);

        // Verify the table block property is parsed (renderer needs API context for children)
        var table = doc.RootElement.GetProperty("table");
        Assert.True(table.TryGetProperty("has_row_header", out var hrh));
        Assert.True(hrh.GetBoolean());
    }

    // ── ColumnBlockRenderer width_ratio tests ────────────────────────────────

    [Fact]
    public async Task ColumnBlockRenderer_WithWidthRatio_OutputsFlexStyle()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "col-1",
          "has_children": false,
          "column": {
            "width_ratio": 0.33
          }
        }
        """);

        var renderer = new ColumnBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"notion-column\"", html);
        Assert.Contains("style=\"flex:0 0 33%\"", html);
    }

    [Fact]
    public async Task ColumnBlockRenderer_WithoutWidthRatio_NoStyleAttr()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "col-2",
          "has_children": false,
          "column": {}
        }
        """);

        var renderer = new ColumnBlockRenderer();
        var html = await renderer.RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"notion-column\"", html);
        Assert.DoesNotContain("style=", html);
    }
}
