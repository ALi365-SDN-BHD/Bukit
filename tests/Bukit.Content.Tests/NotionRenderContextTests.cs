using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionRenderContextTests
{
    [Fact]
    public async Task RenderChildrenAsync_DelegatesToRenderer()
    {
        var handler = new RenderContextHttpHandler();
        using var http = new HttpClient(handler);
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0
        };
        using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);
        var renderer = new NotionBlocksRenderer(client);
        var context = new NotionRenderContext(renderer, client);

        var html = await context.RenderChildrenAsync("parent-id", CancellationToken.None);

        Assert.Contains("<p>Child content</p>", html);
        Assert.Single(handler.Invocations);
    }

    [Fact]
    public async Task RenderChildrenAsync_EmptyChildren_ReturnsEmptyString()
    {
        var handler = new EmptyChildrenHandler();
        using var http = new HttpClient(handler);
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0
        };
        using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);
        var renderer = new NotionBlocksRenderer(client);
        var context = new NotionRenderContext(renderer, client);

        var html = await context.RenderChildrenAsync("parent-id", CancellationToken.None);

        Assert.True(string.IsNullOrEmpty(html) || html.All(c => c is '\r' or '\n'));
    }

    [Fact]
    public void Constructor_ExposesClient()
    {
        using var http = new HttpClient(new EmptyChildrenHandler());
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0
        };
        using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);
        var renderer = new NotionBlocksRenderer(client);

        var context = new NotionRenderContext(renderer, client);

        Assert.Same(client, context.Client);
    }

    [Fact]
    public async Task RenderChildrenAsync_WithNestedListItems_RendersRecursively()
    {
        var handler = new NestedListHandler();
        using var http = new HttpClient(handler);
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0
        };
        using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);
        var renderer = new NotionBlocksRenderer(client);
        var context = new NotionRenderContext(renderer, client);

        var html = await context.RenderChildrenAsync("parent-id", CancellationToken.None);

        Assert.Contains("Top-level item", html);
        Assert.Contains("Nested child", html);
        Assert.Equal(2, handler.Invocations.Count);
    }

    private sealed class RenderContextHttpHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Invocations { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations.Add(request);
            var json = """
            {
              "has_more": false,
              "results": [
                {
                  "type": "paragraph",
                  "id": "blk-1",
                  "paragraph": { "rich_text": [{ "plain_text": "Child content" }] }
                }
              ]
            }
            """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class EmptyChildrenHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = """
            {
              "has_more": false,
              "results": []
            }
            """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class NestedListHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Invocations { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations.Add(request);
            string json;
            if (Invocations.Count == 1)
            {
                json = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "bulleted_list_item",
                      "id": "blk-1",
                      "has_children": true,
                      "bulleted_list_item": { "rich_text": [{ "plain_text": "Top-level item" }] }
                    }
                  ]
                }
                """;
            }
            else
            {
                json = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "id": "blk-2",
                      "paragraph": { "rich_text": [{ "plain_text": "Nested child" }] }
                    }
                  ]
                }
                """;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

}
