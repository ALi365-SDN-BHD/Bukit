using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class CanonicalExtensionGraphMigrationContractTests
{
    [Fact]
    public void Register_NullRenderer_ThrowsImmediatelyWithRendererParameterName()
    {
        var registry = new NotionBlockRendererRegistry();

        var exception = Assert.Throws<ArgumentNullException>(() => registry.Register("paragraph", null!));

        Assert.Equal("renderer", exception.ParamName);
    }

    [Fact]
    public void SetCustomTransformer_NullTransformer_ThrowsImmediatelyWithTransformerParameterName()
    {
        var registry = new NotionBlockRendererRegistry();

        var exception = Assert.Throws<ArgumentNullException>(() => registry.SetCustomTransformer("paragraph", null!));

        Assert.Equal("transformer", exception.ParamName);
    }

    [Fact]
    public async Task CustomRenderer_ReceivesSourceClientAndToken_AndRendersPaginatedChildren()
    {
        const string parentBlock = "{\"id\":\"parent\",\"type\":\"custom_parent\",\"has_children\":true,\"custom_parent\":{\"marker\":\"exact-source\"}}";
        var handler = new RoutingHandler(request => request.RequestUri!.ToString() switch
        {
            "https://api.notion.com/v1/blocks/page/children?page_size=100" =>
                $"{{\"has_more\":false,\"results\":[{parentBlock}]}}",
            "https://api.notion.com/v1/blocks/parent/children?page_size=100" =>
                "{\"has_more\":true,\"next_cursor\":\"second-page\",\"results\":[{\"type\":\"paragraph\",\"paragraph\":{\"rich_text\":[{\"plain_text\":\"first child\"}]}}]}",
            "https://api.notion.com/v1/blocks/parent/children?page_size=100&start_cursor=second-page" =>
                "{\"has_more\":false,\"results\":[{\"type\":\"paragraph\",\"paragraph\":{\"rich_text\":[{\"plain_text\":\"second child\"}]}}]}",
            _ => throw new InvalidOperationException($"Unexpected request: {request.RequestUri}")
        });
        using var client = CanonicalBlockRendererTestSupport.CreateClient(handler);
        using var cancellation = new CancellationTokenSource();
        var customRenderer = new ChildrenRenderingRenderer(parentBlock);
        var registry = NotionBlockRendererRegistry.CreateDefault()
            .Register("custom_parent", customRenderer);
        var renderer = new NotionBlocksRenderer(client, registry);

        var html = await renderer.RenderPageAsync("page", cancellation.Token);

        Assert.Equal("<section><p>first child</p>\n<p>second child</p>\n</section>\n", html);
        Assert.Same(client, customRenderer.ReceivedClient);
        Assert.Equal(cancellation.Token, customRenderer.ReceivedToken);
        Assert.Equal(parentBlock, customRenderer.ReceivedBlockJson);
        Assert.Equal(3, handler.Urls.Count);
        Assert.All(handler.CancellationTokens, token => Assert.True(token.CanBeCanceled));
    }

    [Fact]
    public async Task CustomRenderer_RenderChildrenAsync_PropagatesCallerCancellationToTheNestedRequest()
    {
        using var handler = new NestedRequestCancellationHandler();
        using var client = CanonicalBlockRendererTestSupport.CreateClient(handler);
        using var cancellation = new CancellationTokenSource();
        var registry = new NotionBlockRendererRegistry()
            .Register("custom_parent", new ChildRenderingOnlyRenderer());
        var renderer = new NotionBlocksRenderer(client, registry);

        var rendering = renderer.RenderPageAsync("page", cancellation.Token);
        await handler.NestedRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rendering);
        await handler.NestedRequestCanceled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Registry_PublicOverridesAndTransformerLifecycle_PreserveCanonicalFallbackAndUnknownBehavior()
    {
        var handler = new RoutingHandler(_ => """
            {
              "has_more": false,
              "results": [
                { "type": "paragraph", "paragraph": { "rich_text": [] } },
                { "type": "unknown_block", "unknown_block": {} }
              ]
            }
            """);
        using var client = CanonicalBlockRendererTestSupport.CreateClient(handler);
        var first = new StaticRenderer("<first>");
        var replacement = new StaticRenderer("<replacement>");
        var registry = NotionBlockRendererRegistry.CreateDefault()
            .Register("paragraph", first)
            .Register("paragraph", replacement);
        var renderer = new NotionBlocksRenderer(client, registry);
        NotionBlockTransformer transformer = (_, _, _) => Task.FromResult<string?>("<transformer>");

        registry.SetCustomTransformer("paragraph", transformer);
        var transformed = await renderer.RenderPageAsync("page", CancellationToken.None);

        Assert.Equal("<transformer>\n", transformed);
        Assert.Equal(0, first.CallCount);
        Assert.Equal(0, replacement.CallCount);

        registry.SetCustomTransformer("paragraph", (_, _, _) => Task.FromResult<string?>(null));
        var fallback = await renderer.RenderPageAsync("page", CancellationToken.None);

        Assert.Equal("<replacement>\n", fallback);
        Assert.Equal(0, first.CallCount);
        Assert.Equal(1, replacement.CallCount);

        registry.RemoveCustomTransformer("paragraph");
        var removed = await renderer.RenderPageAsync("page", CancellationToken.None);

        Assert.Equal("<replacement>\n", removed);
        Assert.Equal(0, first.CallCount);
        Assert.Equal(2, replacement.CallCount);
    }

    [Fact]
    public async Task SharedRegistry_UsesTheClientOwnedByEachRendererInstance()
    {
        var clientAHandler = new RoutingHandler(_ => """
            { "has_more": false, "results": [{ "type": "client_probe", "client_probe": {} }] }
            """);
        var clientBHandler = new RoutingHandler(_ => """
            { "has_more": false, "results": [{ "type": "client_probe", "client_probe": {} }] }
            """);
        using var clientA = CanonicalBlockRendererTestSupport.CreateClient(clientAHandler);
        using var clientB = CanonicalBlockRendererTestSupport.CreateClient(clientBHandler);
        var receivedClients = new List<NotionClient>();
        var registry = new NotionBlockRendererRegistry()
            .SetCustomTransformer("client_probe", (_, context, _) =>
            {
                receivedClients.Add(context.Client);
                return Task.FromResult<string?>("<probe>");
            });
        var rendererA = new NotionBlocksRenderer(clientA, registry);
        var rendererB = new NotionBlocksRenderer(clientB, registry);

        var htmlA = await rendererA.RenderPageAsync("page-a", CancellationToken.None);
        var htmlB = await rendererB.RenderPageAsync("page-b", CancellationToken.None);

        Assert.Equal("<probe>\n", htmlA);
        Assert.Equal("<probe>\n", htmlB);
        Assert.Collection(
            receivedClients,
            received => Assert.Same(clientA, received),
            received => Assert.Same(clientB, received));
    }

    private sealed class ChildrenRenderingRenderer(string expectedBlockJson) : INotionBlockRenderer
    {
        public string? ReceivedBlockJson { get; private set; }

        public NotionClient? ReceivedClient { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public async Task<string?> RenderAsync(
            JsonElement block,
            NotionRenderContext context,
            CancellationToken cancellationToken)
        {
            ReceivedBlockJson = block.GetRawText();
            Assert.Equal(expectedBlockJson, ReceivedBlockJson);
            ReceivedClient = context.Client;
            ReceivedToken = cancellationToken;

            var children = await context.RenderChildrenAsync("parent", cancellationToken);
            return $"<section>{children}</section>";
        }
    }

    private sealed class StaticRenderer(string result) : INotionBlockRenderer
    {
        public int CallCount { get; private set; }

        public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<string?>(result);
        }
    }

    private sealed class ChildRenderingOnlyRenderer : INotionBlockRenderer
    {
        public async Task<string?> RenderAsync(
            JsonElement block,
            NotionRenderContext context,
            CancellationToken cancellationToken)
            => await context.RenderChildrenAsync("parent", cancellationToken);
    }

    private sealed class RoutingHandler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        public List<CancellationToken> CancellationTokens { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri?.ToString() ?? string.Empty);
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(request), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class NestedRequestCancellationHandler : HttpMessageHandler
    {
        public TaskCompletionSource NestedRequestStarted { get; } = new();

        public TaskCompletionSource NestedRequestCanceled { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.ToString() == "https://api.notion.com/v1/blocks/page/children?page_size=100")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"has_more\":false,\"results\":[{\"type\":\"custom_parent\",\"custom_parent\":{}}]}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (request.RequestUri?.ToString() == "https://api.notion.com/v1/blocks/parent/children?page_size=100")
            {
                NestedRequestStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    NestedRequestCanceled.TrySetResult();
                    throw;
                }
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }
    }
}
