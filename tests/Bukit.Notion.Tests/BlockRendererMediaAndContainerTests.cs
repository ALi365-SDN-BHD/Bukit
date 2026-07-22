using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Notion.Rendering.BlockRenderers;
using Bukit.Notion.Transport;
using static Bukit.Notion.Tests.CanonicalBlockRendererTestSupport;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class BlockRendererMediaAndContainerTests
{
    [Fact]
    public async Task AudioBlockRenderer_ExternalUrl_RendersAudioLinkAndCaption()
    {
        using var doc = JsonDocument.Parse("""
        {
          "audio": {
            "type": "external",
            "external": { "url": "https://cdn.example.com/audio.mp3?x=1&y=2" },
            "caption": [{ "plain_text": "Listen now" }]
          }
        }
        """);

        var html = await new AudioBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<audio controls src=\"https://cdn.example.com/audio.mp3?x=1&amp;y=2\"></audio>", html);
        Assert.Contains("<a href=\"https://cdn.example.com/audio.mp3?x=1&amp;y=2\" rel=\"noopener noreferrer\">Audio</a>", html);
        Assert.Contains("<p>Listen now</p>", html);
    }

    [Fact]
    public async Task ImageBlockRenderer_WithCaption_RendersFigure()
    {
        using var doc = JsonDocument.Parse("""
        {
          "image": {
            "type": "file",
            "file": { "url": "https://cdn.example.com/image.png" },
            "caption": [{ "plain_text": "Hero image" }]
          }
        }
        """);

        var html = await new ImageBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Equal("<figure><img src=\"https://cdn.example.com/image.png\" alt=\"\" /><figcaption>Hero image</figcaption></figure>", html);
    }

    [Fact]
    public async Task PdfBlockRenderer_WithoutCaption_UsesPdfLinkText()
    {
        using var doc = JsonDocument.Parse("""
        {
          "pdf": {
            "type": "external",
            "external": { "url": "https://cdn.example.com/doc.pdf" },
            "caption": []
          }
        }
        """);

        var html = await new PdfBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Contains("href=\"https://cdn.example.com/doc.pdf\"", html);
        Assert.Contains(">PDF</a>", html);
    }

    [Fact]
    public async Task EmbedBlockRenderer_NonYouTubeUrl_RendersIframeFigureWithCaption()
    {
        using var doc = JsonDocument.Parse("""
        {
          "embed": {
            "url": "https://example.com/widget",
            "caption": [{ "plain_text": "Widget" }]
          }
        }
        """);

        var html = await new EmbedBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Equal("<figure><iframe src=\"https://example.com/widget\" frameborder=\"0\"></iframe><figcaption>Widget</figcaption></figure>", html);
    }

    [Fact]
    public async Task VideoBlockRenderer_YouTubeUrl_RendersEmbedIframe()
    {
        using var doc = JsonDocument.Parse("""
        {
          "video": {
            "type": "external",
            "external": { "url": "https://www.youtube.com/watch?v=abc123" },
            "caption": []
          }
        }
        """);

        var html = await new VideoBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"video-embed\"", html);
        Assert.Contains("https://www.youtube.com/embed/abc123", html);
    }

    [Fact]
    public async Task VideoBlockRenderer_FileUrl_RendersVideoWithCaption()
    {
        using var doc = JsonDocument.Parse("""
        {
          "video": {
            "type": "file",
            "file": { "url": "https://cdn.example.com/video.mp4" },
            "caption": [{ "plain_text": "Demo video" }]
          }
        }
        """);

        var html = await new VideoBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Equal("<video src=\"https://cdn.example.com/video.mp4\" controls></video><p>Demo video</p>", html);
    }

    [Fact]
    public async Task MediaBlockRenderers_WhenPayloadOrUrlMissing_ReturnNull()
    {
        using var empty = JsonDocument.Parse("{}");
        using var imageWithoutUrl = JsonDocument.Parse("""
        {
          "image": {
            "type": "external",
            "external": {}
          }
        }
        """);
        using var pdfWithoutUrl = JsonDocument.Parse("""
        {
          "pdf": {
            "type": "file",
            "file": {}
          }
        }
        """);
        using var embedWithoutUrl = JsonDocument.Parse("""
        {
          "embed": {
            "url": ""
          }
        }
        """);
        using var videoWithoutUrl = JsonDocument.Parse("""
        {
          "video": {
            "type": "file",
            "file": {}
          }
        }
        """);

        Assert.Null(await new AudioBlockRenderer().RenderAsync(empty.RootElement, null!, CancellationToken.None));
        Assert.Null(await new ImageBlockRenderer().RenderAsync(imageWithoutUrl.RootElement, null!, CancellationToken.None));
        Assert.Null(await new PdfBlockRenderer().RenderAsync(pdfWithoutUrl.RootElement, null!, CancellationToken.None));
        Assert.Null(await new EmbedBlockRenderer().RenderAsync(embedWithoutUrl.RootElement, null!, CancellationToken.None));
        Assert.Null(await new VideoBlockRenderer().RenderAsync(videoWithoutUrl.RootElement, null!, CancellationToken.None));
    }

    [Fact]
    public async Task SyncedBlockRenderer_WithChildren_RendersChildHtml()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "sync-1",
          "has_children": true,
          "synced_block": {}
        }
        """);
        using var client = CreateClient(new JsonHandler(req =>
        {
            Assert.Equal("https://api.notion.com/v1/blocks/sync-1/children?page_size=100", req.RequestUri!.AbsoluteUri);
            return """
            {
              "has_more": false,
              "results": [
                {
                  "type": "paragraph",
                  "id": "p-1",
                  "paragraph": { "rich_text": [{ "plain_text": "Synced child" }] }
                }
              ]
            }
            """;
        }));
        var renderer = new NotionBlocksRenderer(client);
        var context = new NotionRenderContext(renderer, client);

        var html = await new SyncedBlockRenderer().RenderAsync(doc.RootElement, context, CancellationToken.None);

        Assert.Equal("<div class=\"notion-synced-block\"><p>Synced child</p>\n</div>", html);
    }

    [Fact]
    public async Task SyncedBlockRenderer_WithoutChildrenOrId_ReturnsNull()
    {
        using var noChildren = JsonDocument.Parse("""
        {
          "id": "sync-1",
          "has_children": false,
          "synced_block": {}
        }
        """);
        using var noId = JsonDocument.Parse("""
        {
          "has_children": true,
          "synced_block": {}
        }
        """);

        var renderer = new SyncedBlockRenderer();

        Assert.Null(await renderer.RenderAsync(JsonDocument.Parse("{}").RootElement, null!, CancellationToken.None));
        Assert.Null(await renderer.RenderAsync(noChildren.RootElement, null!, CancellationToken.None));
        Assert.Null(await renderer.RenderAsync(noId.RootElement, null!, CancellationToken.None));
    }

    [Fact]
    public async Task DividerBlockRenderer_RendersHr()
    {
        using var doc = JsonDocument.Parse("{}");

        var html = await new DividerBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Equal("<hr />", html);
    }

    [Fact]
    public async Task RichTextContainerRenderer_ParagraphWithChildren_AppendsRenderedChildren()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "p-parent",
          "has_children": true,
          "paragraph": {
            "rich_text": [{ "plain_text": "Parent" }]
          }
        }
        """);
        using var client = CreateClient(new JsonHandler(req =>
        {
            Assert.Equal("https://api.notion.com/v1/blocks/p-parent/children?page_size=100", req.RequestUri!.AbsoluteUri);
            return """
            {
              "has_more": false,
              "results": [
                {
                  "type": "paragraph",
                  "id": "p-child",
                  "paragraph": { "rich_text": [{ "plain_text": "Child" }] }
                }
              ]
            }
            """;
        }));
        var renderer = new NotionBlocksRenderer(client);
        var context = new NotionRenderContext(renderer, client);

        var html = await new RichTextContainerRenderer("paragraph", "p").RenderAsync(doc.RootElement, context, CancellationToken.None);

        Assert.Equal("<p>Parent</p><p>Child</p>\n", html);
    }

    [Fact]
    public async Task RichTextContainerRenderer_ToggleableHeading_RendersDetailsWithChildren()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "heading-1",
          "has_children": true,
          "heading_2": {
            "is_toggleable": true,
            "color": "blue",
            "rich_text": [{ "plain_text": "Toggle heading" }]
          }
        }
        """);
        using var client = CreateClient(new JsonHandler(req =>
        {
            Assert.Equal("https://api.notion.com/v1/blocks/heading-1/children?page_size=100", req.RequestUri!.AbsoluteUri);
            return """
            {
              "has_more": false,
              "results": [
                {
                  "type": "paragraph",
                  "id": "p-child",
                  "paragraph": { "rich_text": [{ "plain_text": "Nested under heading" }] }
                }
              ]
            }
            """;
        }));
        var renderer = new NotionBlocksRenderer(client);
        var context = new NotionRenderContext(renderer, client);

        var html = await new RichTextContainerRenderer("heading_2", "h2").RenderAsync(doc.RootElement, context, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<details class=\"notion-blue\">", html);
        Assert.Contains("<summary><h2>Toggle heading</h2></summary>", html);
        Assert.Contains("<p>Nested under heading</p>", html);
    }

    [Fact]
    public async Task TableBlockRenderer_WithColumnAndRowHeaders_RendersThCells()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "table-1",
          "has_children": true,
          "table": {
            "has_column_header": true,
            "has_row_header": true
          }
        }
        """);
        using var client = CreateClient(new JsonHandler(req =>
        {
            Assert.Equal("https://api.notion.com/v1/blocks/table-1/children?page_size=100", req.RequestUri!.AbsoluteUri);
            return """
            {
              "has_more": false,
              "results": [
                {
                  "type": "table_row",
                  "table_row": {
                    "cells": [
                      [{ "plain_text": "Name" }],
                      [{ "plain_text": "Value" }]
                    ]
                  }
                },
                {
                  "type": "table_row",
                  "table_row": {
                    "cells": [
                      [{ "plain_text": "Size" }],
                      [{ "plain_text": "Large" }]
                    ]
                  }
                }
              ]
            }
            """;
        }));
        var renderer = new NotionBlocksRenderer(client);
        var context = new NotionRenderContext(renderer, client);

        var html = await new TableBlockRenderer().RenderAsync(doc.RootElement, context, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<th>Name</th><th>Value</th>", html);
        Assert.Contains("<th>Size</th><td>Large</td>", html);
    }

    [Fact]
    public async Task TableBlockRenderer_WhenMissingTableChildrenOrRows_ReturnsNull()
    {
        using var missingTable = JsonDocument.Parse("{}");
        using var noChildren = JsonDocument.Parse("""
        {
          "id": "table-1",
          "has_children": false,
          "table": {}
        }
        """);
        using var noId = JsonDocument.Parse("""
        {
          "has_children": true,
          "table": {}
        }
        """);
        using var noRows = JsonDocument.Parse("""
        {
          "id": "table-1",
          "has_children": true,
          "table": {}
        }
        """);
        using var client = CreateClient(new JsonHandler(_ => """
        {
          "has_more": false,
          "results": []
        }
        """));
        var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);
        var renderer = new TableBlockRenderer();

        Assert.Null(await renderer.RenderAsync(missingTable.RootElement, context, CancellationToken.None));
        Assert.Null(await renderer.RenderAsync(noChildren.RootElement, context, CancellationToken.None));
        Assert.Null(await renderer.RenderAsync(noId.RootElement, context, CancellationToken.None));
        Assert.Null(await renderer.RenderAsync(noRows.RootElement, context, CancellationToken.None));
    }

    [Fact]
    public async Task ColumnRenderers_WithChildren_RenderWrappedColumns()
    {
        using var columnListDoc = JsonDocument.Parse("""
        {
          "id": "columns-1",
          "has_children": true,
          "column_list": {}
        }
        """);
        using var columnDoc = JsonDocument.Parse("""
        {
          "id": "column-1",
          "has_children": true,
          "column": {
            "width_ratio": 0.4
          }
        }
        """);
        using var client = CreateClient(new JsonHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("columns-1", StringComparison.Ordinal))
            {
                return """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "column",
                      "id": "column-1",
                      "has_children": false,
                      "column": {}
                    }
                  ]
                }
                """;
            }

            return """
            {
              "has_more": false,
              "results": [
                {
                  "type": "paragraph",
                  "id": "p-1",
                  "paragraph": { "rich_text": [{ "plain_text": "Column body" }] }
                }
              ]
            }
            """;
        }));
        var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);

        var columnListHtml = await new ColumnListBlockRenderer().RenderAsync(columnListDoc.RootElement, context, CancellationToken.None);
        var columnHtml = await new ColumnBlockRenderer().RenderAsync(columnDoc.RootElement, context, CancellationToken.None);

        Assert.Equal("<div class=\"notion-columns\"><div class=\"notion-column\"></div>\n</div>", columnListHtml);
        Assert.Equal("<div class=\"notion-column\" style=\"flex:0 0 40%\"><p>Column body</p>\n</div>", columnHtml);
    }

    [Fact]
    public async Task ColumnRenderers_WhenMissingInputs_ReturnNullOrEmptyColumn()
    {
        using var missingList = JsonDocument.Parse("{}");
        using var listNoChildren = JsonDocument.Parse("""{"id":"columns-1","has_children":false,"column_list":{}}""");
        using var listNoId = JsonDocument.Parse("""{"has_children":true,"column_list":{}}""");
        using var columnNoChildren = JsonDocument.Parse("""{"id":"column-1","has_children":false,"column":{"width_ratio":2}}""");
        var context = new NotionRenderContext(new NotionBlocksRenderer(CreateClient(new JsonHandler(_ => "{}"))), CreateClient(new JsonHandler(_ => "{}")));

        Assert.Null(await new ColumnListBlockRenderer().RenderAsync(missingList.RootElement, context, CancellationToken.None));
        Assert.Null(await new ColumnListBlockRenderer().RenderAsync(listNoChildren.RootElement, context, CancellationToken.None));
        Assert.Null(await new ColumnListBlockRenderer().RenderAsync(listNoId.RootElement, context, CancellationToken.None));
        Assert.Equal("<div class=\"notion-column\"></div>", await new ColumnBlockRenderer().RenderAsync(columnNoChildren.RootElement, context, CancellationToken.None));
    }

    [Fact]
    public async Task ToDoAndToggleRenderers_RenderColorsCheckedAndChildren()
    {
        using var toDoDoc = JsonDocument.Parse("""
        {
          "id": "todo-1",
          "has_children": true,
          "to_do": {
            "checked": true,
            "color": "green_background",
            "rich_text": [{ "plain_text": "Ship tests" }]
          }
        }
        """);
        using var toggleDoc = JsonDocument.Parse("""
        {
          "id": "toggle-1",
          "has_children": true,
          "toggle": {
            "color": "red",
            "rich_text": [{ "plain_text": "More" }]
          }
        }
        """);
        using var client = CreateClient(new JsonHandler(req =>
        {
            var text = req.RequestUri!.AbsoluteUri.Contains("todo-1", StringComparison.Ordinal) ? "Todo child" : "Toggle child";
            return $$"""
            {
              "has_more": false,
              "results": [
                {
                  "type": "paragraph",
                  "id": "p-1",
                  "paragraph": { "rich_text": [{ "plain_text": "{{text}}" }] }
                }
              ]
            }
            """;
        }));
        var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);

        var todoHtml = await new ToDoBlockRenderer().RenderAsync(toDoDoc.RootElement, context, CancellationToken.None);
        var toggleHtml = await new ToggleBlockRenderer().RenderAsync(toggleDoc.RootElement, context, CancellationToken.None);

        Assert.Contains("class=\"to-do notion-green_background\"", todoHtml);
        Assert.Contains("disabled checked", todoHtml);
        Assert.Contains("<div class=\"to-do-children\"><p>Todo child</p>", todoHtml);
        Assert.Equal("<details class=\"notion-red\"><summary>More</summary><p>Toggle child</p>\n</details>", toggleHtml);
    }

    [Fact]
    public async Task CalloutEquationBookmarkAndChildRenderers_CoverStyledAndMissingPaths()
    {
        using var calloutDoc = JsonDocument.Parse("""
        {
          "id": "callout-1",
          "has_children": true,
          "callout": {
            "icon": { "type": "custom_emoji", "custom_emoji": { "url": "https://cdn.example.com/icon.png?x=1&y=2" } },
            "color": "blue_background",
            "rich_text": [{ "plain_text": "Heads up" }]
          }
        }
        """);
        using var equationDoc = JsonDocument.Parse("""{"equation":{"expression":"x < y","color":"purple"}}""");
        using var bookmarkDoc = JsonDocument.Parse("""
        {
          "bookmark": {
            "url": "https://example.com?a=1&b=2",
            "color": "yellow",
            "caption": [{ "plain_text": "Example link" }]
          }
        }
        """);
        using var childDoc = JsonDocument.Parse("""{"child_page":{}}""");
        using var client = CreateClient(new JsonHandler(_ => """
        {
          "has_more": false,
          "results": [
            {
              "type": "paragraph",
              "id": "p-1",
              "paragraph": { "rich_text": [{ "plain_text": "Nested callout" }] }
            }
          ]
        }
        """));
        var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);

        var calloutHtml = await new CalloutBlockRenderer().RenderAsync(calloutDoc.RootElement, context, CancellationToken.None);
        var equationHtml = await new EquationBlockRenderer().RenderAsync(equationDoc.RootElement, context, CancellationToken.None);
        var bookmarkHtml = await new BookmarkBlockRenderer().RenderAsync(bookmarkDoc.RootElement, context, CancellationToken.None);
        var childHtml = await new ChildEntityBlockRenderer("child_page").RenderAsync(childDoc.RootElement, context, CancellationToken.None);

        Assert.Contains("class=\"callout notion-blue_background\"", calloutHtml);
        Assert.Contains("https://cdn.example.com/icon.png?x=1&amp;y=2", calloutHtml);
        Assert.Contains("<div class=\"callout-children\"><p>Nested callout</p>", calloutHtml);
        Assert.Equal("<div class=\"math-block notion-purple\">\\[x &lt; y\\]</div>", equationHtml);
        Assert.Contains("href=\"https://example.com?a=1&amp;b=2\"", bookmarkHtml);
        Assert.Contains("class=\"bookmark notion-yellow\"", bookmarkHtml);
        Assert.Contains("Example link</a>", bookmarkHtml);
        Assert.Equal("<p class=\"notion-child-page\">child_page</p>", childHtml);

        Assert.Null(await new EquationBlockRenderer().RenderAsync(JsonDocument.Parse("""{"equation":{"expression":" "}}""").RootElement, context, CancellationToken.None));
        Assert.Null(await new BookmarkBlockRenderer().RenderAsync(JsonDocument.Parse("""{"bookmark":{"url":" "}}""").RootElement, context, CancellationToken.None));
        Assert.Null(await new ChildEntityBlockRenderer("child_page").RenderAsync(JsonDocument.Parse("{}").RootElement, context, CancellationToken.None));
    }

    [Fact]
    public async Task ImageAndSimpleRenderers_WithMissingContainers_ReturnNull()
    {
        using var empty = JsonDocument.Parse("{}");

        Assert.Null(await new ImageBlockRenderer().RenderAsync(empty.RootElement, null!, CancellationToken.None));
        Assert.Null(await new EquationBlockRenderer().RenderAsync(empty.RootElement, null!, CancellationToken.None));
        Assert.Null(await new BookmarkBlockRenderer().RenderAsync(empty.RootElement, null!, CancellationToken.None));
        Assert.Null(await new ToggleBlockRenderer().RenderAsync(empty.RootElement, null!, CancellationToken.None));
        Assert.Null(await new CalloutBlockRenderer().RenderAsync(empty.RootElement, null!, CancellationToken.None));
        Assert.Null(await new ColumnBlockRenderer().RenderAsync(empty.RootElement, null!, CancellationToken.None));
    }
}
