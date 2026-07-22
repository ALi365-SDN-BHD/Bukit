using System.Net;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionRenderingTests
{
    [Fact]
    public async Task RenderPageAsync_RendersPaginationAndPreservesEscaping()
    {
        var handler = new SequenceHandler(
            Json("""
                {
                  "has_more": true,
                  "next_cursor": "cursor/value",
                  "results": [
                    {
                      "type": "paragraph",
                      "paragraph": {
                        "rich_text": [{ "plain_text": "First <page>" }]
                      }
                    }
                  ]
                }
                """),
            Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "paragraph": {
                        "rich_text": [{ "plain_text": "Unsafe", "href": "javascript:alert(1)" }]
                      }
                    }
                  ]
                }
                """));
        using var http = new HttpClient(handler);
        using var transport = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            http);
        var renderer = new NotionBlocksRenderer(transport);

        var html = await renderer.RenderPageAsync("page", CancellationToken.None);

        Assert.Equal("<p>First &lt;page&gt;</p>\n<p>Unsafe</p>\n", html);
        Assert.Equal(2, handler.Urls.Count);
        Assert.Equal(
            "https://api.notion.com/v1/blocks/page/children?page_size=100&start_cursor=cursor%2Fvalue",
            handler.Urls[1]);
    }

    [Fact]
    public async Task CustomTransformer_CanOverrideAndFallBack()
    {
        var handler = new SequenceHandler(Json("""
            {
              "has_more": false,
              "results": [
                {
                  "type": "paragraph",
                  "paragraph": { "rich_text": [{ "plain_text": "Original" }] }
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        using var transport = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            http);
        var registry = NotionBlockRendererRegistry.CreateDefault()
            .SetCustomTransformer("paragraph", (_, _, _) =>
                Task.FromResult<string?>("<p>Custom</p>"));
        var renderer = new NotionBlocksRenderer(transport, registry);

        var html = await renderer.RenderPageAsync("page", CancellationToken.None);

        Assert.Equal("<p>Custom</p>\n", html);
    }

    [Fact]
    public async Task MissingResults_UsesRenderingExceptionWithoutContentDependency()
    {
        var handler = new SequenceHandler(Json("{}"));
        using var http = new HttpClient(handler);
        using var transport = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            http);
        var renderer = new NotionBlocksRenderer(transport);

        var exception = await Assert.ThrowsAsync<NotionRenderingException>(() =>
            renderer.RenderPageAsync("page", CancellationToken.None));

        Assert.Equal("Notion blocks response missing results.", exception.Message);
    }

    [Fact]
    public async Task RenderPageAsync_PropagatesCallerCancellationUnchanged()
    {
        using var http = new HttpClient(new CancelingHandler());
        using var transport = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            http);
        var renderer = new NotionBlocksRenderer(transport);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            renderer.RenderPageAsync("page", cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public void ColorPalette_PreservesCanonicalValues()
    {
        Assert.Equal("#0B6E99", NotionColorPalette.ToForeground("blue"));
        Assert.Equal("#E7F3F8", NotionColorPalette.ToBackground("blue_background"));
        Assert.Equal(NotionColorPalette.DefaultBg, NotionColorPalette.ToBackground("unknown"));
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri?.ToString() ?? string.Empty);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }
}
