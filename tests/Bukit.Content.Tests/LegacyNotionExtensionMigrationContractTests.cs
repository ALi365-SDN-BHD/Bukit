using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class LegacyNotionExtensionMigrationContractTests
{
    [Fact]
    public async Task PublicRendererConsumer_ContextExposesLegacyNotionApiClient()
    {
        using var http = new HttpClient(new SequenceHandler(Json("""
            {
              "has_more": false,
              "results": [{ "type": "client_probe", "client_probe": {} }]
            }
            """)));
        using var client = CreateClient(http);
        var consumer = new LegacyRendererConsumer();
        var renderer = new Bukit.Content.Notion.NotionBlocksRenderer(
            client,
            new Bukit.Content.Notion.NotionBlockRendererRegistry().Register("client_probe", consumer));

        var html = await renderer.RenderPageAsync("page", CancellationToken.None);

        Assert.Equal("<legacy-client>\n", html);
        Assert.IsType<NotionApiClient>(consumer.ReceivedClient);
        Assert.Same(client, consumer.ReceivedClient);
    }

    [Fact]
    public async Task MissingResults_ExposesContentExceptionWithRenderingInnerException()
    {
        using var http = new HttpClient(new SequenceHandler(Json("{}")));
        using var client = CreateClient(http);
        var renderer = new Bukit.Content.Notion.NotionBlocksRenderer(client);

        var exception = await Assert.ThrowsAsync<ContentException>(() =>
            renderer.RenderPageAsync("page", CancellationToken.None));

        Assert.IsType<NotionRenderingException>(exception.InnerException);
    }

    [Theory]
    [InlineData(FailureKind.HttpStatus, NotionApiErrorKind.HttpStatus)]
    [InlineData(FailureKind.TerminalRateLimit, NotionApiErrorKind.RateLimited)]
    [InlineData(FailureKind.InvalidJson, NotionApiErrorKind.InvalidJson)]
    [InlineData(FailureKind.Transport, NotionApiErrorKind.Transport)]
    public async Task ApiFailures_ExposeContentExceptionWithApiInnerException(
        FailureKind failure,
        NotionApiErrorKind expectedKind)
    {
        using var http = new HttpClient(new FailureHandler(failure));
        using var client = CreateClient(http);
        var renderer = new Bukit.Content.Notion.NotionBlocksRenderer(client);

        var exception = await Assert.ThrowsAsync<ContentException>(() =>
            renderer.RenderPageAsync("page", CancellationToken.None));

        var inner = Assert.IsType<NotionApiException>(exception.InnerException);
        Assert.Equal(expectedKind, inner.Kind);
    }

    [Fact]
    public async Task CustomRendererException_PropagatesWithoutTranslation()
    {
        using var http = new HttpClient(new SequenceHandler(Json("""
            {
              "has_more": false,
              "results": [{ "type": "custom_failure", "custom_failure": {} }]
            }
            """)));
        using var client = CreateClient(http);
        var expected = new ConsumerRendererException("consumer failure");
        var renderer = new Bukit.Content.Notion.NotionBlocksRenderer(
            client,
            new Bukit.Content.Notion.NotionBlockRendererRegistry()
                .Register("custom_failure", new ThrowingRenderer(expected)));

        var actual = await Assert.ThrowsAsync<ConsumerRendererException>(() =>
            renderer.RenderPageAsync("page", CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesWithOriginalToken()
    {
        using var http = new HttpClient(new CancelingHandler());
        using var client = CreateClient(http);
        var renderer = new Bukit.Content.Notion.NotionBlocksRenderer(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            renderer.RenderPageAsync("page", cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task Rendering_DoesNotTakeOwnershipOfLegacyClient()
    {
        var handler = new SequenceHandler(
            Json("{\"has_more\":false,\"results\":[]}"),
            Json("{\"ok\":true}"));
        using var http = new HttpClient(handler);
        using var client = CreateClient(http);
        var renderer = new Bukit.Content.Notion.NotionBlocksRenderer(client);

        var html = await renderer.RenderPageAsync("page", CancellationToken.None);
        using var response = await client.GetAsync(
            "https://api.notion.com/v1/databases/database-id",
            CancellationToken.None);

        Assert.Equal(string.Empty, html);
        Assert.True(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(2, handler.RequestCount);
    }

    private static NotionApiClient CreateClient(HttpClient http)
        => new(
            new NotionProviderOptions
            {
                DatabaseId = "database-id",
                Token = "token",
                MaxRetries = 0
            },
            http,
            (_, _) => Task.CompletedTask);

    private static HttpResponseMessage Json(string body)
        => Json(HttpStatusCode.OK, body);

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    public enum FailureKind
    {
        HttpStatus,
        TerminalRateLimit,
        InvalidJson,
        Transport
    }

    private sealed class LegacyRendererConsumer : Bukit.Content.Notion.INotionBlockRenderer
    {
        public NotionApiClient? ReceivedClient { get; private set; }

        public Task<string?> RenderAsync(
            JsonElement block,
            Bukit.Content.Notion.NotionRenderContext context,
            CancellationToken cancellationToken)
        {
            ReceivedClient = context.Client;
            return Task.FromResult<string?>("<legacy-client>");
        }
    }

    private sealed class ThrowingRenderer(Exception exception) : Bukit.Content.Notion.INotionBlockRenderer
    {
        public Task<string?> RenderAsync(
            JsonElement block,
            Bukit.Content.Notion.NotionRenderContext context,
            CancellationToken cancellationToken)
            => Task.FromException<string?>(exception);
    }

    private sealed class ConsumerRendererException(string message) : Exception(message);

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class FailureHandler(FailureKind failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => failure switch
            {
                FailureKind.HttpStatus => Task.FromResult(Json(HttpStatusCode.BadGateway, "{}")),
                FailureKind.TerminalRateLimit => Task.FromResult(Json(HttpStatusCode.TooManyRequests, "{}")),
                FailureKind.InvalidJson => Task.FromResult(Json("not-json")),
                FailureKind.Transport => Task.FromException<HttpResponseMessage>(
                    new HttpRequestException("deterministic transport failure")),
                _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
            };
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }
}
