using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class CanonicalClientMigrationContractTests
{
    [Fact]
    public async Task PublicRendererConsumer_ContextExposesCanonicalNotionClient()
    {
        using var http = new HttpClient(new SequenceHandler(Json("""
            {
              "has_more": false,
              "results": [{ "type": "client_probe", "client_probe": {} }]
            }
            """)));
        using var client = CreateClient(http);
        var consumer = new CanonicalRendererConsumer();
        var renderer = new NotionBlocksRenderer(
            client,
            new NotionBlockRendererRegistry().Register("client_probe", consumer));

        var html = await renderer.RenderPageAsync("page", CancellationToken.None);

        Assert.Equal("<canonical-client>\n", html);
        Assert.IsType<NotionClient>(consumer.ReceivedClient);
        Assert.Same(client, consumer.ReceivedClient);
    }

    [Fact]
    public async Task MissingResults_ExposesNotionRenderingExceptionDirectly()
    {
        using var http = new HttpClient(new SequenceHandler(Json("{}")));
        using var client = CreateClient(http);
        var renderer = new NotionBlocksRenderer(client);

        var exception = await Assert.ThrowsAsync<NotionRenderingException>(() =>
            renderer.RenderPageAsync("page", CancellationToken.None));

        Assert.Equal("Notion blocks response missing results.", exception.Message);
    }

    [Theory]
    [InlineData(FailureKind.HttpStatus, NotionApiErrorKind.HttpStatus)]
    [InlineData(FailureKind.TerminalRateLimit, NotionApiErrorKind.RateLimited)]
    [InlineData(FailureKind.InvalidJson, NotionApiErrorKind.InvalidJson)]
    [InlineData(FailureKind.Transport, NotionApiErrorKind.Transport)]
    [InlineData(FailureKind.NonCallerCancellation, NotionApiErrorKind.Transport)]
    public async Task ApiFailures_ExposeNotionApiExceptionDirectly(
        FailureKind failure,
        NotionApiErrorKind expectedKind)
    {
        using var http = new HttpClient(new FailureHandler(failure));
        using var client = CreateClient(http);
        var renderer = new NotionBlocksRenderer(client);

        var exception = await Assert.ThrowsAsync<NotionApiException>(() =>
            renderer.RenderPageAsync("page", CancellationToken.None));

        Assert.Equal(expectedKind, exception.Kind);
    }

    [Theory]
    [InlineData(CallbackEntryPoint.Renderer, CallbackFailureKind.Rendering)]
    [InlineData(CallbackEntryPoint.Renderer, CallbackFailureKind.Api)]
    [InlineData(CallbackEntryPoint.Renderer, CallbackFailureKind.ConsumerDefined)]
    [InlineData(CallbackEntryPoint.Transformer, CallbackFailureKind.Rendering)]
    [InlineData(CallbackEntryPoint.Transformer, CallbackFailureKind.Api)]
    [InlineData(CallbackEntryPoint.Transformer, CallbackFailureKind.ConsumerDefined)]
    public async Task CallbackExceptions_PropagateOriginalInstanceWithoutTranslation(
        CallbackEntryPoint entryPoint,
        CallbackFailureKind failureKind)
    {
        using var http = new HttpClient(new SequenceHandler(Json("""
            {
              "has_more": false,
              "results": [{ "type": "callback_failure", "callback_failure": {} }]
            }
            """)));
        using var client = CreateClient(http);
        var expected = CreateCallbackException(failureKind);
        var registry = new NotionBlockRendererRegistry();
        if (entryPoint == CallbackEntryPoint.Renderer)
        {
            registry.Register("callback_failure", new ThrowingRenderer(expected));
        }
        else
        {
            registry.SetCustomTransformer(
                "callback_failure",
                (_, _, _) => throw expected);
        }

        var renderer = new NotionBlocksRenderer(client, registry);

        var actual = await Record.ExceptionAsync(() =>
            renderer.RenderPageAsync("page", CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesWithOriginalToken()
    {
        using var http = new HttpClient(new CancelingHandler());
        using var client = CreateClient(http);
        var renderer = new NotionBlocksRenderer(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            renderer.RenderPageAsync("page", cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task PublicOptionsAndExplicitRequestSemantics_PreserveMigrationMappingAndHeaders()
    {
        var handler = new HeaderCaptureHandler();
        using var http = new HttpClient(handler);
        var options = new NotionClientOptions
        {
            Token = "migration-token",
            ApiVersion = "2025-09-03",
            RequestDelayMs = 17,
            MaxRetries = 4,
            MaxRps = 3
        };
        using var client = new NotionClient(options, http);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.notion.com/v1/databases/database-id/query")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, NotionRequestSemantics.IdempotentRead);

        Assert.Equal("migration-token", handler.AuthorizationParameter);
        Assert.Equal("2025-09-03", handler.NotionVersion);
        Assert.Equal(17, options.RequestDelayMs);
        Assert.Equal(4, options.MaxRetries);
        Assert.Equal(3, options.MaxRps);
        Assert.Equal(TimeSpan.FromSeconds(30), new NotionClientOptions { Token = "token" }.Timeout);
    }

    [Fact]
    public async Task DatabaseQueryPost_AsIdempotentRead_Retries429ThenSucceeds()
    {
        var handler = new SequenceHandler(
            RateLimited(),
            Json("{\"ok\":true}"));
        using var http = new HttpClient(handler);
        using var client = CreateClient(http, maxRetries: 1);
        using var request = DatabaseQueryRequest();

        using var response = await client.SendAsync(request, NotionRequestSemantics.IdempotentRead);

        Assert.True(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task WriteRequest_AsNonReplayableWrite_DoesNotReplayAfter429()
    {
        var handler = new SequenceHandler(RateLimited());
        using var http = new HttpClient(handler);
        using var client = CreateClient(http, maxRetries: 3);
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            "https://api.notion.com/v1/pages/page-id")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        var exception = await Assert.ThrowsAsync<NotionApiException>(() =>
            client.SendAsync(request, NotionRequestSemantics.NonReplayableWrite));

        Assert.Equal(NotionApiErrorKind.RateLimited, exception.Kind);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Rendering_DoesNotTakeOwnershipOfCanonicalClient()
    {
        var handler = new SequenceHandler(
            Json("{\"has_more\":false,\"results\":[]}"),
            Json("{\"ok\":true}"));
        using var http = new HttpClient(handler);
        using var client = CreateClient(http);
        var renderer = new NotionBlocksRenderer(client);

        var html = await renderer.RenderPageAsync("page", CancellationToken.None);
        using var response = await client.GetAsync("https://api.notion.com/v1/databases/database-id");

        Assert.Equal(string.Empty, html);
        Assert.True(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task DisposingClientWithInjectedHttpClient_LeavesHttpClientUsable()
    {
        var handler = new SequenceHandler(Json("{}"));
        using var http = new HttpClient(handler);
        var client = new NotionClient(new NotionClientOptions { Token = "token" }, http);

        client.Dispose();
        using var response = await http.GetAsync("https://example.com/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task PublicOneArgumentConstructor_DisposesInternallyOwnedHttpClient()
    {
        var client = new NotionClient(new NotionClientOptions { Token = "token" });
        var httpClientField = typeof(NotionClient).GetField(
            "_httpClient",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(httpClientField);
        var internallyOwnedHttpClient = Assert.IsType<HttpClient>(httpClientField.GetValue(client));

        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            internallyOwnedHttpClient.GetAsync("https://example.invalid/"));
    }

    [Fact]
    public void InternalOwnershipConstructor_DisposesHandlerExactlyOnceWhenDisposeIsRepeated()
    {
        var handler = new TrackingDisposeHandler();
        var http = new HttpClient(handler);
        var client = new NotionClient(
            new NotionClientOptions { Token = "token" },
            http,
            (_, _) => Task.CompletedTask,
            () => DateTimeOffset.UtcNow,
            ownsHttpClient: true);

        client.Dispose();
        client.Dispose();

        Assert.Equal(1, handler.DisposeCount);
    }

    private static NotionClient CreateClient(
        HttpClient http,
        int maxRetries = 0)
        => new(
            new NotionClientOptions
            {
                Token = "token",
                MaxRetries = maxRetries
            },
            http);

    private static HttpRequestMessage DatabaseQueryRequest()
        => new(HttpMethod.Post, "https://api.notion.com/v1/databases/database-id/query")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage Json(string body)
        => Json(HttpStatusCode.OK, body);

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage RateLimited()
    {
        var response = Json(HttpStatusCode.TooManyRequests, "{\"code\":\"rate_limited\"}");
        response.Headers.TryAddWithoutValidation("Retry-After", "0");
        return response;
    }

    public enum FailureKind
    {
        HttpStatus,
        TerminalRateLimit,
        InvalidJson,
        Transport,
        NonCallerCancellation
    }

    public enum CallbackEntryPoint
    {
        Renderer,
        Transformer
    }

    public enum CallbackFailureKind
    {
        Rendering,
        Api,
        ConsumerDefined
    }

    private sealed class CanonicalRendererConsumer : INotionBlockRenderer
    {
        public NotionClient? ReceivedClient { get; private set; }

        public Task<string?> RenderAsync(
            JsonElement block,
            NotionRenderContext context,
            CancellationToken cancellationToken)
        {
            ReceivedClient = context.Client;
            return Task.FromResult<string?>("<canonical-client>");
        }
    }

    private sealed class ThrowingRenderer(Exception exception) : INotionBlockRenderer
    {
        public Task<string?> RenderAsync(
            JsonElement block,
            NotionRenderContext context,
            CancellationToken cancellationToken)
            => throw exception;
    }

    private sealed class ConsumerCallbackException(string message) : Exception(message);

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
                FailureKind.NonCallerCancellation => Task.FromException<HttpResponseMessage>(
                    new OperationCanceledException("deterministic non-caller cancellation")),
                _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
            };
    }

    private sealed class HeaderCaptureHandler : HttpMessageHandler
    {
        public string? AuthorizationParameter { get; private set; }

        public string? NotionVersion { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            NotionVersion = request.Headers.TryGetValues("Notion-Version", out var values)
                ? values.Single()
                : null;
            return Task.FromResult(Json("{}"));
        }
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }

    private sealed class TrackingDisposeHandler : HttpMessageHandler
    {
        public int DisposeCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(Json("{}"));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }

    private static Exception CreateCallbackException(CallbackFailureKind failureKind)
        => failureKind switch
        {
            CallbackFailureKind.Rendering => new NotionRenderingException("callback rendering failure"),
            CallbackFailureKind.Api => new NotionApiException(
                NotionApiErrorKind.Transport,
                "callback API failure"),
            CallbackFailureKind.ConsumerDefined => new ConsumerCallbackException(
                "consumer callback failure"),
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, null)
        };
}
