using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionClientTests
{
    [Fact]
    public async Task GetAsync_RetriesRateLimitForIdempotentRead()
    {
        var handler = new SequenceHandler(
            Response((HttpStatusCode)429, "{\"code\":\"rate_limited\"}", retryAfterSeconds: 2),
            Response(HttpStatusCode.OK, "{\"ok\":true}"));
        using var http = new HttpClient(handler);
        var delays = new List<int>();
        using var client = CreateClient("token", http, (milliseconds, _) =>
        {
            delays.Add(milliseconds);
            return Task.CompletedTask;
        });

        using var document = await client.GetAsync(NotionApiUrls.Database("db"));

        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal([2000], delays);
    }

    [Fact]
    public async Task SendAsync_DoesNotReplayNonReplayableWrite()
    {
        var handler = new SequenceHandler(
            Response((HttpStatusCode)429, "{\"code\":\"rate_limited\"}"),
            Response(HttpStatusCode.OK, "{\"ok\":true}"));
        using var http = new HttpClient(handler);
        using var client = CreateClient("token", http);
        using var request = new HttpRequestMessage(HttpMethod.Patch, NotionApiUrls.Pages("page"))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        var exception = await Assert.ThrowsAsync<NotionApiException>(() =>
            client.SendAsync(request, NotionRequestSemantics.NonReplayableWrite));

        Assert.Equal(NotionApiErrorKind.RateLimited, exception.Kind);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task SharedHttpClient_DoesNotCrossContaminateTokens()
    {
        var authorizations = new List<string?>();
        var versions = new List<string?>();
        var handler = new CallbackHandler(request =>
        {
            authorizations.Add(request.Headers.Authorization?.Parameter);
            versions.Add(request.Headers.TryGetValues("Notion-Version", out var values)
                ? values.Single()
                : null);
            return Response(HttpStatusCode.OK, "{}");
        });
        using var http = new HttpClient(handler);
        using var first = CreateClient("first-token", http);
        using var second = CreateClient("second-token", http);

        using var firstResult = await first.GetAsync(NotionApiUrls.Database("one"));
        using var secondResult = await second.GetAsync(NotionApiUrls.Database("two"));

        Assert.Equal(["first-token", "second-token"], authorizations);
        Assert.Equal([NotionApiUrls.NotionVersion, NotionApiUrls.NotionVersion], versions);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
        Assert.False(http.DefaultRequestHeaders.Contains("Notion-Version"));
    }

    [Fact]
    public async Task Dispose_DoesNotDisposeInjectedHttpClient()
    {
        var handler = new CallbackHandler(_ => Response(HttpStatusCode.OK, "{}"));
        using var http = new HttpClient(handler);
        var client = CreateClient("token", http);

        client.Dispose();

        using var response = await http.GetAsync("https://example.com/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ThrottleState_IsPerClientInstance()
    {
        var handler = new CallbackHandler(_ => Response(HttpStatusCode.OK, "{}"));
        using var http = new HttpClient(handler);
        var firstDelays = new List<int>();
        var secondDelays = new List<int>();
        var now = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);
        using var first = CreateClient("first", http, Capture(firstDelays), () => now, maxRps: 1);
        using var second = CreateClient("second", http, Capture(secondDelays), () => now, maxRps: 1);

        using var firstResult = await first.GetAsync(NotionApiUrls.Database("one"));
        using var secondResult = await second.GetAsync(NotionApiUrls.Database("two"));

        Assert.Empty(firstDelays);
        Assert.Empty(secondDelays);
    }

    [Fact]
    public async Task ThrottleState_TracksSecondRequestSeparatelyPerClient()
    {
        var handler = new CallbackHandler(_ => Response(HttpStatusCode.OK, "{}"));
        using var http = new HttpClient(handler);
        var firstDelays = new List<int>();
        var secondDelays = new List<int>();
        var now = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);
        using var first = CreateClient("first", http, Capture(firstDelays), () => now, maxRps: 1);
        using var second = CreateClient("second", http, Capture(secondDelays), () => now, maxRps: 1);

        using var firstOne = await first.GetAsync(NotionApiUrls.Database("first-one"));
        using var secondOne = await second.GetAsync(NotionApiUrls.Database("second-one"));
        using var firstTwo = await first.GetAsync(NotionApiUrls.Database("first-two"));
        using var secondTwo = await second.GetAsync(NotionApiUrls.Database("second-two"));

        Assert.Equal([1000], firstDelays);
        Assert.Equal([1000], secondDelays);
        Assert.Equal(1, first.GetStats().ThrottleWaitCount);
        Assert.Equal(1, second.GetStats().ThrottleWaitCount);
    }

    [Fact]
    public async Task HttpFailure_DoesNotExposeResponseBodyTokenOrUrl()
    {
        const string secret = "secret-value";
        var handler = new SequenceHandler(Response(
            HttpStatusCode.BadRequest,
            $"{{\"message\":\"token={secret}\"}}"));
        using var http = new HttpClient(handler);
        using var client = CreateClient(secret, http);

        var exception = await Assert.ThrowsAsync<NotionApiException>(() =>
            client.GetAsync($"https://api.notion.com/v1/databases/db?token={secret}"));

        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("api.notion.com", exception.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task Cancellation_PropagatesWithoutWrapping()
    {
        var handler = new CancelingHandler();
        using var http = new HttpClient(handler);
        using var client = CreateClient("token", http);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync(NotionApiUrls.Database("db"), cancellation.Token));
    }

    [Fact]
    public async Task Timeout_IsTranslatedToSafeTransportFailure()
    {
        const string secret = "timeout-secret";
        var handler = new ThrowingHandler(new TaskCanceledException(secret));
        using var http = new HttpClient(handler);
        using var client = CreateClient("token", http);

        var exception = await Assert.ThrowsAsync<NotionApiException>(() =>
            client.GetAsync(NotionApiUrls.Database("db")));

        Assert.Equal(NotionApiErrorKind.Transport, exception.Kind);
        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransportFailure_DoesNotExposeInnerUrlOrToken()
    {
        const string secret = "transport-secret";
        var handler = new ThrowingHandler(new HttpRequestException(
            $"failed https://api.notion.com/v1/databases/db?token={secret}"));
        using var http = new HttpClient(handler);
        using var client = CreateClient(secret, http);

        var exception = await Assert.ThrowsAsync<NotionApiException>(() =>
            client.GetAsync(NotionApiUrls.Database("db")));

        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("api.notion.com", exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(NotionApiErrorKind.Transport, exception.Kind);
    }

    [Fact]
    public void OptionsToString_DoesNotExposeToken()
    {
        const string secret = "options-secret";
        var options = new NotionClientOptions { Token = secret };

        Assert.DoesNotContain(secret, options.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_DisposesInternallyOwnedHttpClientExactlyOnce()
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
        string token,
        HttpClient http,
        Func<int, CancellationToken, Task>? delay = null,
        Func<DateTimeOffset>? utcNow = null,
        int? maxRps = null)
        => new(
            new NotionClientOptions
            {
                Token = token,
                MaxRetries = 2,
                MaxRps = maxRps
            },
            http,
            delay ?? ((_, _) => Task.CompletedTask),
            utcNow ?? (() => DateTimeOffset.UtcNow),
            ownsHttpClient: false);

    private static Func<int, CancellationToken, Task> Capture(List<int> delays)
        => (milliseconds, _) =>
        {
            delays.Add(milliseconds);
            return Task.CompletedTask;
        };

    private static HttpResponseMessage Response(
        HttpStatusCode status,
        string body,
        int? retryAfterSeconds = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (retryAfterSeconds is not null)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(
                TimeSpan.FromSeconds(retryAfterSeconds.Value));
        }

        return response;
    }

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

    private sealed class CallbackHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(callback(request));
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class TrackingDisposeHandler : HttpMessageHandler
    {
        public int DisposeCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(Response(HttpStatusCode.OK, "{}"));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }
}
