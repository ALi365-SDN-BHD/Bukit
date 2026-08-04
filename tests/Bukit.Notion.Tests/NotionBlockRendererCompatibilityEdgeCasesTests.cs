#pragma warning disable CS0618 // Test-only use of obsolete injected-HttpClient constructor
using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionBlockRendererCompatibilityEdgeCasesTests
{
    [Fact]
    public void NotionBlocksRenderer_Registry_ReturnsRegistry()
    {
        var handler = new HttpMessageHandlerStub();
        var options = new NotionClientOptions
        {
            Token = "t",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);
        var renderer = new NotionBlocksRenderer(client);

        var registry = renderer.Registry;

        Assert.NotNull(registry);
        Assert.Same(registry, renderer.Registry);
    }

    // ── CalloutBlockRenderer emoji icon (no image URL) ───────────────────

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
    public async Task NotionBlocksRenderer_HasMoreNoCursor_ThrowsMissingCursor()
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

        // CI-08 contract: has_more without a cursor is a fail-closed pagination error
        // instead of silently stopping (which could hide truncated pages).
        var exception = await Assert.ThrowsAsync<NotionPaginationException>(
            () => renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None));
        Assert.Equal(NotionPaginationGuard.ReasonMissingCursor, exception.Reason);
        Assert.Contains("Only page", sb.ToString());
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static NotionClient CreateClient(HttpMessageHandler handler)
    {
        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        return CanonicalBlockRendererTestSupport.CreateClient(options, handler);
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
