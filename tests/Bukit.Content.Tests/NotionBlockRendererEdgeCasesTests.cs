using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion;
using Bukit.Content.Notion.BlockRenderers;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionBlockRendererEdgeCasesTests
{
    // ── LinkToPageBlockRenderer database_id and empty target ─────────────

    [Fact]
    public async Task LinkToPageBlockRenderer_DatabaseId_RendersDataAttribute()
    {
        using var doc = JsonDocument.Parse("""
        {
          "link_to_page": {
            "type": "database_id",
            "database_id": "db-789"
          }
        }
        """);

        var html = await new LinkToPageBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("data-notion-id=\"db-789\"", html);
    }

    [Fact]
    public async Task LinkToPageBlockRenderer_UnknownType_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "link_to_page": {
            "type": "unknown_type"
          }
        }
        """);

        var html = await new LinkToPageBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task LinkToPageBlockRenderer_EmptyTargetId_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "link_to_page": {
            "type": "page_id",
            "page_id": ""
          }
        }
        """);

        var html = await new LinkToPageBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    // ── EmbedBlockRenderer YouTube path ──────────────────────────────────

    [Fact]
    public async Task EmbedBlockRenderer_YouTubeUrl_RendersVideoEmbed()
    {
        using var doc = JsonDocument.Parse("""
        {
          "embed": {
            "url": "https://www.youtube.com/watch?v=abc123"
          }
        }
        """);

        var html = await new EmbedBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"video-embed\"", html);
        Assert.Contains("https://www.youtube.com/embed/abc123", html);
    }

    // ── ImageBlockRenderer without caption ───────────────────────────────

    [Fact]
    public async Task ImageBlockRenderer_WithoutCaption_ReturnsImgOnly()
    {
        using var doc = JsonDocument.Parse("""
        {
          "image": {
            "type": "external",
            "external": { "url": "https://cdn.example.com/img.png" }
          }
        }
        """);

        var html = await new ImageBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Equal("<img src=\"https://cdn.example.com/img.png\" alt=\"\" />", html);
    }

    // ── FileBlockRenderer and PdfBlockRenderer "file" type paths ─────────

    [Fact]
    public async Task FileBlockRenderer_FileTypeUrl_RendersLink()
    {
        using var doc = JsonDocument.Parse("""
        {
          "file": {
            "type": "file",
            "file": { "url": "https://cdn.example.com/data.bin" }
          }
        }
        """);

        var html = await new FileBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("href=\"https://cdn.example.com/data.bin\"", html);
        Assert.Contains("File", html);
    }

    [Fact]
    public async Task PdfBlockRenderer_FileTypeUrl_RendersLink()
    {
        using var doc = JsonDocument.Parse("""
        {
          "pdf": {
            "type": "file",
            "file": { "url": "https://cdn.example.com/doc.pdf" }
          }
        }
        """);

        var html = await new PdfBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("href=\"https://cdn.example.com/doc.pdf\"", html);
        Assert.Contains("<a", html);
    }

    [Fact]
    public async Task PdfBlockRenderer_WithCaption_RendersCaptionText()
    {
        using var doc = JsonDocument.Parse("""
        {
          "pdf": {
            "type": "external",
            "external": { "url": "https://cdn.example.com/doc.pdf" },
            "caption": [{ "plain_text": "Download report" }]
          }
        }
        """);

        var html = await new PdfBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("Download report", html);
    }

    // ── AudioBlockRenderer with empty caption ────────────────────────────

    [Fact]
    public async Task AudioBlockRenderer_WithoutCaption_RendersAudioLinkOnly()
    {
        using var doc = JsonDocument.Parse("""
        {
          "audio": {
            "type": "external",
            "external": { "url": "https://cdn.example.com/track.mp3" }
          }
        }
        """);

        var html = await new AudioBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<audio controls src=\"https://cdn.example.com/track.mp3\"></audio>", html);
        Assert.Contains("<a href=\"https://cdn.example.com/track.mp3\" rel=\"noopener noreferrer\">Audio</a>", html);
        Assert.DoesNotContain("<p><p>", html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("//evil.com/audio.mp3")]
    public async Task AudioBlockRenderer_DangerousUrl_ReturnsNull(string fileUrl)
    {
        var json = $"{{\"audio\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new AudioBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task AudioBlockRenderer_ExternalUrl_RendersRelNoopener()
    {
        using var doc = JsonDocument.Parse("""
        {
          "audio": {
            "type": "external",
            "external": { "url": "https://cdn.example.com/track.mp3" }
          }
        }
        """);

        var html = await new AudioBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
    }

    // ── ColumnListBlockRenderer with children ────────────────────────────

    [Fact]
    public async Task ColumnListBlockRenderer_EmptyColumn_ReturnsEmptyWrapper()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "cols-1",
          "has_children": true,
          "column_list": {}
        }
        """);
        using var client = CreateClient(new JsonHandler(_ => """
        {
          "has_more": false,
          "results": [
            {
              "type": "column",
              "id": "col-1",
              "has_children": false,
              "column": {}
            }
          ]
        }
        """));
        var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);

        var html = await new ColumnListBlockRenderer().RenderAsync(doc.RootElement, context, CancellationToken.None);

        Assert.Equal("<div class=\"notion-columns\"><div class=\"notion-column\"></div>\n</div>", html);
    }

    // ── ColumnBlockRenderer with width_ratio edge ────────────────────────

    [Fact]
    public async Task ColumnBlockRenderer_WidthRatioZero_OutputsNoStyle()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "col-1",
          "has_children": false,
          "column": {
            "width_ratio": 0.0
          }
        }
        """);

        var html = await new ColumnBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Equal("<div class=\"notion-column\"></div>", html);
    }

    [Fact]
    public async Task ColumnBlockRenderer_WidthRatioOne_OutputsFullFlexStyle()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "col-1",
          "has_children": false,
          "column": {
            "width_ratio": 1.0
          }
        }
        """);

        var html = await new ColumnBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Equal("<div class=\"notion-column\" style=\"flex:0 0 100%\"></div>", html);
    }

    // ── VideoBlockRenderer without caption ───────────────────────────────

    [Fact]
    public async Task VideoBlockRenderer_WithoutCaption_RendersVideoOnly()
    {
        using var doc = JsonDocument.Parse("""
        {
          "video": {
            "type": "external",
            "external": { "url": "https://cdn.example.com/video.mp4" }
          }
        }
        """);

        var html = await new VideoBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Equal("<video src=\"https://cdn.example.com/video.mp4\" controls></video>", html);
    }

    // ── VideoBlockRenderer youtu.be path ─────────────────────────────────

    [Fact]
    public async Task VideoBlockRenderer_YouTubeShortUrl_RendersEmbed()
    {
        using var doc = JsonDocument.Parse("""
        {
          "video": {
            "type": "external",
            "external": { "url": "https://youtu.be/xyz789" }
          }
        }
        """);

        var html = await new VideoBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("https://www.youtube.com/embed/xyz789", html);
    }

    [Fact]
    public async Task VideoBlockRenderer_YouTubeEmbedUrl_RendersEmbed()
    {
        using var doc = JsonDocument.Parse("""
        {
          "video": {
            "type": "external",
            "external": { "url": "https://www.youtube.com/embed/abc123" }
          }
        }
        """);

        var html = await new VideoBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("https://www.youtube.com/embed/abc123", html);
    }

    // ── LinkPreviewBlockRenderer missing container ───────────────────────

    [Fact]
    public async Task LinkPreviewBlockRenderer_MissingContainer_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{}");

        var html = await new LinkPreviewBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    // ── BookmarkBlockRenderer with caption ───────────────────────────────

    [Fact]
    public async Task BookmarkBlockRenderer_WithCaption_RendersAnchorWithText()
    {
        using var doc = JsonDocument.Parse("""
        {
          "bookmark": {
            "url": "https://example.com/page",
            "caption": [{ "plain_text": "My Bookmark" }],
            "color": "default"
          }
        }
        """);

        var html = await new BookmarkBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("My Bookmark", html);
        Assert.Contains("href=\"https://example.com/page\"", html);
    }

    // ── NotionBlocksRenderer getter ──────────────────────────────────────

    [Fact]
    public void NotionBlocksRenderer_Registry_ReturnsRegistry()
    {
        using var http = new HttpClient(new HttpMessageHandlerStub());
        var options = new NotionProviderOptions { DatabaseId = "db", Token = "t" };
        using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);
        var renderer = new NotionBlocksRenderer(client);

        var registry = renderer.Registry;

        Assert.NotNull(registry);
        Assert.Same(registry, renderer.Registry);
    }

    // ── CalloutBlockRenderer emoji icon (no image URL) ───────────────────

    [Fact]
    public async Task CalloutBlockRenderer_EmojiIcon_RendersEmojiSpan()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "callout-1",
          "has_children": false,
          "callout": {
            "icon": {
              "type": "emoji",
              "emoji": "\u2728"
            },
            "rich_text": [{ "plain_text": "Sparkle callout" }]
          }
        }
        """);

        var html = await new CalloutBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"callout-icon\">\u2728</span>", html);
        Assert.Contains("Sparkle callout", html);
    }

    [Fact]
    public async Task CalloutBlockRenderer_NoColor_NoColorClass()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "callout-1",
          "has_children": false,
          "callout": {
            "icon": { "type": "emoji", "emoji": "\u2139" },
            "rich_text": [{ "plain_text": "Info" }],
            "color": "default"
          }
        }
        """);

        var html = await new CalloutBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("class=\"callout\"", html);
        Assert.DoesNotContain("notion-default", html);
    }

    // ── NotionRichTextRenderer edge cases ────────────────────────────────

    [Fact]
    public void NotionRichTextRenderer_MentionWithoutPlainText_Skipped()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "mention",
            "mention": { "type": "page", "page": { "id": "p-1" } }
          },
          {
            "type": "text",
            "plain_text": "after"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("after", html);
        Assert.DoesNotContain("data-mention-type", html);
    }

    [Fact]
    public void NotionRichTextRenderer_TextItemWithoutPlainTextKey_Skipped()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text"
          },
          {
            "type": "text",
            "plain_text": "visible"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("visible", html);
    }

    [Fact]
    public void NotionRichTextRenderer_UnknownColor_ReturnsInherit()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "custom color",
            "annotations": { "color": "teal" }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("color:inherit", html);
        Assert.Contains("custom color", html);
    }

    [Fact]
    public void NotionRichTextRenderer_NonArrayValueKind_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("{}");

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Equal(string.Empty, html);
    }

    // ── TableBlockRenderer with has_more pagination ──────────────────────

    [Fact]
    public async Task TableBlockRenderer_MultiplePages_ConcatenatesRows()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "table-1",
          "has_children": true,
          "table": {
            "has_column_header": false,
            "has_row_header": false
          }
        }
        """);
        using var client = CreateClient(new SequenceHandler(
            """
            {
              "has_more": true,
              "next_cursor": "cursor-1",
              "results": [
                {
                  "type": "table_row",
                  "table_row": { "cells": [[{ "plain_text": "A1" }], [{ "plain_text": "B1" }]] }
                }
              ]
            }
            """,
            """
            {
              "has_more": false,
              "results": [
                {
                  "type": "table_row",
                  "table_row": { "cells": [[{ "plain_text": "A2" }], [{ "plain_text": "B2" }]] }
                }
              ]
            }
            """));
        var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);

        var html = await new TableBlockRenderer().RenderAsync(doc.RootElement, context, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<td>A1</td><td>B1</td>", html);
        Assert.Contains("<td>A2</td><td>B2</td>", html);
    }

    [Fact]
    public async Task TableBlockRenderer_SkipsNonTableRowTypes()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "table-1",
          "has_children": true,
          "table": {
            "has_column_header": false,
            "has_row_header": false
          }
        }
        """);
        using var client = CreateClient(new JsonHandler(_ => """
        {
          "has_more": false,
          "results": [
            {
              "type": "paragraph",
              "paragraph": { "rich_text": [{ "plain_text": "skip me" }] }
            },
            {
              "type": "table_row",
              "table_row": { "cells": [[{ "plain_text": "Data" }]] }
            }
          ]
        }
        """));
        var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);

        var html = await new TableBlockRenderer().RenderAsync(doc.RootElement, context, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<td>Data</td>", html);
        Assert.DoesNotContain("skip me", html);
    }

    [Fact]
    public async Task TableBlockRenderer_SkipsMalformedTableRows()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "table-1",
          "has_children": true,
          "table": {
            "has_column_header": false,
            "has_row_header": false
          }
        }
        """);
        using var client = CreateClient(new JsonHandler(_ => """
        {
          "has_more": false,
          "results": [
            {
              "type": "table_row",
              "table_row": {}
            },
            {
              "type": "table_row",
              "table_row": { "cells": [[{ "plain_text": "Valid" }]] }
            }
          ]
        }
        """));
        var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);

        var html = await new TableBlockRenderer().RenderAsync(doc.RootElement, context, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<td>Valid</td>", html);
    }

    // ── RichTextContainerRenderer empty/invalid rich_text ────────────────

    [Fact]
    public async Task RichTextContainerRenderer_NoRichText_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "paragraph": {
            "color": "default"
          }
        }
        """);

        var html = await new RichTextContainerRenderer("paragraph", "p").RenderAsync(
            doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task RichTextContainerRenderer_EmptyRichText_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "paragraph": {
            "rich_text": []
          }
        }
        """);

        var html = await new RichTextContainerRenderer("paragraph", "p").RenderAsync(
            doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task RichTextContainerRenderer_HeadingEmptyRichText_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "heading_2": {
            "rich_text": []
          }
        }
        """);

        var html = await new RichTextContainerRenderer("heading_2", "h2").RenderAsync(
            doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task RichTextContainerRenderer_BlockquoteWithChildren_RendersNested()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "bq-1",
          "has_children": true,
          "blockquote": {
            "rich_text": [{ "plain_text": "Quote text" }]
          }
        }
        """);
        using var client = CreateClient(new JsonHandler(req =>
        {
            Assert.Contains("bq-1", req.RequestUri!.AbsoluteUri);
            return """
            {
              "has_more": false,
              "results": [
                {
                  "type": "paragraph",
                  "id": "p-1",
                  "paragraph": { "rich_text": [{ "plain_text": "Nested para" }] }
                }
              ]
            }
            """;
        }));
        var context = new NotionRenderContext(new NotionBlocksRenderer(client), client);

        var html = await new RichTextContainerRenderer("blockquote", "blockquote").RenderAsync(
            doc.RootElement, context, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<blockquote>Quote text<p>Nested para</p>", html);
    }

    [Fact]
    public async Task RichTextContainerRenderer_RenderChildrenIfAny_NoId_ReturnsEmptyChildren()
    {
        using var doc = JsonDocument.Parse("""
        {
          "has_children": true,
          "paragraph": {
            "rich_text": [{ "plain_text": "Has children but no id" }]
          }
        }
        """);

        var html = await new RichTextContainerRenderer("paragraph", "p").RenderAsync(
            doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains("<p>Has children but no id</p>", html);
    }

    [Fact]
    public async Task RichTextContainerRenderer_ToggleableHeading_NoRichText_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "heading_1": {
            "is_toggleable": true
          }
        }
        """);

        var html = await new RichTextContainerRenderer("heading_1", "h1").RenderAsync(
            doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Fact]
    public async Task RichTextContainerRenderer_ToggleableHeading_EmptyRichText_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "heading-1",
          "has_children": true,
          "heading_1": {
            "is_toggleable": true,
            "rich_text": []
          }
        }
        """);

        var html = await new RichTextContainerRenderer("heading_1", "h1").RenderAsync(
            doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    // ── SyncedBlockRenderer edge ─────────────────────────────────────────

    [Fact]
    public async Task SyncedBlockRenderer_OriginalSyncedBlock_NoChildren_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "sync-1",
          "has_children": false,
          "synced_block": {}
        }
        """);

        var html = await new SyncedBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    // ── EquationBlockRenderer empty expression ──────────────────────────

    [Fact]
    public async Task EquationBlockRenderer_EmptyExpression_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""
        {
          "equation": {
            "expression": "   "
          }
        }
        """);

        var html = await new EquationBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    // ── NotionBlocksRenderer list switching edge cases ───────────────────

    [Fact]
    public async Task NotionBlocksRenderer_NullType_BlockSkipped()
    {
        using var doc = JsonDocument.Parse("""
        {
          "has_more": false,
          "results": [
            {
              "type": "paragraph",
              "id": "p-1",
              "paragraph": { "rich_text": [{ "plain_text": "Valid" }] }
            }
          ]
        }
        """);
        var handler = new JsonHandler(req =>
        {
            return doc.RootElement.GetRawText();
        });
        using var client = CreateClient(handler);
        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        Assert.Contains("<p>Valid</p>", sb.ToString());
    }

    [Fact]
    public async Task NotionBlocksRenderer_HasMoreNoCursor_StopsPagination()
    {
        var handler = new SequenceHandler(
            """
            {
              "has_more": true,
              "next_cursor": "",
              "results": [
                {
                  "type": "paragraph",
                  "id": "p-1",
                  "paragraph": { "rich_text": [{ "plain_text": "Only page" }] }
                }
              ]
            }
            """,
            """
            {
              "has_more": false,
              "results": [
                {
                  "type": "paragraph",
                  "id": "p-2",
                  "paragraph": { "rich_text": [{ "plain_text": "Should not appear" }] }
                }
              ]
            }
            """);
        using var client = CreateClient(handler);
        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        var html = sb.ToString();
        Assert.Contains("Only page", html);
        Assert.DoesNotContain("Should not appear", html);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static NotionApiClient CreateClient(HttpMessageHandler handler)
    {
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0
        };
        return new NotionApiClient(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
    }

    private sealed class JsonHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _response;

        public JsonHandler(Func<HttpRequestMessage, string> response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response(request), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly string[] _responses;
        private int _index;

        public SequenceHandler(params string[] responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = _index < _responses.Length ? _responses[_index] : "{\"has_more\":false,\"results\":[]}";
            _index++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class HttpMessageHandlerStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}
